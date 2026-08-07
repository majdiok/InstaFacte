using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain.Payroll;

public sealed class TerminationIndemnityCalculatorTests
{
    [Fact]
    public void Compute_24MonthsSeniority_OneDayPerMonth_CappedAtThreeMonths()
    {
        var start = new DateTime(2024, 1, 1);
        var end = new DateTime(2026, 1, 1);
        var monthlyGross = 1200m;

        var result = TerminationIndemnityCalculator.Compute(new TerminationIndemnityCalculator.Input(
            start, end, monthlyGross, TerminationReason.Dismissal));

        Assert.Equal(24, result.SeniorityMonths);
        Assert.Equal(24, result.IndemnityDays);
        var expectedDaily = Math.Round(monthlyGross / 26m, 3, MidpointRounding.AwayFromZero);
        Assert.Equal(expectedDaily, result.DailyRate);
        Assert.Equal(Math.Round(24m * expectedDaily, 3, MidpointRounding.AwayFromZero), result.AppliedAmount);
        Assert.True(result.AppliedAmount <= monthlyGross * 3m);
    }

    [Fact]
    public void Compute_Resignation_ReturnsZeroLegalIndemnity()
    {
        var result = TerminationIndemnityCalculator.Compute(new TerminationIndemnityCalculator.Input(
            new DateTime(2020, 1, 1),
            new DateTime(2026, 6, 1),
            2000m,
            TerminationReason.Resignation));

        Assert.Equal(0m, result.AppliedAmount);
        Assert.Equal(0, result.IndemnityDays);
    }

    [Fact]
    public void Compute_HighSeniority_HitsThreeMonthCap()
    {
        var result = TerminationIndemnityCalculator.Compute(new TerminationIndemnityCalculator.Input(
            new DateTime(2010, 1, 1),
            new DateTime(2026, 8, 1),
            1000m,
            TerminationReason.Dismissal));

        Assert.Equal(3000m, result.AppliedAmount);
        Assert.Equal(3000m, result.CappedAmount);
    }
}
