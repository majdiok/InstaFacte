using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Billing;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services.Billing;

/// <summary>
/// Lot C5 — Orchestrateur de checkout : crée une <see cref="PaymentIntent"/>,
/// appelle le <see cref="IPaymentProviderClient"/> approprié, persiste l'URL
/// de redirection, ou bien renvoie les instructions virement pour Wire.
///
/// Côté Wire, l'appelant (admin) saisit ensuite le reçu via
/// <see cref="RegisterWireReceiptAsync"/>, ce qui crée un <see cref="PlatformReceipt"/>
/// directement (pas de redirection externe).
/// </summary>
public sealed class PaymentCheckoutService : IPaymentCheckoutService
{
    private readonly MasterDbContext _db;
    private readonly IEnumerable<IPaymentProviderClient> _providerClients;
    private readonly IPlatformReceiptAdminService _receiptAdminService;
    private readonly IPlatformFiscalSettingsService _fiscalService;
    private readonly PaymentProviderConfigService _configService;
    private readonly ILogger<PaymentCheckoutService> _logger;

    public PaymentCheckoutService(
        MasterDbContext db,
        IEnumerable<IPaymentProviderClient> providerClients,
        IPlatformReceiptAdminService receiptAdminService,
        IPlatformFiscalSettingsService fiscalService,
        PaymentProviderConfigService configService,
        ILogger<PaymentCheckoutService> logger)
    {
        _db = db;
        _providerClients = providerClients;
        _receiptAdminService = receiptAdminService;
        _fiscalService = fiscalService;
        _configService = configService;
        _logger = logger;
    }

    public async Task<Result<InitiateCheckoutResponse>> InitiateAsync(
        InitiateCheckoutRequest request,
        Guid tenantId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (request is null) return Result.Failure<InitiateCheckoutResponse>(Error.Validation("Request", "Requête invalide"));
        if (!PaymentProviderCodes.IsKnown(request.ProviderCode))
            return Result.Failure<InitiateCheckoutResponse>(Error.Validation("ProviderCode", "Provider inconnu"));

        var invoice = await _db.PlatformInvoices
            .Include(i => i.Receipts)
            .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken);
        if (invoice is null) return Result.Failure<InitiateCheckoutResponse>(Error.NotFound(nameof(PlatformInvoice), request.InvoiceId));
        if (invoice.TenantId != tenantId)
            return Result.Failure<InitiateCheckoutResponse>(Error.Forbidden("Cette facture ne vous appartient pas."));
        if (invoice.Status is PlatformInvoiceStatus.Draft)
            return Result.Failure<InitiateCheckoutResponse>(Error.Conflict("Le brouillon doit être émis avant paiement."));
        if (invoice.Status is PlatformInvoiceStatus.Paid or PlatformInvoiceStatus.Cancelled or PlatformInvoiceStatus.Refunded)
            return Result.Failure<InitiateCheckoutResponse>(Error.Conflict($"Facture {invoice.Status} : checkout indisponible."));

        var remaining = Math.Max(0m, invoice.TotalTTC - invoice.TotalReceived());
        if (remaining <= 0m)
            return Result.Failure<InitiateCheckoutResponse>(Error.Conflict("Facture déjà soldée."));

        var idemKey = BuildIdempotencyKey(invoice.Id, request.ProviderCode, remaining);
        var existing = await _db.PaymentIntents.FirstOrDefaultAsync(i => i.IdempotencyKey == idemKey, cancellationToken);
        var intent = existing ?? PaymentIntent.Create(
            tenantId: tenantId,
            invoiceId: invoice.Id,
            providerCode: request.ProviderCode,
            amountTND: remaining,
            idempotencyKey: idemKey,
            createdByUserId: actorUserId,
            returnUrl: request.ReturnUrl);
        if (existing is null)
        {
            _db.PaymentIntents.Add(intent);
            await _db.SaveChangesAsync(cancellationToken);
        }

        // Wire : on ne contacte aucune API. On renvoie les instructions IBAN.
        if (request.ProviderCode.Equals(PaymentProviderCodes.Wire, StringComparison.OrdinalIgnoreCase))
        {
            var fiscal = await _fiscalService.GetAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(fiscal.Iban))
                return Result.Failure<InitiateCheckoutResponse>(Error.Conflict("IBAN plateforme non configuré."));

