namespace FactuTrust.Domain.Enums;

/// <summary>
/// Tunisian VAT rates according to the Tunisian tax code.
/// </summary>
public enum VatRate
{
    /// <summary>
    /// 0% - Exempt (Exonéré)
    /// Applies to: exports, certain agricultural products, etc.
    /// </summary>
    Exempt = 0,

    /// <summary>
    /// 7% - Reduced rate (Taux réduit)
    /// Applies to: basic necessities, medicines, etc.
    /// </summary>
    Reduced = 7,

    /// <summary>
    /// 13% - Intermediate rate (Taux intermédiaire)
    /// Applies to: certain services, hospitality, etc.
    /// </summary>
    Intermediate = 13,

    /// <summary>
    /// 19% - Standard rate (Taux normal)
    /// Applies to: general goods and services.
    /// </summary>
    Standard = 19
}

public static class VatRateExtensions
{
    public static decimal ToDecimal(this VatRate rate) => (int)rate;

    public static decimal ToPercentage(this VatRate rate) => (int)rate / 100m;

    public static string ToDisplayString(this VatRate rate) => rate switch
    {
        VatRate.Exempt => "0% (Exonéré)",
        VatRate.Reduced => "7% (Réduit)",
        VatRate.Intermediate => "13% (Intermédiaire)",
        VatRate.Standard => "19% (Normal)",
        _ => throw new ArgumentOutOfRangeException(nameof(rate))
    };

    public static string ToShortString(this VatRate rate) => $"{(int)rate}%";

    /// <summary>
    /// Creates a VatRate from a percentage value.
    /// </summary>
    public static VatRate FromPercent(int percent) => percent switch
    {
        0 => VatRate.Exempt,
        7 => VatRate.Reduced,
        13 => VatRate.Intermediate,
        19 => VatRate.Standard,
        _ => throw new ArgumentException($"Taux de TVA non valide: {percent}%", nameof(percent))
    };
}
