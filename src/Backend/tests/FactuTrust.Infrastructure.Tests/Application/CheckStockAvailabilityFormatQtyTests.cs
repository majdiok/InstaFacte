using FactuTrust.Application.Features.Stock.Queries;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

public sealed class CheckStockAvailabilityFormatQtyTests
{
    [Theory]
    [InlineData(7, "7")]
    [InlineData(45, "45")]
    [InlineData(45.5, "45,5")]
    [InlineData(0.5, "0,5")]
    [InlineData(1.25, "1,25")]
    public void FormatQty_TrimsTrailingZeros_UsesFrenchDecimalSeparator(decimal value, string expected)
    {
        Assert.Equal(expected, CheckStockAvailabilityQueryHandler.FormatQty(value));
    }

    [Fact]
    public void FormatQty_FourScaleDecimal_DoesNotKeepTrailingZeros()
    {
        Assert.Equal("45", CheckStockAvailabilityQueryHandler.FormatQty(45.0000m));
    }
}
