using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Services;

/// <summary>
/// Prorata calendaire sur une période de facturation (arrondi 3 décimales, AwayFromZero).
/// </summary>
public static class ProrationCalculator
{
    public static decimal ProrateAmount(
        decimal fullPeriodAmount,
        DateTime periodFrom,
        DateTime periodTo,
        DateTime effectiveFrom,
        DateTime? effectiveTo = null)
    {
        if (fullPeriodAmount == 0m)
            return 0m;

        periodFrom = periodFrom.Date;
        periodTo = periodTo.Date;
        effectiveFrom = effectiveFrom.Date;
        effectiveTo = effectiveTo?.Date;

        var totalDays = (periodTo - periodFrom).Days + 1;
        if (totalDays <= 0)
            return 0m;

        var start = effectiveFrom > periodFrom ? effectiveFrom : periodFrom;
        var end = effectiveTo.HasValue && effectiveTo.Value < periodTo ? effectiveTo.Value : periodTo;
        if (end < start)
            return 0m;

        var activeDays = (end - start).Days + 1;
        var ratio = (decimal)activeDays / totalDays;
        return decimal.Round(fullPeriodAmount * ratio, 3, MidpointRounding.AwayFromZero);
    }

    public static (DateTime PeriodFrom, DateTime PeriodTo) ResolveBillingPeriod(
        DateTime nextBillingDate,
        BillingFrequency frequency)
    {
        var periodFrom = nextBillingDate.Date;
        var periodTo = frequency switch
        {
            BillingFrequency.Monthly => periodFrom.AddMonths(1).AddDays(-1),
            BillingFrequency.Quarterly => periodFrom.AddMonths(3).AddDays(-1),
            BillingFrequency.Annual => periodFrom.AddYears(1).AddDays(-1),
            _ => periodFrom.AddMonths(1).AddDays(-1)
        };
        return (periodFrom, periodTo);
    }

    public static DateTime ComputeNextBillingDate(
        DateTime currentBillingDate,
        BillingFrequency frequency,
        int billingDayOfMonth)
    {
        var baseDate = currentBillingDate.Date;
        return frequency switch
        {
            BillingFrequency.Monthly => AdvanceMonthly(baseDate, billingDayOfMonth, 1),
            BillingFrequency.Quarterly => AdvanceMonthly(baseDate, billingDayOfMonth, 3),
            BillingFrequency.Annual => AdvanceMonthly(baseDate, billingDayOfMonth, 12),
            _ => AdvanceMonthly(baseDate, billingDayOfMonth, 1)
        };
    }

    public static DateTime ClampBillingDay(int year, int month, int billingDayOfMonth)
    {
        var day = Math.Clamp(billingDayOfMonth, 1, 31);
        var daysInMonth = DateTime.DaysInMonth(year, month);
        return new DateTime(year, month, Math.Min(day, daysInMonth), 0, 0, 0, DateTimeKind.Utc);
    }

    private static DateTime AdvanceMonthly(DateTime from, int billingDayOfMonth, int months)
    {
        var target = from.AddMonths(months);
        return ClampBillingDay(target.Year, target.Month, billingDayOfMonth);
    }
}
