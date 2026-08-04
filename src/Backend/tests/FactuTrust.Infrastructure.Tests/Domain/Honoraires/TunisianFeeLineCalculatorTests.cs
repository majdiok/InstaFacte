using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Honoraires;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain.Honoraires;

public sealed class TunisianFeeLineCalculatorTests
{
    [Fact]
    public void Calculate_WithRemiseAndStandardVat_ReturnsExpectedTotals()
    {
        var calc = TunisianFeeLineCalculator.Calculate(2m, 1000m, 10m, VatRate.Standard);

        Assert.Equal(2000m, calc.GrossHt);
        Assert.Equal(200m, calc.DiscountAmount);
        Assert.Equal(1800m, calc.NetHt);
        Assert.Equal(342m, calc.VatAmount);
        Assert.Equal(2142m, calc.TotalTtc);
    }

    [Fact]
    public void Calculate_WithoutDiscount_VatOnFullGross()
    {
        var calc = TunisianFeeLineCalculator.Calculate(1m, 100m, null, VatRate.Standard);

        Assert.Equal(100m, calc.NetHt);
        Assert.Equal(19m, calc.VatAmount);
        Assert.Equal(119m, calc.TotalTtc);
    }
}
