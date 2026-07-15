using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Represents a tenant's subscription to the platform.
/// </summary>
public sealed class Subscription : AggregateRoot
{
    public Guid TenantId { get; private set; }
    public SubscriptionPlan Plan { get; private set; }
    public SubscriptionStatus Status { get; private set; }

    /// <summary>
    /// Lot C1 — FK optionnelle vers le <see cref="FactuTrust.Domain.Billing.Plan"/> en BD.
    /// Coexiste avec l'enum <see cref="Plan"/> pour rétro-compatibilité totale :
    /// si <c>PlanId</c> est null, <c>IPlanResolver</c> retombe sur l'enum statique
    /// <c>SubscriptionLimits</c>. Le seed initial peuple ce champ pour les 3 plans connus.
    /// </summary>
    public Guid? PlanId { get; private set; }
    
    public DateTime StartDate { get; private set; }
    public DateTime? EndDate { get; private set; }
    public DateTime? TrialEndDate { get; private set; }
    
    public Money? MonthlyPrice { get; private set; }
    public Money? AnnualPrice { get; private set; }
    
    public int InvoicesThisMonth { get; private set; }
    public DateTime CurrentPeriodStart { get; private set; }
    
    public DateTime? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }

    private Subscription() { }

    public static Subscription CreateFree(Guid tenantId)
    {
        return new Subscription
        {
            TenantId = tenantId,
            Plan = SubscriptionPlan.Free,
            Status = SubscriptionStatus.Active,
            StartDate = DateTime.UtcNow,
            CurrentPeriodStart = DateTime.UtcNow,
            InvoicesThisMonth = 0
        };
    }

    public static Subscription CreateTrial(Guid tenantId, int trialDays = 14)
    {
        return new Subscription
        {
            TenantId = tenantId,
            Plan = SubscriptionPlan.Monthly,
            Status = SubscriptionStatus.Trial,
            StartDate = DateTime.UtcNow,
            TrialEndDate = DateTime.UtcNow.AddDays(trialDays),
            CurrentPeriodStart = DateTime.UtcNow,
            InvoicesThisMonth = 0
        };
    }

    public Result UpgradeTo(SubscriptionPlan newPlan, Money price)
    {
        if (newPlan == SubscriptionPlan.Free)
            return Result.Failure(Error.Validation("Plan", "Utilisez Downgrade pour passer au plan gratuit"));

        if (newPlan == Plan && Status == SubscriptionStatus.Active)
            return Result.Failure(Error.Validation("Plan", "Vous êtes déjà sur ce plan"));

        Plan = newPlan;
        Status = SubscriptionStatus.Active;
        StartDate = DateTime.UtcNow;
        EndDate = newPlan == SubscriptionPlan.Annual 
            ? DateTime.UtcNow.AddYears(1) 
            : DateTime.UtcNow.AddMonths(1);
        TrialEndDate = null;

        if (newPlan == SubscriptionPlan.Monthly)
            MonthlyPrice = price;
        else
            AnnualPrice = price;

        return Result.Success();
    }

    public Result DowngradeToFree()
    {
        if (Plan == SubscriptionPlan.Free)
            return Result.Failure(Error.Validation("Plan", "Vous êtes déjà sur le plan gratuit"));

        Plan = SubscriptionPlan.Free;
        Status = SubscriptionStatus.Active;
        EndDate = null;
        MonthlyPrice = null;
        AnnualPrice = null;

        return Result.Success();
    }

    public Result Cancel(string reason)
    {
        if (Status == SubscriptionStatus.Cancelled)
            return Result.Failure(Error.Validation("Status", "L'abonnement est déjà annulé"));

        CancelledAt = DateTime.UtcNow;
        CancellationReason = reason?.Trim();
        Status = SubscriptionStatus.Cancelled;

        return Result.Success();
    }

    public Result Renew()
    {
        if (Plan == SubscriptionPlan.Free)
            return Result.Failure(Error.Validation("Plan", "Le plan gratuit ne nécessite pas de renouvellement"));

        if (Status != SubscriptionStatus.Active && Status != SubscriptionStatus.PastDue)
            return Result.Failure(Error.Validation("Status", "Impossible de renouveler dans cet état"));

        StartDate = DateTime.UtcNow;
        EndDate = Plan == SubscriptionPlan.Annual 
            ? DateTime.UtcNow.AddYears(1) 
            : DateTime.UtcNow.AddMonths(1);
        Status = SubscriptionStatus.Active;

        return Result.Success();
    }

    /// <summary>
    /// Reactivates a paid subscription after payment regularization (Suspended or PastDue).
    /// </summary>
    public Result Reactivate()
    {
        if (Plan == SubscriptionPlan.Free)
            return Result.Failure(Error.Validation("Plan", "Le plan gratuit ne nécessite pas de réactivation"));

        if (Status is not (SubscriptionStatus.Suspended or SubscriptionStatus.PastDue))
            return Result.Failure(Error.Validation("Status", "L'abonnement n'est pas en attente de réactivation"));

        Status = SubscriptionStatus.Active;
        return Result.Success();
    }

    public bool CanCreateInvoice()
    {
        if (Status is SubscriptionStatus.Suspended or SubscriptionStatus.Cancelled)
            return false;

        var limit = SubscriptionLimits.GetMaxInvoicesPerMonth(Plan);
        return InvoicesThisMonth < limit;
    }

    public void IncrementInvoiceCount()
    {
        InvoicesThisMonth++;
    }

    public void ResetMonthlyCounter()
    {
        var now = DateTime.UtcNow;
        if (CurrentPeriodStart.Month != now.Month || CurrentPeriodStart.Year != now.Year)
        {
            InvoicesThisMonth = 0;
            CurrentPeriodStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        }
    }

    public bool HasFeature(string featureName)
    {
        return SubscriptionLimits.HasFeature(Plan, featureName);
    }

    /// <summary>Lot C1 — Lie l'abonnement à un Plan configurable (BD). Garde l'enum aligné.</summary>
    public void AttachToPlan(Guid planId, SubscriptionPlan enumPlan)
    {
        if (planId == Guid.Empty) throw new ArgumentException("PlanId requis", nameof(planId));
        PlanId = planId;
        Plan = enumPlan;
    }

    /// <summary>Lot C1 — Détache l'abonnement de tout plan BD (retour au mode enum-only).</summary>
    public void DetachPlan() => PlanId = null;

    /// <summary>Lot C6 — Passe l'abonnement en PastDue (impayé en grace period).</summary>
    public Result MarkPastDue()
    {
        if (Status is SubscriptionStatus.Cancelled or SubscriptionStatus.Suspended)
            return Result.Failure(Error.Validation("Status", "Impossible de marquer en impayé dans cet état."));
        Status = SubscriptionStatus.PastDue;
        return Result.Success();
    }

    /// <summary>Lot C6 — Suspend l'abonnement (déclenché par le DunningExecutorJob après J+14).</summary>
    public Result Suspend(string reason)
    {
        if (Status is SubscriptionStatus.Cancelled)
            return Result.Failure(Error.Validation("Status", "L'abonnement est annulé, suspension impossible."));
        Status = SubscriptionStatus.Suspended;
        CancellationReason = (reason ?? string.Empty).Trim();
        return Result.Success();
    }

    public void CheckExpiration()
    {
        if (EndDate.HasValue && EndDate.Value < DateTime.UtcNow && Status == SubscriptionStatus.Active)
        {
            Status = SubscriptionStatus.Expired;
        }

        if (TrialEndDate.HasValue && TrialEndDate.Value < DateTime.UtcNow && Status == SubscriptionStatus.Trial)
        {
            Status = SubscriptionStatus.Expired;
            Plan = SubscriptionPlan.Free;
        }
    }
}
