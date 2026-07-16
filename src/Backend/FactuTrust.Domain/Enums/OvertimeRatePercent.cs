namespace FactuTrust.Domain.Enums;

/// <summary>
/// Taux de majoration des heures supplémentaires (Tunisie — barèmes courants).
/// </summary>
public enum OvertimeRatePercent
{
    Rate125 = 125,
    Rate150 = 150,
    Rate175 = 175,
    Rate200 = 200
}

public static class OvertimeRatePercentExtensions
{
    public static bool IsValid(decimal rate, bool enableExtendedOvertimeRates = false) =>
        rate is 125m or 150m
        || (enableExtendedOvertimeRates && rate is 175m or 200m);

    /// <summary>
    /// Variante tenant compte du régime hebdomadaire : 175 % est le taux légal du régime
    /// 48 h et reste donc autorisé même sans l'option « taux étendus ». Les taux 125/150
    /// restent toujours acceptés (lignes historiques).
    /// </summary>
    public static bool IsValid(decimal rate, bool enableExtendedOvertimeRates, WeeklyWorkRegime regime) =>
        IsValid(rate, enableExtendedOvertimeRates)
        || (regime == WeeklyWorkRegime.FortyEightHours && rate is 175m);

    public static string ToDisplayString(decimal rate) => rate switch
    {
        125m => "125 %",
        150m => "150 %",
        175m => "175 %",
        200m => "200 %",
        _ => $"{rate} %"
    };
}
