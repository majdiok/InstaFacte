using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Services.Payroll;

/// <summary>
/// Calcule le montant des heures supplémentaires à partir du salaire de base mensuel.
/// hourlyRate = baseSalary / (26 × 8) ; amount = hourlyRate × hours × (ratePercent / 100).
/// </summary>
public static class OvertimeAmountCalculator
{
    /// <summary>Base mensuelle de jours ouvrables (convention tunisienne).</summary>
    public const decimal MonthlyWorkingDays = 26m;

    /// <summary>Heures journalières standard pour dériver le taux horaire.</summary>
    public const decimal StandardDailyHours = 8m;

    public static decimal ComputeHourlyRate(decimal baseSalary)
    {
        if (baseSalary <= 0)
            return 0m;

        return Round(baseSalary / (MonthlyWorkingDays * StandardDailyHours));
    }

    public static decimal ComputeAmount(decimal baseSalary, decimal hours, decimal ratePercent, bool enableExtendedOvertimeRates = false)
    {
        if (hours <= 0 || baseSalary <= 0)
            return 0m;

        if (!OvertimeRatePercentExtensions.IsValid(ratePercent, enableExtendedOvertimeRates))
            throw new ArgumentOutOfRangeException(nameof(ratePercent), "Le taux de majoration doit être 125 ou 150 (175 ou 200 si activé).");

        var hourlyRate = ComputeHourlyRate(baseSalary);
        return Round(hourlyRate * hours * (ratePercent / 100m));
    }

    public static decimal ResolveEffectiveAmount(decimal computedAmount, decimal? overrideAmount) =>
        overrideAmount.HasValue && overrideAmount.Value > 0
            ? Round(overrideAmount.Value)
            : computedAmount;

    private static decimal Round(decimal value) =>
        Math.Round(value, 3, MidpointRounding.AwayFromZero);
}
