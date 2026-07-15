using FactuTrust.Application.DTOs;
using FactuTrust.Infrastructure.Services;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

public sealed class WithholdingTaxCalculationTests
{
    private readonly WithholdingTaxCalculationService _sut = new();

    [Theory]
    [InlineData("RS2_000001", true, false, false, 1000, 19, 3, 30)]
    [InlineData("RS2_000002", true, false, false, 1000, 19, 10, 100)]
    [InlineData("RS1_000001", true, false, false, 5000, 0, 15, 750)]
    [InlineData("RS3_000001", true, false, false, 10000, 0, 20, 2000)]
    [InlineData("RS4_000001", true, false, false, 5000, 0, 10, 500)]
    [InlineData("RS7_000001", true, false, false, 2000, 19, 1.5, 30)]
    [InlineData("RS8_000001", true, false, false, 1000, 0, 25, 250)]
    public void CalculateWithholding_StandardCases(
        string code, bool isResident, bool hasCNPC, bool hasPriseEnCharge,
        decimal amountHT, decimal vatRate, decimal expectedRate, decimal expectedWithheld)
    {
        var request = new WithholdingCalculationRequest(amountHT, vatRate, code, isResident, hasCNPC, hasPriseEnCharge);
        var result = _sut.CalculateWithholding(request);

        Assert.Equal(expectedRate, result.WithholdingRate);
        Assert.Equal(expectedWithheld, result.WithholdingAmount);
        Assert.Equal(amountHT, result.GrossAmountHT);
    }

    [Fact]
    public void CalculateWithholding_RS7_BelowThreshold_ShouldReturnZero()
    {
        var request = new WithholdingCalculationRequest(800m, 19m, "RS7_000001", true, false, false);
        var result = _sut.CalculateWithholding(request);

        Assert.Equal(0m, result.WithholdingRate);
        Assert.Equal(0m, result.WithholdingAmount);
    }

    [Fact]
    public void CalculateWithholding_RS7_CustomThreshold_1500TtcStillBelow_ShouldReturnZero()
    {
        var request = new WithholdingCalculationRequest(
            800m, 19m, "RS7_000001", true, false, false,
            WithholdingRateOverride: null,
            Rs7TtcThresholdTnd: 1500m);
        var result = _sut.CalculateWithholding(request);

        Assert.Equal(0m, result.WithholdingAmount);
    }

    [Fact]
    public void CalculateWithholding_RS7_CustomThreshold_900_AppliesWhenTtcAbove()
    {
        var request = new WithholdingCalculationRequest(
            2000m, 19m, "RS7_000001", true, false, false,
            WithholdingRateOverride: null,
            Rs7TtcThresholdTnd: 900m);
        var result = _sut.CalculateWithholding(request);

        Assert.Equal(1.5m, result.WithholdingRate);
        Assert.Equal(30m, result.WithholdingAmount);
    }

    [Fact]
    public void CalculateWithholding_RS7_AtThreshold_ShouldApply()
    {
        var request = new WithholdingCalculationRequest(840.34m, 19m, "RS7_000001", true, false, false);
        var result = _sut.CalculateWithholding(request);

        Assert.True(result.WithholdingAmount > 0);
    }

    [Fact]
    public void CalculateWithholding_NonResident_WithoutCNPC_ShouldUseHighRate()
    {
        var request = new WithholdingCalculationRequest(10000m, 0m, "RS9_000001", false, false, false);
        var result = _sut.CalculateWithholding(request);

        Assert.Equal(15m, result.WithholdingRate);
        Assert.Equal(1500m, result.WithholdingAmount);
    }

    [Fact]
    public void CalculateWithholding_NonResident_WithCNPC_ShouldUseReducedRate()
    {
        var request = new WithholdingCalculationRequest(10000m, 0m, "RS9_000001", false, true, false);
        var result = _sut.CalculateWithholding(request);

        Assert.Equal(10m, result.WithholdingRate);
        Assert.Equal(1000m, result.WithholdingAmount);
    }

