using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Billing;
using FactuTrust.Domain.Common;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services.Billing;

/// <summary>
/// Lot C5 — Lecture / mise à jour de la config des providers de paiement.
///
/// Les secrets (API key, webhook secret) sont chiffrés via
/// <see cref="IDataProtectionProvider"/> avant stockage. Les DTOs renvoyés ne
/// contiennent jamais les secrets en clair, juste un flag <c>HasSecrets</c>.
///
/// La table <c>PaymentProviderConfigs</c> est seedée à la volée avec les 3
/// providers connus (<c>konnect</c>, <c>paymee</c>, <c>wire</c>) en état
/// désactivé / mode test.
/// </summary>
public sealed class PaymentProviderConfigService : IPaymentProviderConfigService
{
    private const string ProtectorPurpose = "FactuTrust.PaymentProviders.Secrets.v1";

    private readonly MasterDbContext _db;
    private readonly IDataProtector _protector;

    public PaymentProviderConfigService(MasterDbContext db, IDataProtectionProvider dataProtectionProvider)
    {
        _db = db;
        _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
    }

    public async Task<PaymentProviderConfigsListDto> ListAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);
        var rows = await _db.PaymentProviderConfigs.AsNoTracking().OrderBy(p => p.ProviderCode).ToListAsync(cancellationToken);
        var items = rows.Select(Map).ToList();
        return new PaymentProviderConfigsListDto
        {
            Items = items,
            EnabledCount = items.Count(i => i.IsEnabled)
        };
    }

    public async Task<Result<PaymentProviderConfigDto>> GetAsync(string providerCode, CancellationToken cancellationToken = default)
    {
        if (!PaymentProviderCodes.IsKnown(providerCode))
            return Result.Failure<PaymentProviderConfigDto>(Error.Validation("ProviderCode", "Provider inconnu"));

        await EnsureSeededAsync(cancellationToken);
        var entity = await _db.PaymentProviderConfigs.AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProviderCode == providerCode.ToLowerInvariant(), cancellationToken);
        if (entity is null) return Result.Failure<PaymentProviderConfigDto>(Error.NotFound("PaymentProviderConfig", Guid.Empty));
        return Result.Success(Map(entity));
    }

    public async Task<Result<PaymentProviderConfigDto>> UpdateAsync(
        string providerCode,
        UpdatePaymentProviderConfigRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (!PaymentProviderCodes.IsKnown(providerCode))
            return Result.Failure<PaymentProviderConfigDto>(Error.Validation("ProviderCode", "Provider inconnu"));
        if (request is null) return Result.Failure<PaymentProviderConfigDto>(Error.Validation("Request", "Requête invalide"));

        await EnsureSeededAsync(cancellationToken);
        var entity = await _db.PaymentProviderConfigs
            .FirstOrDefaultAsync(p => p.ProviderCode == providerCode.ToLowerInvariant(), cancellationToken);
        if (entity is null) return Result.Failure<PaymentProviderConfigDto>(Error.NotFound("PaymentProviderConfig", Guid.Empty));

        // Si l'admin envoie un secret, on le chiffre. Sinon on conserve l'existant.
        var encryptedSecrets = string.IsNullOrWhiteSpace(request.SecretsJson)
            ? entity.EncryptedSecretsJson
            : ProtectIfPresent(request.SecretsJson);
        var encryptedWebhook = string.IsNullOrWhiteSpace(request.WebhookSecret)
            ? entity.WebhookSecretEncrypted
            : ProtectIfPresent(request.WebhookSecret);

        try
        {
            entity.UpdateConfig(
                displayName: request.DisplayName,
                isEnabled: request.IsEnabled,
                isTestMode: request.IsTestMode,
                encryptedSecretsJson: encryptedSecrets,
                webhookSecretEncrypted: encryptedWebhook,
                allowedReturnDomain: request.AllowedReturnDomain);
            entity.SetAuditInfo(actorUserId.ToString(), isUpdate: true);
            await _db.SaveChangesAsync(cancellationToken);
            return Result.Success(Map(entity));
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<PaymentProviderConfigDto>(Error.Validation("Provider", ex.Message));
        }
    }

    /// <summary>
    /// Lit (en clair) les secrets et webhook d'un provider. Réservé aux services
    /// internes (PaymentCheckoutService, WebhookSignatureValidator). Ne jamais
    /// exposer via API.
    /// </summary>
    public async Task<(string? SecretsJsonPlain, string? WebhookSecretPlain, PaymentProviderConfig Config)?> ReadDecryptedAsync(
        string providerCode, CancellationToken cancellationToken = default)
    {
        if (!PaymentProviderCodes.IsKnown(providerCode)) return null;
        var entity = await _db.PaymentProviderConfigs.AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProviderCode == providerCode.ToLowerInvariant(), cancellationToken);
        if (entity is null) return null;
        var secrets = UnprotectIfPresent(entity.EncryptedSecretsJson);
        var webhook = UnprotectIfPresent(entity.WebhookSecretEncrypted);
        return (secrets, webhook, entity);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private string? ProtectIfPresent(string? plaintext) =>
        string.IsNullOrWhiteSpace(plaintext) ? null : _protector.Protect(plaintext);

    private string? UnprotectIfPresent(string? ciphertext)
    {
        if (string.IsNullOrWhiteSpace(ciphertext)) return null;
        try { return _protector.Unprotect(ciphertext); }
        catch { return null; }
    }

    private async Task EnsureSeededAsync(CancellationToken cancellationToken)
    {
        var existing = await _db.PaymentProviderConfigs.AsNoTracking()
            .Select(p => p.ProviderCode)
            .ToListAsync(cancellationToken);

        var seedDefaults = new (string Code, string Name)[]
        {
            (PaymentProviderCodes.Konnect, "Konnect Network"),
            (PaymentProviderCodes.Paymee, "Paymee.tn"),
            (PaymentProviderCodes.Wire, "Virement bancaire")
        };
        var added = false;
        foreach (var (code, name) in seedDefaults)
        {
            if (!existing.Contains(code))
            {
                _db.PaymentProviderConfigs.Add(PaymentProviderConfig.CreateDisabled(code, name));
                added = true;
            }
        }
        if (added) await _db.SaveChangesAsync(cancellationToken);
    }

    private static PaymentProviderConfigDto Map(PaymentProviderConfig p) => new()
    {
        Id = p.Id,
        ProviderCode = p.ProviderCode,
        DisplayName = p.DisplayName,
        IsEnabled = p.IsEnabled,
        IsTestMode = p.IsTestMode,
        AllowedReturnDomain = p.AllowedReturnDomain,
        HasSecrets = !string.IsNullOrWhiteSpace(p.EncryptedSecretsJson),
        HasWebhookSecret = !string.IsNullOrWhiteSpace(p.WebhookSecretEncrypted),
        UpdatedAt = p.UpdatedAt
    };
}
