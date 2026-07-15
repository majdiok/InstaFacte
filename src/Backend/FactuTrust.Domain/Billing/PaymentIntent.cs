using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Billing;

/// <summary>Statut d'une intention de paiement.</summary>
public enum PaymentIntentStatus
{
    /// <summary>Créée localement, pas encore envoyée au provider.</summary>
    Created = 0,
    /// <summary>Provider a renvoyé une URL de redirection — utilisateur n'a pas encore payé.</summary>
    RedirectIssued = 1,
    /// <summary>Provider en attente de confirmation (état intermédiaire).</summary>
    Pending = 2,
    /// <summary>Paiement réussi et webhook validé.</summary>
    Succeeded = 3,
    /// <summary>Paiement refusé / échec.</summary>
    Failed = 4,
    /// <summary>Annulée par l'utilisateur ou expirée.</summary>
    Cancelled = 5,
    /// <summary>Remboursée ultérieurement.</summary>
    Refunded = 6
}

/// <summary>
/// Lot C5 — Intention de paiement adossée à une facture plateforme.
///
/// Une <c>PaymentIntent</c> est créée à chaque clic « Payer ». Elle stocke la clé d'idempotence,
/// l'URL de retour, la référence du provider et son payload brut.
///
/// Cycle : <c>Created → RedirectIssued → (Pending) → Succeeded | Failed | Cancelled</c>.
/// </summary>
public sealed class PaymentIntent : Entity
{
    public Guid TenantId { get; private set; }
    public Guid InvoiceId { get; private set; }
    public string ProviderCode { get; private set; } = null!;
    public string? ProviderRef { get; private set; }
    public decimal AmountTND { get; private set; }
    public PaymentIntentStatus Status { get; private set; }
    public string IdempotencyKey { get; private set; } = null!;
    public string? ReturnUrl { get; private set; }
    public string? RedirectUrl { get; private set; }
    public string? RawProviderPayload { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? FailureReason { get; private set; }
    public Guid CreatedByUserId { get; private set; }

    private PaymentIntent() { }

    public static PaymentIntent Create(
        Guid tenantId,
        Guid invoiceId,
        string providerCode,
        decimal amountTND,
        string idempotencyKey,
        Guid createdByUserId,
        string? returnUrl)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId requis", nameof(tenantId));
        if (invoiceId == Guid.Empty) throw new ArgumentException("InvoiceId requis", nameof(invoiceId));
        if (!PaymentProviderCodes.IsKnown(providerCode)) throw new ArgumentException("Provider inconnu", nameof(providerCode));
        if (amountTND <= 0) throw new ArgumentOutOfRangeException(nameof(amountTND));
        if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new ArgumentException("IdempotencyKey requis", nameof(idempotencyKey));

        return new PaymentIntent
        {
            TenantId = tenantId,
            InvoiceId = invoiceId,
            ProviderCode = providerCode.ToLowerInvariant(),
            AmountTND = Math.Round(amountTND, 3),
            Status = PaymentIntentStatus.Created,
            IdempotencyKey = idempotencyKey.Trim(),
            ReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? null : returnUrl.Trim(),
            CreatedByUserId = createdByUserId
        };
    }

    public void AttachProviderRedirect(string providerRef, string redirectUrl, string? rawPayload)
    {
        ProviderRef = providerRef;
        RedirectUrl = redirectUrl;
        RawProviderPayload = rawPayload;
        Status = PaymentIntentStatus.RedirectIssued;
    }

    public void MarkPending(string? rawPayload)
    {
        if (Status is PaymentIntentStatus.Succeeded or PaymentIntentStatus.Refunded) return;
        Status = PaymentIntentStatus.Pending;
        if (!string.IsNullOrWhiteSpace(rawPayload)) RawProviderPayload = rawPayload;
    }

    public void MarkSucceeded(string? rawPayload)
    {
        Status = PaymentIntentStatus.Succeeded;
        CompletedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(rawPayload)) RawProviderPayload = rawPayload;
    }

    public void MarkFailed(string reason, string? rawPayload)
    {
        Status = PaymentIntentStatus.Failed;
        CompletedAt = DateTime.UtcNow;
        FailureReason = (reason ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(rawPayload)) RawProviderPayload = rawPayload;
    }

    public void MarkCancelled(string? rawPayload)
    {
        Status = PaymentIntentStatus.Cancelled;
        CompletedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(rawPayload)) RawProviderPayload = rawPayload;
    }

    public void MarkRefunded(string? rawPayload)
    {
        Status = PaymentIntentStatus.Refunded;
        CompletedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(rawPayload)) RawProviderPayload = rawPayload;
    }
}

/// <summary>
/// Lot C5 — Trace d'un événement webhook reçu par FactuTrust.
///
/// Stocké même quand l'événement est rejeté (signature invalide, doublon) afin
/// de permettre l'audit. Index unique sur <c>(ProviderCode, ProviderEventId)</c>
/// pour l'idempotence.
/// </summary>
public sealed class PaymentWebhookEvent : Entity
{
    public string ProviderCode { get; private set; } = null!;
    public string ProviderEventId { get; private set; } = null!;
    public DateTime ReceivedAt { get; private set; }
    public string Payload { get; private set; } = null!;
    public bool SignatureValid { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public string? ProcessingError { get; private set; }
    public Guid? IntentId { get; private set; }

    private PaymentWebhookEvent() { }

    public static PaymentWebhookEvent Create(
        string providerCode,
        string providerEventId,
        string payload,
        bool signatureValid)
    {
        if (string.IsNullOrWhiteSpace(providerCode)) throw new ArgumentException("Provider requis", nameof(providerCode));
        if (string.IsNullOrWhiteSpace(providerEventId)) throw new ArgumentException("EventId requis", nameof(providerEventId));
        return new PaymentWebhookEvent
        {
            ProviderCode = providerCode.ToLowerInvariant(),
            ProviderEventId = providerEventId.Trim(),
            ReceivedAt = DateTime.UtcNow,
            Payload = payload ?? string.Empty,
            SignatureValid = signatureValid
        };
    }

    public void MarkProcessed(Guid? intentId)
    {
        ProcessedAt = DateTime.UtcNow;
        ProcessingError = null;
        IntentId = intentId;
    }

    public void MarkFailedProcessing(string error)
    {
        ProcessedAt = DateTime.UtcNow;
        ProcessingError = (error ?? string.Empty).Trim();
    }
}
