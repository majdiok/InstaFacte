using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

public sealed class ProductPricingCalculatorTests
{
    [Theory]
    [InlineData(50400, 13, false, 56952)]
    [InlineData(45000, 13, false, 50850)]
    [InlineData(50, 13, false, 56.5)]
    [InlineData(50, 13, true, 57.065)]
    public void CalculateSaleTtc_matches_expected(
        decimal unitPriceHt,
        int vatPercent,
        bool isFodec,
        decimal expectedTtc)
    {
        var ttc = ProductPricingCalculator.CalculateSaleTtc(
            unitPriceHt,
            VatRateExtensions.FromPercent(vatPercent),
            isFodec);

        Assert.Equal(expectedTtc, ttc);
    }

    [Theory]
    [InlineData(56952, 13, false, 50400)]
    [InlineData(56.5, 13, false, 50)]
    [InlineData(57.065, 13, true, 50)]
    public void DeriveUnitPriceFromTtc_round_trips(
        decimal saleTtc,
        int vatPercent,
        bool isFodec,
        decimal expectedHt)
    {
        var ht = ProductPricingCalculator.DeriveUnitPriceFromTtc(
            saleTtc,
            VatRateExtensions.FromPercent(vatPercent),
            isFodec);

        Assert.Equal(expectedHt, ht);
    }

    [Fact]
    public void CalculateUnitPriceFromMargin_axeane_example()
    {
        var ht = ProductPricingCalculator.CalculateUnitPriceFromMargin(45000, 12);
        Assert.Equal(50400m, ht);
    }

    [Fact]
    public void CalculateMarginPercent_axeane_example()
    {
        var margin = ProductPricingCalculator.CalculateMarginPercent(45000, 50400);
        Assert.Equal(12m, margin);
    }

    [Fact]
    public void CalculateMarginPercent_returns_null_when_purchase_zero()
    {
        Assert.Null(ProductPricingCalculator.CalculateMarginPercent(0, 100));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(13)]
    [InlineData(19)]
    public void Round_trip_ht_ttc_all_vat_rates(int vatPercent)
    {
        const decimal ht = 123.456m;
        var ttc = ProductPricingCalculator.CalculateSaleTtc(ht, VatRateExtensions.FromPercent(vatPercent), false);
        var derived = ProductPricingCalculator.DeriveUnitPriceFromTtc(ttc, VatRateExtensions.FromPercent(vatPercent), false);
        Assert.Equal(ht, derived);
    }
}
