using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Services.Payroll;

/// <summary>
/// Calcul des soldes de congés payés : acquis, consommés, restants.
/// Règle d'acquisition : 1 jour ouvrable par 26 jours travaillés.
/// </summary>
public static class LeaveBalanceService
{
    public const decimal MonthlyWorkingDays = OvertimeAmountCalculator.MonthlyWorkingDays;

    public static decimal ComputeWorkedDays(decimal unpaidAbsenceDays) =>
        Math.Max(0m, Math.Round(MonthlyWorkingDays - unpaidAbsenceDays, 2, MidpointRounding.AwayFromZero));

    public static decimal ComputeAccruedDays(decimal workedDays)
    {
        if (workedDays <= 0)
            return 0m;

        return Math.Round(workedDays / MonthlyWorkingDays, 3, MidpointRounding.AwayFromZero);
    }

    public static decimal SumAccruedDays(IEnumerable<decimal> monthlyAccruals) =>
        Math.Round(monthlyAccruals.Sum(), 3, MidpointRounding.AwayFromZero);

    public static decimal SumConsumedPaidLeaveDays(
        IEnumerable<(LeaveType Type, bool IsApproved, decimal Days, int StartYear)> leaves,
        int fiscalYear)
    {
        var consumed = leaves
            .Where(l => l.Type == LeaveType.Paid && l.IsApproved && l.StartYear == fiscalYear)
            .Sum(l => l.Days);

        return Math.Round(consumed, 2, MidpointRounding.AwayFromZero);
    }

    public static decimal ComputeRemaining(
        decimal openingBalance,
        decimal accruedInYear,
        decimal consumedInYear) =>
        Math.Round(openingBalance + accruedInYear - consumedInYear, 3, MidpointRounding.AwayFromZero);

    public static decimal ComputeTotalAcquired(decimal openingBalance, decimal accruedInYear) =>
        Math.Round(openingBalance + accruedInYear, 3, MidpointRounding.AwayFromZero);
}
