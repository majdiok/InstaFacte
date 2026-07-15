using FactuTrust.Infrastructure.Services;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

public sealed class WithholdingComplianceTests
{
    [Theory]
    [InlineData(2026, 1, 2026, 2, 28)]
    [InlineData(2026, 2, 2026, 3, 28)]
    [InlineData(2026, 12, 2027, 1, 28)]
    [InlineData(2024, 1, 2024, 2, 28)]
    public void GetNextTejDeadline_ReturnsCorrectDate(
        int year, int month, int expectedYear, int expectedMonth, int expectedDay)
    {
        var sut = new WithholdingComplianceService(null!);
        var deadline = sut.GetNextTejDeadline(year, month);

        Assert.Equal(expectedYear, deadline.Year);
        Assert.Equal(expectedMonth, deadline.Month);
        Assert.Equal(expectedDay, deadline.Day);
    }

    [Fact]
    public void IsThresholdApplicable_RS7_BelowThreshold_ReturnsFalse()
    {
        var sut = new WithholdingComplianceService(null!);
        Assert.False(sut.IsThresholdApplicable("RS7_000001", 999m));
    }

    [Fact]
    public void IsThresholdApplicable_RS7_AtThreshold_ReturnsTrue()
    {
        var sut = new WithholdingComplianceService(null!);
        Assert.True(sut.IsThresholdApplicable("RS7_000001", 1000m));
    }

    [Fact]
    public void IsThresholdApplicable_RS7_AboveThreshold_ReturnsTrue()
    {
        var sut = new WithholdingComplianceService(null!);
        Assert.True(sut.IsThresholdApplicable("RS7_000001", 1500m));
    }

    [Fact]
    public void IsThresholdApplicable_NonRS7_AnyAmount_ReturnsTrue()
    {
        var sut = new WithholdingComplianceService(null!);
        Assert.True(sut.IsThresholdApplicable("RS2_000001", 100m));
        Assert.True(sut.IsThresholdApplicable("RS1_000001", 50m));
    }

    [Fact]
    public void IsThresholdApplicable_EmptyCode_ReturnsFalse()
    {
        var sut = new WithholdingComplianceService(null!);
        Assert.False(sut.IsThresholdApplicable("", 5000m));
    }
}
