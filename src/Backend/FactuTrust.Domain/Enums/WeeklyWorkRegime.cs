namespace FactuTrust.Domain.Enums;

/// <summary>
/// Régime de durée hebdomadaire de travail du contrat (Code du travail tunisien, art. 79 et s.).
/// Détermine le diviseur mensuel du taux horaire et les taux de majoration légaux des heures
/// supplémentaires : 48 h/semaine → 208 h/mois, majoration légale 75 % ; 40 h/semaine →
/// 173,33 h/mois, majorations 25 % (jusqu'à 48 h) et 50 % (au-delà).
/// </summary>
public enum WeeklyWorkRegime
{
    /// <summary>48 heures par semaine (régime général).</summary>
    FortyEightHours = 0,
    /// <summary>40 heures par semaine.</summary>
    FortyHours = 1
}

public static class WeeklyWorkRegimeExtensions
{
    public static string ToDisplayString(this WeeklyWorkRegime regime) => regime switch
    {
        WeeklyWorkRegime.FortyEightHours => "48 h / semaine",
        WeeklyWorkRegime.FortyHours => "40 h / semaine",
        _ => throw new ArgumentOutOfRangeException(nameof(regime))
    };

    /// <summary>Diviseur mensuel (en heures) pour dériver le taux horaire du salaire de base.</summary>
    public static decimal MonthlyHoursDivisor(this WeeklyWorkRegime regime) => regime switch
    {
        WeeklyWorkRegime.FortyHours => 173.33m,
        _ => 208m
    };

    /// <summary>Taux de majoration HS légal proposé par défaut pour le régime.</summary>
    public static decimal DefaultOvertimeRatePercent(this WeeklyWorkRegime regime) => regime switch
    {
        WeeklyWorkRegime.FortyHours => 125m,
        _ => 175m
    };
}
