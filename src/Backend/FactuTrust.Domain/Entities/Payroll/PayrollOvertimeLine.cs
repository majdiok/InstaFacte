using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;

namespace FactuTrust.Domain.Entities.Payroll;

/// <summary>
/// Ligne d'heures supplémentaires saisie pour un salarié sur un mois de paie.
/// </summary>
public sealed class PayrollOvertimeLine : AggregateRoot
{
    public Guid EmployeeId { get; private set; }
    public int Year { get; private set; }
    public int Month { get; private set; }
    public decimal Hours { get; private set; }
    public decimal RatePercent { get; private set; }
    public decimal ComputedAmount { get; private set; }
    public decimal? OverrideAmount { get; private set; }
    public bool IsOverridden { get; private set; }

    private PayrollOvertimeLine() { }

    public decimal EffectiveAmount =>
        OvertimeAmountCalculator.ResolveEffectiveAmount(ComputedAmount, OverrideAmount);

    public static Result<PayrollOvertimeLine> Create(
        Guid employeeId,
        int year,
        int month,
        decimal hours,
        decimal ratePercent,
        decimal baseSalary,
        decimal? overrideAmount = null,
        bool enableExtendedOvertimeRates = false,
        WeeklyWorkRegime? weeklyRegime = null)
    {
        if (employeeId == Guid.Empty)
            return Result.Failure<PayrollOvertimeLine>(Error.Validation("EmployeeId", "Le salarié est obligatoire."));
        if (year < 2000 || year > 2100)
            return Result.Failure<PayrollOvertimeLine>(Error.Validation("Year", "Année invalide."));
        if (month is < 1 or > 12)
            return Result.Failure<PayrollOvertimeLine>(Error.Validation("Month", "Mois invalide."));
        if (hours <= 0)
            return Result.Failure<PayrollOvertimeLine>(Error.Validation("Hours", "Le nombre d'heures doit être strictement positif."));
        if (!IsRateAllowed(ratePercent, enableExtendedOvertimeRates, weeklyRegime))
            return Result.Failure<PayrollOvertimeLine>(Error.Validation("RatePercent", "Le taux de majoration n'est pas autorisé."));

        if (overrideAmount.HasValue && overrideAmount.Value <= 0)
            return Result.Failure<PayrollOvertimeLine>(Error.Validation("OverrideAmount", "Le montant de substitution doit être strictement positif."));

        var computed = OvertimeAmountCalculator.ComputeAmount(baseSalary, hours, ratePercent, enableExtendedOvertimeRates, weeklyRegime);
        var isOverridden = overrideAmount.HasValue && overrideAmount.Value > 0;

        return Result.Success(new PayrollOvertimeLine
        {
            EmployeeId = employeeId,
            Year = year,
            Month = month,
            Hours = Math.Round(hours, 2),
            RatePercent = ratePercent,
            ComputedAmount = computed,
            OverrideAmount = isOverridden ? Math.Round(overrideAmount!.Value, 3) : null,
            IsOverridden = isOverridden
        });
    }

    public Result Update(decimal hours, decimal ratePercent, decimal baseSalary, decimal? overrideAmount = null, bool enableExtendedOvertimeRates = false, WeeklyWorkRegime? weeklyRegime = null)
    {
        if (hours <= 0)
            return Result.Failure(Error.Validation("Hours", "Le nombre d'heures doit être strictement positif."));
        if (!IsRateAllowed(ratePercent, enableExtendedOvertimeRates, weeklyRegime))
            return Result.Failure(Error.Validation("RatePercent", "Le taux de majoration n'est pas autorisé."));
        if (overrideAmount.HasValue && overrideAmount.Value <= 0)
            return Result.Failure(Error.Validation("OverrideAmount", "Le montant de substitution doit être strictement positif."));

        Hours = Math.Round(hours, 2);
        RatePercent = ratePercent;
        ComputedAmount = OvertimeAmountCalculator.ComputeAmount(baseSalary, hours, ratePercent, enableExtendedOvertimeRates, weeklyRegime);
        IsOverridden = overrideAmount.HasValue && overrideAmount.Value > 0;
        OverrideAmount = IsOverridden ? Math.Round(overrideAmount!.Value, 3) : null;
        IncrementVersion();
        return Result.Success();
    }

    private static bool IsRateAllowed(decimal ratePercent, bool enableExtendedOvertimeRates, WeeklyWorkRegime? weeklyRegime) =>
        weeklyRegime.HasValue
            ? OvertimeRatePercentExtensions.IsValid(ratePercent, enableExtendedOvertimeRates, weeklyRegime.Value)
            : OvertimeRatePercentExtensions.IsValid(ratePercent, enableExtendedOvertimeRates);
}