    [Fact]
    public void CalculateWithholding_PriseEnCharge_ShouldGrossUp()
    {
        var request = new WithholdingCalculationRequest(1000m, 0m, "RS2_000001", true, false, true);
        var result = _sut.CalculateWithholding(request);

        Assert.True(result.WithholdingAmount > 30m);
        var expectedEffectiveRate = 3m / (100m - 3m) * 100m;
        var expectedWithheld = Math.Round(1000m * expectedEffectiveRate / 100m, 3);
        Assert.Equal(expectedWithheld, result.WithholdingAmount);
    }

    [Fact]
    public void CalculateWithholding_WithVatWithholding_RS2_ShouldApply25Percent()
    {
        var request = new WithholdingCalculationRequest(1000m, 19m, "RS2_000002", true, false, false);
        var result = _sut.CalculateWithholding(request);

        Assert.NotNull(result.VatWithholdingAmount);
        var expectedVatWithholding = Math.Round(190m * 0.25m, 3);
        Assert.Equal(expectedVatWithholding, result.VatWithholdingAmount!.Value);
    }

    [Fact]
    public void CalculateWithholding_WithVatWithholding_RS9_ShouldApply100Percent()
    {
        var request = new WithholdingCalculationRequest(10000m, 19m, "RS9_000001", false, false, false);
        var result = _sut.CalculateWithholding(request);

        Assert.NotNull(result.VatWithholdingAmount);
        var expectedVatWithholding = Math.Round(1900m * 1.0m, 3);
        Assert.Equal(expectedVatWithholding, result.VatWithholdingAmount!.Value);
    }

    [Fact]
    public void CalculateWithholding_NoVatWithholding_ForRS3()
    {
        var request = new WithholdingCalculationRequest(5000m, 19m, "RS3_000001", true, false, false);
        var result = _sut.CalculateWithholding(request);

        Assert.Null(result.VatWithholdingAmount);
    }

    [Fact]
    public void CalculateWithholding_NetAmount_IsCorrect()
    {
        var request = new WithholdingCalculationRequest(1000m, 19m, "RS2_000001", true, false, false);
        var result = _sut.CalculateWithholding(request);

        var expectedNet = result.AmountTTC - result.WithholdingAmount - (result.VatWithholdingAmount ?? 0);
        Assert.Equal(expectedNet, result.NetAmountPaid);
    }

    [Fact]
    public void CalculateWithholding_EmptyOperationCode_ShouldReturnZero()
    {
        var request = new WithholdingCalculationRequest(1000m, 19m, "", true, false, false);
        var result = _sut.CalculateWithholding(request);

        Assert.Equal(0m, result.WithholdingRate);
        Assert.Equal(0m, result.WithholdingAmount);
    }

    [Fact]
    public void CalculateWithholding_VatCalculation_IsCorrect()
    {
        var request = new WithholdingCalculationRequest(1000m, 19m, "RS2_000001", true, false, false);
        var result = _sut.CalculateWithholding(request);

        Assert.Equal(190m, result.VatAmount);
        Assert.Equal(1190m, result.AmountTTC);
    }

    [Fact]
    public void CalculateWithholding_RateOverride_AboveRS7Threshold_UsesOverride()
    {
        var request = new WithholdingCalculationRequest(
            2000m, 19m, "RS7_000001", true, false, false, WithholdingRateOverride: 2m);
        var result = _sut.CalculateWithholding(request);

        Assert.Equal(2m, result.WithholdingRate);
        Assert.Equal(40m, result.WithholdingAmount);
    }

    [Fact]
    public void CalculateWithholding_InvalidOverride_FallsBackToStatutory()
    {
        var request = new WithholdingCalculationRequest(
            2000m, 19m, "RS7_000001", true, false, false, WithholdingRateOverride: 150m);
        var result = _sut.CalculateWithholding(request);

        Assert.Equal(1.5m, result.WithholdingRate);
    }
}
