using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Logging;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Billing;
using FactuTrust.Domain.Common;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services.Billing;

/// <summary>
/// Lot C5 — Traitement idempotent des webhooks providers (Konnect / Paymee).
///
/// Algorithme :
/// <list type="number">
///   <item>Charge la config et le webhook secret en clair.</item>
///   <item>Valide la signature HMAC SHA-256 (constant-time).</item>
///   <item>Parse le payload via le client provider (extrait <c>EventId</c>, <c>ProviderRef</c>, outcome).</item>
///   <item>Si <c>(ProviderCode, EventId)</c> déjà reçu → ignore (200 OK).</item>
///   <item>Sinon, retrouve l'intent par <c>ProviderRef</c>, met à jour son statut + crée un PlatformReceipt si Succeeded.</item>
///   <item>Persiste le <see cref="PaymentWebhookEvent"/> avec son outcome de processing.</item>
/// </list>
/// </summary>
public sealed class PaymentWebhookHandler : IPaymentWebhookHandler
{
    private readonly MasterDbContext _db;
    private readonly IEnumerable<IPaymentProviderClient> _providerClients;
    private readonly PaymentProviderConfigService _configService;
    private readonly IWebhookSignatureValidator _signatureValidator;
    private readonly IPlatformReceiptAdminService _receiptAdminService;
    private readonly ILogger<PaymentWebhookHandler> _logger;

