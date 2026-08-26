using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Services;

/// <summary>
/// Projection des échéances d'un contrat récurrent. Source unique de vérité,
/// partagée entre le moteur de facturation et les endpoints de prévision.
/// </summary>
public static class RecurringContractScheduleProjector
{
    /// <summary>Date de première facturation : jour clampé du mois de début, sinon mois suivant.</summary>
    public static DateTime ComputeInitialBillingDate(DateTime startDate, int billingDayOfMonth)
    {
        var candidate = ProrationCalculator.ClampBillingDay(startDate.Year, startDate.Month, billingDayOfMonth);
        if (candidate < startDate.Date)
        {
            var next = startDate.AddMonths(1);
            candidate = ProrationCalculator.ClampBillingDay(next.Year, next.Month, billingDayOfMonth);
        }
        return candidate;
    }

    /// <summary>
    /// Projette jusqu'à <paramref name="count"/> occurrences à partir de
    /// <paramref name="nextBillingDate"/> (ou de la date initiale si null).
    /// S'arrête à <paramref name="endDate"/> sauf si <paramref name="autoRenew"/> (reconduction tacite).
    /// </summary>
    public static IReadOnlyList<ProjectedBillingOccurrence> Project(
        DateTime? nextBillingDate,
        DateTime startDate,
        DateTime? endDate,
        bool autoRenew,
        BillingFrequency frequency,
        int billingDayOfMonth,
        int count)
    {
        var occurrences = new List<ProjectedBillingOccurrence>();
        var date = nextBillingDate?.Date ?? ComputeInitialBillingDate(startDate, billingDayOfMonth);
        for (var i = 0; i < count; i++)
        {
            if (endDate.HasValue && !autoRenew && date > endDate.Value.Date)
                break;
            var (periodFrom, periodTo) = ProrationCalculator.ResolveBillingPeriod(date, frequency);
            occurrences.Add(new ProjectedBillingOccurrence(date, periodFrom, periodTo));
            date = ProrationCalculator.ComputeNextBillingDate(date, frequency, billingDayOfMonth);
        }
        return occurrences;
    }
}

public sealed record ProjectedBillingOccurrence(DateTime Date, DateTime PeriodFrom, DateTime PeriodTo);
