using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Billing;

/// <summary>Issue de la campagne dunning pour un abonnement.</summary>
public enum DunningOutcome
{
    /// <summary>En cours — campagne active sur l'abonnement.</summary>
    Active = 0,
    /// <summary>Tenant a payé — campagne arrêtée avec succès.</summary>
    Paid = 1,
    /// <summary>Suspension exécutée — fin de cycle.</summary>
    Suspended = 2,
    /// <summary>Renouvellement abandonné côté admin.</summary>
    GiveUp = 3
}

/// <summary>
/// Lot C6 — État courant d'un cycle dunning sur un abonnement particulier.
///
/// Créé par <c>DunningExecutorJob</c> dès qu'un abonnement <c>EndDate &lt; UtcNow</c>
/// avec une facture impayée associée. Avance d'une étape par jour jusqu'au statut
/// final (<c>Paid</c>, <c>Suspended</c>, <c>GiveUp</c>).
/// </summary>
public sealed class DunningState : Entity
{
    public Guid SubscriptionId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CampaignId { get; private set; }
    /// <summary>Date d'échéance de la facture impayée (point d'origine).</summary>
    public DateTime DueDate { get; private set; }
    /// <summary>Index de la prochaine étape à exécuter (0-based dans <see cref="DunningCampaign.StepsJson"/>).</summary>
    public int CurrentStepIndex { get; private set; }
    public DateTime NextActionAt { get; private set; }
    public DateTime? LastEmailSentAt { get; private set; }
    public int AttemptsCount { get; private set; }
    public DunningOutcome Outcome { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? LastError { get; private set; }
    /// <summary>Référence vers la facture impayée (utile pour vérifier le paiement et arrêter le cycle).</summary>
    public Guid? RelatedInvoiceId { get; private set; }

    private DunningState() { }

    public static DunningState Start(Guid subscriptionId, Guid tenantId, Guid campaignId, DateTime dueDate, Guid? relatedInvoiceId, DateTime firstActionAt)
    {
        if (subscriptionId == Guid.Empty) throw new ArgumentException("SubscriptionId requis", nameof(subscriptionId));
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId requis", nameof(tenantId));
        if (campaignId == Guid.Empty) throw new ArgumentException("CampaignId requis", nameof(campaignId));
        return new DunningState
        {
            SubscriptionId = subscriptionId,
            TenantId = tenantId,
            CampaignId = campaignId,
            DueDate = DateTime.SpecifyKind(dueDate.Date, DateTimeKind.Utc),
            CurrentStepIndex = 0,
            NextActionAt = firstActionAt,
            AttemptsCount = 0,
            Outcome = DunningOutcome.Active,
            RelatedInvoiceId = relatedInvoiceId
        };
    }

    /// <summary>Avance au prochain step (incrémente l'index et programme l'exécution).</summary>
    public void AdvanceTo(int stepIndex, DateTime nextActionAt)
    {
        CurrentStepIndex = stepIndex;
        NextActionAt = nextActionAt;
        AttemptsCount++;
        LastError = null;
    }

    public void RecordEmailSent() => LastEmailSentAt = DateTime.UtcNow;

    public void RecordError(string error)
    {
        LastError = (error ?? string.Empty).Trim();
        AttemptsCount++;
    }

    public void MarkPaid()
    {
        Outcome = DunningOutcome.Paid;
        CompletedAt = DateTime.UtcNow;
    }

    public void MarkSuspended()
    {
        Outcome = DunningOutcome.Suspended;
        CompletedAt = DateTime.UtcNow;
    }

    public void MarkGiveUp(string reason)
    {
        Outcome = DunningOutcome.GiveUp;
        CompletedAt = DateTime.UtcNow;
        LastError = reason;
    }

    public bool IsCompleted => Outcome != DunningOutcome.Active;
}
