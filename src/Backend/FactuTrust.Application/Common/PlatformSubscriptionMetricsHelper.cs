using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common;

/// <summary>
/// Calculs partagés des métriques d'abonnements pour le backoffice plateforme :
/// MRR (Monthly Recurring Revenue) par ligne, par parc, et indicateurs de risque.
/// </summary>
public static class PlatformSubscriptionMetricsHelper
{
    /// <summary>Prix mensuel par défaut (TND) si Subscription.MonthlyPrice est null.</summary>
    public const decimal DefaultMonthlyPriceTnd = 49m;

    /// <summary>Prix annuel par défaut (TND) si Subscription.AnnualPrice est null.</summary>
    public const decimal DefaultAnnualPriceTnd = 468m;

    /// <summary>Conversion d'un abonnement annuel en MRR (par mois).</summary>
    public const decimal MonthsPerYear = 12m;

    /// <summary>
    /// Indique si l'abonnement contribue actuellement au MRR :
    /// plan Monthly ou Annual + statut Active ou Trial.
    /// </summary>
    public static bool ContributesToMrr(SubscriptionPlan? plan, SubscriptionStatus? status)
        => PlatformSubscriptionSegmentHelper.IsPayingSubscriber(plan, status);

    /// <summary>
    /// MRR estimé d'une ligne d'abonnement, en TND :
    ///  - Monthly Active/Trial → MonthlyPrice (ou 49 par défaut)
    ///  - Annual Active/Trial  → AnnualPrice / 12 (ou 39 par défaut)
    ///  - Sinon                → null
    /// </summary>
    public static decimal? ComputeRowMrrTnd(
        SubscriptionPlan? plan,
        SubscriptionStatus? status,
        decimal? monthlyPriceTnd,
        decimal? annualPriceTnd)
    {
        if (!ContributesToMrr(plan, status))
            return null;

        return plan switch
        {
            SubscriptionPlan.Monthly => monthlyPriceTnd ?? DefaultMonthlyPriceTnd,
            SubscriptionPlan.Annual => (annualPriceTnd ?? DefaultAnnualPriceTnd) / MonthsPerYear,
            _ => null
        };
    }

    /// <summary>
    /// Indique si l'abonnement est "en risque" (impayé ou suspendu).
    /// Utilisé pour le KPI "Tenants en risque" du backoffice.
    /// </summary>
    public static bool IsAtRisk(SubscriptionStatus? status)
        => status is SubscriptionStatus.PastDue or SubscriptionStatus.Suspended;
}
