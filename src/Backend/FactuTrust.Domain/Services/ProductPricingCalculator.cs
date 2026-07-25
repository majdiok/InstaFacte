using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Services;

/// <summary>
/// Centralized Tunisian product pricing formulas (FODEC → VAT → TTC, margin).
/// </summary>
public static class ProductPricingCalculator
{
    public const decimal DefaultFodecRatePercent = 1m;
    public const decimal MinProfitMarginPercent = -99.999m;
    public const decimal MaxProfitMarginPercent = 9999.999m;

    public static decimal CalculateSaleTtc(
        decimal unitPriceHt,
        VatRate vatRate,
        bool isFodecApplicable,
        decimal fodecRatePercent = DefaultFodecRatePercent)
    {
        var fodecAmount = CalculateFodecAmount(unitPriceHt, isFodecApplicable, fodecRatePercent);
        var vatAmount = CalculateVatAmount(unitPriceHt, fodecAmount, vatRate);
        return MillimeRounding.Round(unitPriceHt + fodecAmount + vatAmount);
    }

    public static decimal CalculateSaleTtc(
        decimal unitPriceHt,
        decimal vatRatePercent,
        bool isFodecApplicable,
        decimal fodecRatePercent = DefaultFodecRatePercent)
    {
        var vatRate = VatRateExtensions.FromPercent((int)vatRatePercent);
        return CalculateSaleTtc(unitPriceHt, vatRate, isFodecApplicable, fodecRatePercent);
    }

    public static decimal DeriveUnitPriceFromTtc(
        decimal saleTtc,
        VatRate vatRate,
        bool isFodecApplicable,
        decimal fodecRatePercent = DefaultFodecRatePercent)
    {
        if (saleTtc < 0)
            throw new ArgumentOutOfRangeException(nameof(saleTtc), "Le prix TTC ne peut pas être négatif");

        var fodecFactor = isFodecApplicable && fodecRatePercent > 0
            ? 1m + fodecRatePercent / 100m
            : 1m;
        var vatFactor = 1m + vatRate.ToDecimal() / 100m;
        var multiplier = fodecFactor * vatFactor;

        if (multiplier <= 0)
            throw new InvalidOperationException("Multiplicateur TTC invalide");

        return MillimeRounding.Round(saleTtc / multiplier);
    }

    public static decimal DeriveUnitPriceFromTtc(
        decimal saleTtc,
        decimal vatRatePercent,
        bool isFodecApplicable,
        decimal fodecRatePercent = DefaultFodecRatePercent)
    {
        var vatRate = VatRateExtensions.FromPercent((int)vatRatePercent);
        return DeriveUnitPriceFromTtc(saleTtc, vatRate, isFodecApplicable, fodecRatePercent);
    }

    public static decimal? CalculateMarginPercent(decimal purchasePrice, decimal unitPriceHt)
    {
        if (purchasePrice <= 0)
            return null;

        var margin = ((unitPriceHt - purchasePrice) / purchasePrice) * 100m;
        return MillimeRounding.Round(margin, 3);
    }

    public static decimal CalculateUnitPriceFromMargin(decimal purchasePrice, decimal marginPercent)
    {
        if (purchasePrice <= 0)
            throw new ArgumentOutOfRangeException(nameof(purchasePrice), "Le prix d'achat doit être positif pour calculer la marge");

        return MillimeRounding.Round(purchasePrice * (1m + marginPercent / 100m));
    }

    public static decimal CalculateFodecAmount(
        decimal unitPriceHt,
        bool isFodecApplicable,
        decimal fodecRatePercent = DefaultFodecRatePercent)
    {
        if (!isFodecApplicable || fodecRatePercent <= 0)
            return 0m;

        return MillimeRounding.Round(unitPriceHt * fodecRatePercent / 100m);
    }

    public static decimal CalculateVatAmount(decimal unitPriceHt, decimal fodecAmount, VatRate vatRate)
    {
        var vatBase = unitPriceHt + fodecAmount;
        return MillimeRounding.Round(vatBase * vatRate.ToDecimal() / 100m);
    }

    /// <summary>
    /// Recomputes margin from catalog prices (server-side source of truth on save).
    /// </summary>
    public static decimal? ResolveProfitMarginPercent(decimal? purchasePrice, decimal unitPriceHt)
    {
        if (!purchasePrice.HasValue || purchasePrice.Value <= 0)
            return null;

        return CalculateMarginPercent(purchasePrice.Value, unitPriceHt);
    }
}