            return Result.Success(new InitiateCheckoutResponse
            {
                IntentId = intent.Id,
                ProviderCode = PaymentProviderCodes.Wire,
                Status = intent.Status.ToString(),
                RedirectUrl = null,
                WireInstructions = new WireTransferInstructionsDto
                {
                    CompanyName = fiscal.CompanyName,
                    Iban = fiscal.Iban!,
                    BankName = fiscal.BankName,
                    Reference = invoice.Number ?? invoice.Id.ToString("N"),
                    AmountTND = remaining
                }
            });
        }

        var client = _providerClients.FirstOrDefault(c =>
            c.ProviderCode.Equals(request.ProviderCode, StringComparison.OrdinalIgnoreCase));
        if (client is null)
            return Result.Failure<InitiateCheckoutResponse>(Error.Conflict($"Provider {request.ProviderCode} non disponible."));

        var tenant = await _db.Tenants.FindAsync(new object?[] { tenantId }, cancellationToken);
        var initRequest = new ProviderInitRequest(
            IntentId: intent.Id,
            IdempotencyKey: idemKey,
            AmountTND: remaining,
            ReturnUrl: request.ReturnUrl,
            CustomerEmail: tenant?.Email?.Value,
            CustomerName: tenant?.CompanyName,
            Description: $"Paiement facture {invoice.Number ?? "FactuTrust"}");

        var result = await client.InitPaymentAsync(initRequest, cancellationToken);
        if (result.IsFailure)
        {
            intent.MarkFailed(result.Error.Description, rawPayload: null);
            await _db.SaveChangesAsync(cancellationToken);
            return Result.Failure<InitiateCheckoutResponse>(result.Error);
        }

        intent.AttachProviderRedirect(result.Value.ProviderRef, result.Value.RedirectUrl, result.Value.RawPayload);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success(new InitiateCheckoutResponse
        {
            IntentId = intent.Id,
            ProviderCode = request.ProviderCode,
            Status = intent.Status.ToString(),
            RedirectUrl = result.Value.RedirectUrl
        });
    }

    public async Task<Result<InitiateCheckoutResponse>> InitiateAdminAsync(
        InitiateCheckoutRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (request is null) return Result.Failure<InitiateCheckoutResponse>(Error.Validation("Request", "Requête invalide"));

        var tenantId = await _db.PlatformInvoices.AsNoTracking()
            .Where(i => i.Id == request.InvoiceId)
            .Select(i => (Guid?)i.TenantId)
            .FirstOrDefaultAsync(cancellationToken);
        if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
            return Result.Failure<InitiateCheckoutResponse>(Error.NotFound(nameof(PlatformInvoice), request.InvoiceId));

        // Réutilise la même logique que le flow tenant — la garde Forbidden ne se déclenche
        // pas car on passe le bon tenantId déduit de la facture.
        return await InitiateAsync(request, tenantId.Value, actorUserId, cancellationToken);
    }

    public async Task<Result> RegisterWireReceiptAsync(
        Guid invoiceId,
        RegisterWireReceiptRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (request is null) return Result.Failure(Error.Validation("Request", "Requête invalide"));

        var receiptResult = await _receiptAdminService.CreateAsync(
            invoiceId,
            new CreatePlatformReceiptRequest
            {
                AmountTND = request.AmountTND,
                Method = PlatformPaymentMethod.BankTransfer,
                PaymentDate = request.PaymentDate ?? DateTime.UtcNow,
                Reference = request.Reference,
                AutoConfirm = true
            },
            actorUserId,
            cancellationToken);

        if (receiptResult.IsFailure) return Result.Failure(receiptResult.Error);

        // Marque l'éventuel intent Wire associé comme Succeeded.
        var intent = await _db.PaymentIntents
            .Where(i => i.InvoiceId == invoiceId
                && i.ProviderCode == PaymentProviderCodes.Wire
                && (i.Status == PaymentIntentStatus.Created || i.Status == PaymentIntentStatus.RedirectIssued || i.Status == PaymentIntentStatus.Pending))
            .OrderByDescending(i => i.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (intent is not null)
        {
            intent.MarkSucceeded(rawPayload: $"{{\"reference\":\"{request.Reference}\",\"adminUserId\":\"{actorUserId}\"}}");
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }

    private static string BuildIdempotencyKey(Guid invoiceId, string providerCode, decimal remaining)
    {
        var amountKey = ((int)Math.Round(remaining * 1000m, 0)).ToString("D");
        return $"{providerCode.ToLowerInvariant()}:{invoiceId:N}:{amountKey}";
    }
}
