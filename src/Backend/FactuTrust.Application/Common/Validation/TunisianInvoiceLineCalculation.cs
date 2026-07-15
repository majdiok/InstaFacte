using FactuTrust.Application.Common.Validation;

namespace FactuTrust.Application.Common.Validation;

/// <summary>
/// Canonical Tunisian invoice line calculation (FODEC then TVA on HT+FODEC).
/// Single source of truth for wizard validation and server-side totals.
/// </summary>
public static class TunisianInvoiceLineCalculation
{
    public readonly record struct LineAmounts(
        decimal TotalHT,
        decimal FodecAmount,
        decimal VatAmount,
        decimal TotalTTC);

    public static decimal CalculateDiscount(decimal subtotal, string? discountType, decimal? discountValue)
    {
        if (!discountValue.HasValue || discountValue.Value <= 0)
            return 0;

        return discountType switch
        {
            "PERCENT" => TunisianValidationRules.RoundToMillimes(subtotal * discountValue.Value / 100),
            "AMOUNT" => TunisianValidationRules.RoundToMillimes(discountValue.Value),
            _ => 0
        };
    }

    public static LineAmounts CalculateLine(
        decimal quantity,
        decimal unitPriceHT,
        string? discountType,
        decimal? discountValue,
        int vatRate,
        bool fodecApplicable,
        decimal fodecRatePercent)
    {
        var subtotal = quantity * unitPriceHT;
        var discount = CalculateDiscount(subtotal, discountType, discountValue);
        var totalHT = TunisianValidationRules.RoundToMillimes(subtotal - discount);
        var fodec = fodecApplicable && fodecRatePercent > 0
            ? TunisianValidationRules.RoundToMillimes(totalHT * fodecRatePercent / 100m)
            : 0m;
        var vatBase = totalHT + fodec;
        var vat = TunisianValidationRules.RoundToMillimes(vatBase * vatRate / 100m);
        var ttc = TunisianValidationRules.RoundToMillimes(totalHT + fodec + vat);
        return new LineAmounts(totalHT, fodec, vat, ttc);
    }
}
