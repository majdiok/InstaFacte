using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Services.Honoraires;

/// <summary>
/// Pure Tunisian line calculator for firm honoraires documents (no FODEC / fiscal stamp).
/// Isolated from commercial <c>InvoiceLine.Calculate</c> to avoid sales regressions.
/// </summary>
public readonly record struct FeeLineCalculation(
    decimal GrossHt,
    decimal DiscountAmount,
    decimal NetHt,
    decimal VatAmount,
    decimal TotalTtc);

public static class TunisianFeeLineCalculator
{
    private const int Decimals = 3;

    public static FeeLineCalculation Calculate(
        decimal quantity,
        decimal unitPrice,
        decimal? discountPercent,
        VatRate vatRate)
    {
        var gross = Round(quantity * unitPrice);
        var discount = 0m;
        if (discountPercent is > 0)
            discount = Round(gross * discountPercent.Value / 100m);

        var netHt = Round(gross - discount);
        var vat = Round(netHt * vatRate.ToPercentage());
        var total = Round(netHt + vat);
        return new FeeLineCalculation(gross, discount, netHt, vat, total);
    }

    private static decimal Round(decimal value) => Math.Round(value, Decimals, MidpointRounding.AwayFromZero);
}
