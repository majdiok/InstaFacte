using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>Lot C5 — CRUD des configurations de providers + lecture côté admin.</summary>
public interface IPaymentProviderConfigService
{
    Task<PaymentProviderConfigsListDto> ListAsync(CancellationToken cancellationToken = default);

    Task<Result<PaymentProviderConfigDto>> GetAsync(string providerCode, CancellationToken cancellationToken = default);

    Task<Result<PaymentProviderConfigDto>> UpdateAsync(string providerCode, UpdatePaymentProviderConfigRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
}

/// <summary>Lot C5 — Initialisation d'un paiement côté tenant + cycle webhook.</summary>
public interface IPaymentCheckoutService
{
    Task<Result<InitiateCheckoutResponse>> InitiateAsync(InitiateCheckoutRequest request, Guid tenantId, Guid actorUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sous-lot C5.5 — Variante admin : déduit le <c>tenantId</c> depuis la facture (pas de
    /// vérification <see cref="ITenantContext"/>). Réservé aux admins plateforme avec la
    /// permission <c>platform.invoice:issue</c> (utile pour tester l'intégration provider
    /// ou rattraper un paiement qu'un tenant ne parvient pas à finaliser).
    /// </summary>
    Task<Result<InitiateCheckoutResponse>> InitiateAdminAsync(InitiateCheckoutRequest request, Guid actorUserId, CancellationToken cancellationToken = default);

    Task<Result> RegisterWireReceiptAsync(Guid invoiceId, RegisterWireReceiptRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
}

/// <summary>Lot C5 — Lecture des intentions de paiement (audit).</summary>
public interface IPaymentIntentQueryService
{
    Task<PaymentIntentsPageDto> ListAsync(
        string? providerCode,
        string? status,
        Guid? tenantId,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}

/// <summary>Lot C5 — Validation HMAC des webhooks providers.</summary>
public interface IWebhookSignatureValidator
{
    /// <summary>Valide la signature HMAC SHA-256 d'un payload (constant-time).</summary>
    bool ValidateHmacSha256(string payload, string? signatureHeader, string? webhookSecretEncrypted);
}

/// <summary>Lot C5 — Traitement idempotent d'un webhook reçu.</summary>
public interface IPaymentWebhookHandler
{
    /// <summary>Traite un webhook : valide signature, idempotence, met à jour intent + facture.</summary>
    Task<Result> HandleAsync(string providerCode, string payload, string? signatureHeader, CancellationToken cancellationToken = default);
}

/// <summary>Lot C5 — Client provider (Konnect/Paymee). Implémentation différente par provider.</summary>
public interface IPaymentProviderClient
{
    /// <summary>Code provider supporté par cette implémentation (<c>konnect</c>, <c>paymee</c>).</summary>
    string ProviderCode { get; }

    /// <summary>Initialise une transaction côté provider et renvoie l'URL de redirection.</summary>
    Task<Result<ProviderInitResult>> InitPaymentAsync(ProviderInitRequest request, CancellationToken cancellationToken = default);

    /// <summary>Parse la payload du webhook et extrait l'identifiant unique d'événement + ProviderRef + nouvel état.</summary>
    Result<ProviderWebhookEvent> ParseWebhook(string payload);
}

public sealed record ProviderInitRequest(
    Guid IntentId,
    string IdempotencyKey,
    decimal AmountTND,
    string? ReturnUrl,
    string? CustomerEmail,
    string? CustomerName,
    string? Description);

public sealed record ProviderInitResult(string ProviderRef, string RedirectUrl, string RawPayload);

public sealed record ProviderWebhookEvent(
    string EventId,
    string ProviderRef,
    ProviderWebhookOutcome Outcome,
    string? FailureReason,
    string? RawPayloadEcho);

public enum ProviderWebhookOutcome
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2,
    Cancelled = 3,
    Refunded = 4,
    Unknown = 99
}
