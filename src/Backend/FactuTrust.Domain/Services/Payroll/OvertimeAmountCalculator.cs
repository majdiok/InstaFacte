using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Services.Payroll;

/// <summary>
/// Calcule le montant des heures supplémentaires à partir du salaire de base mensuel.
/// hourlyRate = baseSalary / diviseur du régime ; amount = hourlyRate × hours × (ratePercent / 100).
/// Régime 48 h : diviseur 208 (26 j × 8 h) ; régime 40 h : diviseur 520/3 (40 × 52 ÷ 12, exact).
///
/// <para>
/// R-37 (CAL-018) : seul le montant final est arrondi (3 décimales, AwayFromZero) — le taux
/// horaire intermédiaire n'est plus pré-arrondi avant d'être multiplié par les heures et le taux
/// de majoration, ce qui évite une dérive de millimes sur les gros volumes d'heures.
/// <see cref="ComputeHourlyRate"/> reste arrondi pour l'affichage bulletin (usage externe), mais
/// <see cref="ComputeAmount"/> utilise désormais le taux horaire non arrondi en interne.
/// </para>
/// </summary>
public static class OvertimeAmountCalculator
{
    /// <summary>Base mensuelle de jours ouvrables (convention tunisienne).</summary>
    public const decimal MonthlyWorkingDays = 26m;

    /// <summary>Heures journalières standard pour dériver le taux horaire.</summary>
    public const decimal StandardDailyHours = 8m;

    /// <summary>Diviseur mensuel du régime 48 h/semaine (26 × 8).</summary>
    public const decimal DivisorH48 = 208m;

    /// <summary>
    /// Diviseur mensuel du régime 40 h/semaine (40 × 52 ÷ 12), exact — 173,333... et non 173,33
    /// (R-37/CAL-018 : l'ancienne valeur tronquée à 2 décimales introduisait un biais systématique
    /// d'environ 2/100 000 sur le taux horaire).
    /// </summary>
    public const decimal DivisorH40 = 520m / 3m;

    /// <summary>Taux horaire arrondi (3 décimales) — pour affichage bulletin uniquement.</summary>
    public static decimal ComputeHourlyRate(decimal baseSalary, WeeklyWorkRegime? regime = null) =>
        Round(ComputeRawHourlyRate(baseSalary, regime));

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

        // R-37 : taux horaire non arrondi utilisé pour le calcul — seul le résultat final est
        // arrondi (voir remarque de classe).
        var rawHourlyRate = ComputeRawHourlyRate(baseSalary, regime);
        return Round(rawHourlyRate * hours * (ratePercent / 100m));
    }

    public static decimal ResolveEffectiveAmount(decimal computedAmount, decimal? overrideAmount) =>
        overrideAmount.HasValue && overrideAmount.Value > 0
            ? Round(overrideAmount.Value)
            : computedAmount;

    private static decimal ComputeRawHourlyRate(decimal baseSalary, WeeklyWorkRegime? regime) =>
        baseSalary <= 0 ? 0m : baseSalary / ResolveDivisor(regime);

    private static decimal ResolveDivisor(WeeklyWorkRegime? regime) => regime switch
    {
        WeeklyWorkRegime.FortyHours => DivisorH40,
        _ => DivisorH48
    };

    private static decimal Round(decimal value) =>
        Math.Round(value, 3, MidpointRounding.AwayFromZero);
}
