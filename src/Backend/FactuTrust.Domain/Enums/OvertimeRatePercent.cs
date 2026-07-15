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

    public static string ToDisplayString(decimal rate) => rate switch
    {
        125m => "125 %",
        150m => "150 %",
        175m => "175 %",
        200m => "200 %",
        _ => $"{rate} %"
    };
}
