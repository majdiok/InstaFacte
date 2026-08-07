using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Services.Payroll;

/// <summary>
/// Indemnité légale de licenciement — art. 22bis du Code du travail tunisien :
/// 1 jour de salaire par mois d'ancienneté, plafonnée à 3 mois de salaire brut.
/// </summary>
public static class TerminationIndemnityCalculator
{
    public const decimal MonthlyWorkingDays = 26m;
    public const decimal MaxMonthsCap = 3m;

    public sealed record Input(
        DateTime SeniorityStartDate,
        DateTime TerminationDate,
        decimal MonthlyGrossSalary,
        TerminationReason Reason);

    public sealed record Result(
        int SeniorityMonths,
        int IndemnityDays,
        decimal DailyRate,
        decimal RawAmount,
        decimal CappedAmount,
        decimal AppliedAmount);

    public static Result Compute(Input input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!input.Reason.IsEligibleForLegalIndemnity())
        {
            return new Result(0, 0, 0m, 0m, 0m, 0m);
        }

        var seniorityMonths = CountFullMonths(input.SeniorityStartDate.Date, input.TerminationDate.Date);
        var indemnityDays = Math.Max(0, seniorityMonths);
        var dailyRate = input.MonthlyGrossSalary <= 0m
            ? 0m
            : R(input.MonthlyGrossSalary / MonthlyWorkingDays);
        var rawAmount = R(indemnityDays * dailyRate);
        var cap = R(input.MonthlyGrossSalary * MaxMonthsCap);
        var cappedAmount = cap > 0m ? Math.Min(rawAmount, cap) : rawAmount;

        return new Result(seniorityMonths, indemnityDays, dailyRate, rawAmount, cappedAmount, cappedAmount);
    }

    /// <summary>Compte les mois complets d'ancienneté (jour du mois ignoré).</summary>
    public static int CountFullMonths(DateTime start, DateTime end)
    {
        if (end < start)
            return 0;

        var months = (end.Year - start.Year) * 12 + end.Month - start.Month;
        if (end.Day < start.Day)
            months--;
        return Math.Max(0, months);
    }

    private static decimal R(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}
