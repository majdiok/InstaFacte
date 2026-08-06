namespace FactuTrust.Domain.Services.Payroll;

/// <summary>
/// Calcule le prorata embauche / départ / suspension sur le salaire de base (convention 26 jours).
/// </summary>
public static class PayrollProrataCalculator
{
    public static PayrollProrataMonthResult Compute(PayrollProrataMonthInput input)
    {
        if (!input.IsEnabled || input.BaseSalary <= 0)
            return PayrollProrataMonthResult.Empty;

        var year = input.Year;
        var month = input.Month;
        var monthStart = new DateTime(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var fullMonthWorkdays = PayrollWorkingDaysCounter.CountWeekdaysInMonth(year, month);
        if (fullMonthWorkdays <= 0)
            return PayrollProrataMonthResult.Empty;

        var calendarWorkedDays = PayrollWorkingDaysCounter.CountInMonth(
            year, month, input.EffectiveStart, input.EffectiveEnd);

        var payrollWorkedDays = Math.Round(
            MonthlyWorkingDays * calendarWorkedDays / fullMonthWorkdays,
            2,
            MidpointRounding.AwayFromZero);
        payrollWorkedDays = Math.Clamp(payrollWorkedDays, 0m, MonthlyWorkingDays);

        var warnings = new List<string>();
        var reasons = new List<PayrollProrataReason>();

        var presenceStart = input.EffectiveStart.Date > monthStart ? input.EffectiveStart.Date : monthStart;
        var presenceEnd = input.EffectiveEnd.Date < monthEnd ? input.EffectiveEnd.Date : monthEnd;

        if (presenceStart > monthStart)
            reasons.Add(PayrollProrataReason.Hire);
        if (presenceEnd < monthEnd)
            reasons.Add(PayrollProrataReason.Departure);

        var suspendedWeekdays = CountUnpaidSuspensionWeekdays(
            input.Suspensions, year, month, presenceStart, presenceEnd);

        if (suspendedWeekdays > 0)
        {
            reasons.Add(PayrollProrataReason.Suspension);
            payrollWorkedDays = Math.Max(0m, payrollWorkedDays - suspendedWeekdays);
        }

        var nonWorkedDays = Math.Round(MonthlyWorkingDays - payrollWorkedDays, 2, MidpointRounding.AwayFromZero);
        nonWorkedDays = Math.Clamp(nonWorkedDays, 0m, MonthlyWorkingDays);

        if (nonWorkedDays <= 0)
            return PayrollProrataMonthResult.Empty;

        var deduction = Math.Round(
            input.BaseSalary / MonthlyWorkingDays * nonWorkedDays,
            3,
            MidpointRounding.AwayFromZero);

        var reason = ResolveReason(reasons);

        return new PayrollProrataMonthResult
        {
            WorkedDays = payrollWorkedDays,
            NonWorkedDays = nonWorkedDays,
            DeductionAmount = deduction,
            Reason = reason,
            Warnings = warnings
        };
    }

    private static decimal MonthlyWorkingDays => PayrollWorkingDaysCounter.MonthlyWorkingDays;

    private static int CountUnpaidSuspensionWeekdays(
        IReadOnlyList<PayrollProrataSuspensionPeriod> suspensions,
        int year,
        int month,
        DateTime presenceStart,
        DateTime presenceEnd)
    {
        if (suspensions.Count == 0 || presenceEnd < presenceStart)
            return 0;

        var monthStart = new DateTime(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var coveredDays = new HashSet<DateTime>();

        foreach (var suspension in suspensions)
        {
            if (!suspension.IsApproved || suspension.IsPaid)
                continue;

            var suspensionEnd = suspension.EndDate?.Date ?? monthEnd;
            var overlapStart = MaxDate(suspension.StartDate.Date, presenceStart, monthStart);
            var overlapEnd = MinDate(suspensionEnd, presenceEnd, monthEnd);

            if (overlapEnd < overlapStart)
                continue;

            for (var d = overlapStart; d <= overlapEnd; d = d.AddDays(1))
            {
                if (d.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                    coveredDays.Add(d);
            }
        }

        return coveredDays.Count;
    }

    private static PayrollProrataReason ResolveReason(IReadOnlyList<PayrollProrataReason> reasons)
    {
        if (reasons.Count == 0)
            return PayrollProrataReason.None;

        var distinct = reasons.Distinct().Where(r => r != PayrollProrataReason.None).ToList();
        return distinct.Count switch
        {
            0 => PayrollProrataReason.None,
            1 => distinct[0],
            _ => PayrollProrataReason.Combined
        };
    }

    private static DateTime MaxDate(DateTime a, DateTime b, DateTime c) =>
        new[] { a, b, c }.Max();

    private static DateTime MinDate(DateTime a, DateTime b, DateTime c) =>
        new[] { a, b, c }.Min();
}
