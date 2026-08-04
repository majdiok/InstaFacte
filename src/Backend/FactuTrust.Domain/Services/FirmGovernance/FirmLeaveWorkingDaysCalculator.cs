using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Services.FirmGovernance;

/// <summary>Calcul des jours ouvrés (Lun–Ven) avec demi-journées et jours fériés optionnels.</summary>
public static class FirmLeaveWorkingDaysCalculator
{
    public static decimal ComputeDays(
        DateTime startDate,
        DateTime endDate,
        FirmLeaveDayUnit startUnit,
        FirmLeaveDayUnit endUnit,
        bool allowHalfDays,
        Func<DateTime, bool>? isHoliday = null)
    {
        var start = startDate.Date;
        var end = endDate.Date;
        if (end < start)
            return 0m;

        if (!allowHalfDays)
        {
            startUnit = FirmLeaveDayUnit.FullDay;
            endUnit = FirmLeaveDayUnit.FullDay;
        }

        decimal total = 0m;
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            if (!IsWorkingDay(d, isHoliday))
                continue;

            if (d == start && d == end)
                total += ComputeSameDayUnits(startUnit, endUnit);
            else if (d == start)
                total += startUnit == FirmLeaveDayUnit.Afternoon ? 0.5m : 1m;
            else if (d == end)
                total += endUnit == FirmLeaveDayUnit.Morning ? 0.5m : 1m;
            else
                total += 1m;
        }

        return Math.Round(total, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal ComputeSameDayUnits(FirmLeaveDayUnit startUnit, FirmLeaveDayUnit endUnit)
    {
        if (startUnit == FirmLeaveDayUnit.FullDay || endUnit == FirmLeaveDayUnit.FullDay)
            return 1m;
        if (startUnit == FirmLeaveDayUnit.Morning && endUnit == FirmLeaveDayUnit.Afternoon)
            return 1m;
        if (startUnit == endUnit)
            return 0.5m;
        return 0.5m;
    }

    public static bool IsWorkingDay(DateTime date, Func<DateTime, bool>? isHoliday = null)
    {
        if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            return false;
        if (isHoliday is not null && isHoliday(date.Date))
            return false;
        return true;
    }
}
