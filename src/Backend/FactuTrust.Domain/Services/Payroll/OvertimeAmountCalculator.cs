using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Services.Payroll;

/// <summary>
/// Calcule le montant des heures supplémentaires à partir du salaire de base mensuel.
/// hourlyRate = baseSalary / diviseur du régime ; amount = hourlyRate × hours × (ratePercent / 100).
/// Régime 48 h : diviseur 208 (26 j × 8 h) ; régime 40 h : diviseur 173,33 (40 × 52 ÷ 12).
/// </summary>
public static class OvertimeAmountCalculator
{
    /// <summary>Base mensuelle de jours ouvrables (convention tunisienne).</summary>
    public const decimal MonthlyWorkingDays = 26m;

    /// <summary>Heures journalières standard pour dériver le taux horaire.</summary>
    public const decimal StandardDailyHours = 8m;

    /// <summary>Diviseur mensuel du régime 48 h/semaine (26 × 8).</summary>
    public const decimal DivisorH48 = 208m;

    /// <summary>Diviseur mensuel du régime 40 h/semaine (40 × 52 ÷ 12).</summary>
    public const decimal DivisorH40 = 173.33m;

    public static decimal ComputeHourlyRate(decimal baseSalary, WeeklyWorkRegime? regime = null)
    {
        if (baseSalary <= 0)
            return 0m;

        return Round(baseSalary / (regime ?? WeeklyWorkRegime.FortyEightHours).MonthlyHoursDivisor());
    }

    public static decimal ComputeAmount(
        decimal baseSalary,
        decimal hours,
        decimal ratePercent,
        bool enableExtendedOvertimeRates = false,
        WeeklyWorkRegime? regime = null)
    {
        if (hours <= 0 || baseSalary <= 0)
            return 0m;

        // Régime inconnu : validation historique stricte (125/150, 175/200 sur option).
        // Régime connu : 175 % est en plus le taux légal du régime 48 h.
        var rateAllowed = regime.HasValue
            ? OvertimeRatePercentExtensions.IsValid(ratePercent, enableExtendedOvertimeRates, regime.Value)
            : OvertimeRatePercentExtensions.IsValid(ratePercent, enableExtendedOvertimeRates);
        if (!rateAllowed)
            throw new ArgumentOutOfRangeException(nameof(ratePercent), "Le taux de majoration n'est pas autorisé pour ce régime.");

        var hourlyRate = ComputeHourlyRate(baseSalary, regime);
        return Round(hourlyRate * hours * (ratePercent / 100m));
    }

    public static decimal ResolveEffectiveAmount(decimal computedAmount, decimal? overrideAmount) =>
        overrideAmount.HasValue && overrideAmount.Value > 0
            ? Round(overrideAmount.Value)
            : computedAmount;

    private static decimal Round(decimal value) =>
        Math.Round(value, 3, MidpointRounding.AwayFromZero);
}
