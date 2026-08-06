namespace FactuTrust.Domain.Services.Payroll;

/// <summary>
/// Comptage des jours ouvrables (lun–ven) pour la paie tunisienne.
/// Convention mensuelle : 26 jours travaillés pour un mois complet.
/// </summary>
public static class PayrollWorkingDaysCounter
{
    public const decimal MonthlyWorkingDays = OvertimeAmountCalculator.MonthlyWorkingDays;

    /// <summary>Nombre de jours ouvrables entre deux dates incluses.</summary>
    public static int CountWeekdays(DateTime start, DateTime end)
    {
        if (end.Date < start.Date)
            return 0;

        var days = 0;
        for (var d = start.Date; d <= end.Date; d = d.AddDays(1))
        {
            if (d.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                days++;
        }

        return days;
    }

    /// <summary>Jours ouvrables d'un mois civil complet (lun–ven).</summary>
    public static int CountWeekdaysInMonth(int year, int month)
    {
        var monthStart = new DateTime(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        return CountWeekdays(monthStart, monthEnd);
    }

    /// <summary>
    /// Jours ouvrables dans l'intersection [rangeStart, rangeEnd] ∩ [1er jour du mois, dernier jour du mois].
    /// </summary>
    public static int CountInMonth(int year, int month, DateTime rangeStart, DateTime rangeEnd)
    {
        var monthStart = new DateTime(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var effectiveStart = rangeStart.Date > monthStart ? rangeStart.Date : monthStart;
        var effectiveEnd = rangeEnd.Date < monthEnd ? rangeEnd.Date : monthEnd;

        if (effectiveEnd < effectiveStart)
            return 0;

        return CountWeekdays(effectiveStart, effectiveEnd);
    }
}
