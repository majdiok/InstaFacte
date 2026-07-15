using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain.Payroll;

public sealed class OvertimeAmountCalculatorTests
{
    [Fact]
    public void ComputeHourlyRate_UsesTwentySixDayMonthAndEightHours()
    {
        var hourly = OvertimeAmountCalculator.ComputeHourlyRate(2080m);
        Assert.Equal(10m, hourly);
    }

    [Fact]
    public void ComputeAmount_125Percent_Majoration()
    {
        var amount = OvertimeAmountCalculator.ComputeAmount(2080m, 4m, 125m);
        Assert.Equal(50m, amount);
    }

    [Fact]
    public void ComputeAmount_150Percent_Majoration()
    {
        var amount = OvertimeAmountCalculator.ComputeAmount(2080m, 2m, 150m);
        Assert.Equal(30m, amount);
    }

    [Fact]
    public void ComputeAmount_175Percent_WhenExtendedEnabled()
    {
        var amount = OvertimeAmountCalculator.ComputeAmount(2080m, 2m, 175m, enableExtendedOvertimeRates: true);
        Assert.Equal(35m, amount);
    }

    [Fact]
    public void ComputeAmount_200Percent_WhenExtendedEnabled()
    {
        var amount = OvertimeAmountCalculator.ComputeAmount(2080m, 2m, 200m, enableExtendedOvertimeRates: true);
        Assert.Equal(40m, amount);
    }

    [Fact]
    public void ComputeAmount_175Percent_RejectedWhenExtendedDisabled()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            OvertimeAmountCalculator.ComputeAmount(2080m, 2m, 175m));
    }

    [Fact]
    public void IsValid_AcceptsExtendedRatesOnlyWhenFlagSet()
    {
        Assert.True(OvertimeRatePercentExtensions.IsValid(175m, enableExtendedOvertimeRates: true));
        Assert.True(OvertimeRatePercentExtensions.IsValid(200m, enableExtendedOvertimeRates: true));
        Assert.False(OvertimeRatePercentExtensions.IsValid(175m));
        Assert.False(OvertimeRatePercentExtensions.IsValid(200m));
    }

    [Fact]
    public void ResolveEffectiveAmount_OverrideTakesPrecedence()
    {
        var effective = OvertimeAmountCalculator.ResolveEffectiveAmount(50m, 75m);
        Assert.Equal(75m, effective);
    }

    [Fact]
    public void ResolveEffectiveAmount_UsesComputedWhenNoOverride()
    {
        var effective = OvertimeAmountCalculator.ResolveEffectiveAmount(50m, null);
        Assert.Equal(50m, effective);
    }
}

public sealed class LeaveBalanceServiceTests
{
    [Fact]
    public void ComputeAccruedDays_FullMonth_ReturnsOneDay()
    {
        Assert.Equal(1m, LeaveBalanceService.ComputeAccruedDays(26m));
    }

    [Fact]
    public void ComputeAccruedDays_HalfMonth_ReturnsHalfDay()
    {
        Assert.Equal(0.5m, LeaveBalanceService.ComputeAccruedDays(13m));
    }

    [Fact]
    public void ComputeWorkedDays_SubtractsUnpaidAbsences()
    {
        Assert.Equal(20m, LeaveBalanceService.ComputeWorkedDays(6m));
    }

    [Fact]
    public void SumConsumedPaidLeaveDays_CountsApprovedPaidOnly()
    {
        var leaves = new[]
        {
            (LeaveType.Paid, true, 3m, 2026),
            (LeaveType.Paid, false, 5m, 2026),
            (LeaveType.Unpaid, true, 2m, 2026),
            (LeaveType.Paid, true, 2m, 2025)
        };

        Assert.Equal(3m, LeaveBalanceService.SumConsumedPaidLeaveDays(leaves, 2026));
    }

    [Fact]
    public void ComputeRemaining_IncludesOpeningAndAccruals()
    {
        var remaining = LeaveBalanceService.ComputeRemaining(2m, 3m, 1.5m);
        Assert.Equal(3.5m, remaining);
    }
}
