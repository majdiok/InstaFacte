using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Billing;

/// <summary>
/// Lot C3 — Crédit accordé à un tenant (avoir / dédommagement / parrainage).
///
/// Le crédit est consommé en priorité par <c>ExpiresAt</c> ASC lors de la
/// génération d'une facture (<c>ConsumeTenantCreditService</c> — implémenté Lot C4).
///
/// La révocation ne supprime pas la ligne — elle la marque comme inactive
/// (RevokedAt non null) pour audit.
/// </summary>
public sealed class TenantCredit : Entity
{
    public Guid TenantId { get; private set; }
    public decimal AmountTND { get; private set; }
    public string Reason { get; private set; } = null!;
    public Guid GrantedByUserId { get; private set; }
    public DateTime GrantedAt { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public decimal ConsumedAmountTND { get; private set; }
    public Guid? RelatedInvoiceId { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? RevocationReason { get; private set; }

    private TenantCredit() { }

    public static TenantCredit Grant(
        Guid tenantId,
        decimal amountTND,
        string reason,
        Guid grantedByUserId,
        DateTime? expiresAt = null,
        Guid? relatedInvoiceId = null)
    {
        if (amountTND <= 0)
            throw new ArgumentException("Le montant doit être positif.", nameof(amountTND));

        return new TenantCredit
        {
            TenantId = tenantId,
            AmountTND = Math.Round(amountTND, 3),
            Reason = (reason ?? "Crédit").Trim(),
            GrantedByUserId = grantedByUserId,
            GrantedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            ConsumedAmountTND = 0m,
            RelatedInvoiceId = relatedInvoiceId
        };
    }

    /// <summary>Consomme une partie ou la totalité du crédit (appelé lors de génération facture).</summary>
    public decimal Consume(decimal requestedAmountTND, Guid? invoiceId)
    {
        if (RevokedAt is not null) return 0m;
        if (ExpiresAt is not null && ExpiresAt <= DateTime.UtcNow) return 0m;

        var available = AmountTND - ConsumedAmountTND;
        if (available <= 0) return 0m;

        var consumed = Math.Min(requestedAmountTND, available);
        ConsumedAmountTND += consumed;
        if (invoiceId.HasValue && RelatedInvoiceId is null)
            RelatedInvoiceId = invoiceId;
        return consumed;
    }

    public void Revoke(string reason)
    {
        if (RevokedAt is not null) return;
        RevokedAt = DateTime.UtcNow;
        RevocationReason = reason?.Trim();
    }

    /// <summary>Solde restant (consommable).</summary>
    public decimal RemainingTND => AmountTND - ConsumedAmountTND;

    /// <summary>Indique si le crédit est encore utilisable.</summary>
    public bool IsActive
        => RevokedAt is null
            && (ExpiresAt is null || ExpiresAt > DateTime.UtcNow)
            && RemainingTND > 0;
}
