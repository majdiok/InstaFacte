using FactuTrust.Application.Common.Validation;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

public sealed class TunisianInvoiceLineCalculationTests
{
    [Fact]
    public void CalculateLine_WithoutFodec_VatOnHtOnly()
    {
        var amounts = TunisianInvoiceLineCalculation.CalculateLine(
            quantity: 10m,
            unitPriceHT: 100m,
            discountType: null,
            discountValue: null,
            vatRate: 19,
            fodecApplicable: false,
            fodecRatePercent: 1m);

        Assert.Equal(1000m, amounts.TotalHT);
        Assert.Equal(0m, amounts.FodecAmount);
        Assert.Equal(190m, amounts.VatAmount);
        Assert.Equal(1190m, amounts.TotalTTC);
    }

    [Fact]
    public void CalculateLine_WithFodec_VatOnHtPlusFodec()
    {
        var amounts = TunisianInvoiceLineCalculation.CalculateLine(
            quantity: 10m,
            unitPriceHT: 100m,
            discountType: null,
            discountValue: null,
            vatRate: 19,
            fodecApplicable: true,
            fodecRatePercent: 1m);

        Assert.Equal(1000m, amounts.TotalHT);
        Assert.Equal(10m, amounts.FodecAmount);
        Assert.Equal(191.900m, amounts.VatAmount);
        Assert.Equal(1201.900m, amounts.TotalTTC);
    }

    [Fact]
    public void CalculateLine_WithPercentDiscount_AppliesBeforeFodec()
    {
        var amounts = TunisianInvoiceLineCalculation.CalculateLine(
            quantity: 1m,
            unitPriceHT: 1000m,
            discountType: "PERCENT",
            discountValue: 10m,
            vatRate: 19,
            fodecApplicable: true,
            fodecRatePercent: 1m);

        Assert.Equal(900m, amounts.TotalHT);
        Assert.Equal(9m, amounts.FodecAmount);
        Assert.Equal(172.710m, amounts.VatAmount);
        Assert.Equal(1081.710m, amounts.TotalTTC);
    }
}