    public PaymentWebhookHandler(
        MasterDbContext db,
        IEnumerable<IPaymentProviderClient> providerClients,
        PaymentProviderConfigService configService,
        IWebhookSignatureValidator signatureValidator,
        IPlatformReceiptAdminService receiptAdminService,
        ILogger<PaymentWebhookHandler> logger)
    {
        _db = db;
        _providerClients = providerClients;
        _configService = configService;
        _signatureValidator = signatureValidator;
        _receiptAdminService = receiptAdminService;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(string providerCode, string payload, string? signatureHeader, CancellationToken cancellationToken = default)
    {
        if (!PaymentProviderCodes.IsKnown(providerCode))
            return Result.Failure(Error.Validation("ProviderCode", "Provider inconnu"));

        var configBundle = await _configService.ReadDecryptedAsync(providerCode, cancellationToken);
        if (configBundle is null) return Result.Failure(Error.NotFound("PaymentProviderConfig", Guid.Empty));
        var (_, webhookSecretPlain, config) = configBundle.Value;

        // 1. Validation signature HMAC
        var signatureOk = !string.IsNullOrWhiteSpace(webhookSecretPlain)
            && _signatureValidator.ValidateHmacSha256(payload, signatureHeader, webhookSecretPlain);

        // 2. Parse payload
        var client = _providerClients.FirstOrDefault(c => c.ProviderCode.Equals(providerCode, StringComparison.OrdinalIgnoreCase));
        if (client is null)
        {
            return Result.Failure(Error.Conflict($"Aucun client pour {providerCode}"));
        }
        var parseResult = client.ParseWebhook(payload);
        if (parseResult.IsFailure)
        {
            // On stocke quand même pour audit (avec signature invalide si applicable)
            var failedEvent = PaymentWebhookEvent.Create(providerCode, $"unparseable:{Guid.NewGuid():N}", payload, signatureOk);
            failedEvent.MarkFailedProcessing(parseResult.Error.Description);
            _db.PaymentWebhookEvents.Add(failedEvent);
            await _db.SaveChangesAsync(cancellationToken);
            return Result.Failure(parseResult.Error);
        }

        var providerEvent = parseResult.Value;

        // 3. Idempotence : (ProviderCode, EventId) UQ
        var alreadyReceived = await _db.PaymentWebhookEvents
            .FirstOrDefaultAsync(e => e.ProviderCode == providerCode.ToLowerInvariant()
                && e.ProviderEventId == providerEvent.EventId,
                cancellationToken);
        if (alreadyReceived is not null)
        {
            _logger.LogInformation("Webhook {Provider} {EventId} déjà reçu — ignored", LogSanitizer.Sanitize(providerCode), LogSanitizer.Sanitize(providerEvent.EventId));
            return Result.Success();
        }

        var webhookEvent = PaymentWebhookEvent.Create(providerCode, providerEvent.EventId, payload, signatureOk);
        _db.PaymentWebhookEvents.Add(webhookEvent);

        if (!signatureOk)
        {
            webhookEvent.MarkFailedProcessing("Signature HMAC invalide ou secret manquant");
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogWarning("Webhook {Provider} signature invalid for ref {Ref}", LogSanitizer.Sanitize(providerCode), LogSanitizer.Sanitize(providerEvent.ProviderRef));
            return Result.Failure(Error.Unauthorized("Signature webhook invalide"));
        }

        // 4. Retrouve l'intent
        var intent = await _db.PaymentIntents
            .FirstOrDefaultAsync(i => i.ProviderCode == providerCode.ToLowerInvariant()
                && i.ProviderRef == providerEvent.ProviderRef,
                cancellationToken);
        if (intent is null)
        {
            webhookEvent.MarkFailedProcessing($"Aucune intent trouvée pour ref {providerEvent.ProviderRef}");
            await _db.SaveChangesAsync(cancellationToken);
            return Result.Failure(Error.NotFound("PaymentIntent", Guid.Empty));
        }

        // 5. Mise à jour selon outcome + création reçu si Succeeded
        var rawPayload = providerEvent.RawPayloadEcho ?? payload;
        switch (providerEvent.Outcome)
        {
            case ProviderWebhookOutcome.Succeeded:
                if (intent.Status != PaymentIntentStatus.Succeeded)
                {
                    intent.MarkSucceeded(rawPayload);
                    await _db.SaveChangesAsync(cancellationToken);

                    // Crée le reçu plateforme
                    var method = providerCode.Equals(PaymentProviderCodes.Konnect, StringComparison.OrdinalIgnoreCase)
                        ? PlatformPaymentMethod.CardKonnect
                        : PlatformPaymentMethod.CardPaymee;
                    var receiptResult = await _receiptAdminService.CreateAsync(
                        intent.InvoiceId,
                        new CreatePlatformReceiptRequest
                        {
                            AmountTND = intent.AmountTND,
                            Method = method,
                            PaymentDate = DateTime.UtcNow,
                            Reference = intent.ProviderRef,
                            ProviderTxId = intent.ProviderRef,
                            AutoConfirm = true
                        },
                        actorUserId: intent.CreatedByUserId,
                        cancellationToken);
                    if (receiptResult.IsFailure)
                    {
                        webhookEvent.MarkFailedProcessing($"Reçu non créé : {receiptResult.Error.Description}");
                        await _db.SaveChangesAsync(cancellationToken);
                        return Result.Failure(receiptResult.Error);
                    }
                }
                break;

            case ProviderWebhookOutcome.Failed:
                if (intent.Status != PaymentIntentStatus.Failed)
                    intent.MarkFailed(providerEvent.FailureReason ?? "Failed", rawPayload);
                break;

            case ProviderWebhookOutcome.Cancelled:
                if (intent.Status != PaymentIntentStatus.Cancelled)
                    intent.MarkCancelled(rawPayload);
                break;

            case ProviderWebhookOutcome.Refunded:
                intent.MarkRefunded(rawPayload);
                break;

            case ProviderWebhookOutcome.Pending:
                intent.MarkPending(rawPayload);
                break;

            default:
                _logger.LogWarning("Webhook {Provider} outcome unknown for ref {Ref}", LogSanitizer.Sanitize(providerCode), LogSanitizer.Sanitize(providerEvent.ProviderRef));
                break;
        }

        webhookEvent.MarkProcessed(intent.Id);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
