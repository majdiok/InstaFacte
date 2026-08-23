using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

public sealed class ProrationCalculatorTests
{
    [Fact]
    public void ProrateAmount_FullPeriod_ReturnsFullAmount()
    {
        var amount = ProrationCalculator.ProrateAmount(
            1000m,
            new DateTime(2026, 1, 1),
            new DateTime(2026, 1, 31),
            new DateTime(2026, 1, 1),
            new DateTime(2026, 1, 31));

        Assert.Equal(1000m, amount);
    }

    [Fact]
    public void ProrateAmount_HalfMonth_ReturnsHalf()
    {
        var amount = ProrationCalculator.ProrateAmount(
            1000m,
            new DateTime(2026, 1, 1),
            new DateTime(2026, 1, 31),
            new DateTime(2026, 1, 16),
            new DateTime(2026, 1, 31));

        Assert.True(amount > 400m && amount < 600m);
    }

    [Fact]
    public void ComputeNextBillingDate_Monthly_AdvancesOneMonth()
    {
        var next = ProrationCalculator.ComputeNextBillingDate(
            new DateTime(2026, 1, 15),
            BillingFrequency.Monthly,
            15);

        Assert.Equal(new DateTime(2026, 2, 15), next);
    }

    [Fact]
    public void ClampBillingDay_Day31InFebruary_UsesLastDay()
    {
        var date = ProrationCalculator.ClampBillingDay(2026, 2, 31);
        Assert.Equal(28, date.Day);
    }
}
