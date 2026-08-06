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

    // ── Régime hebdomadaire (48 h ÷ 208 / 40 h ÷ 173,33) ──

    [Fact]
    public void ComputeHourlyRate_FortyEightHoursRegime_MatchesLegacyDivisor()
    {
        // Non-régression : le régime 48 h explicite produit le même résultat que l'historique.
        Assert.Equal(
            OvertimeAmountCalculator.ComputeHourlyRate(2080m),
            OvertimeAmountCalculator.ComputeHourlyRate(2080m, WeeklyWorkRegime.FortyEightHours));
    }

    [Fact]
    public void ComputeHourlyRate_FortyHoursRegime_Uses173_33Divisor()
    {
        // 1733,30 / 173,33 = 10,000
        Assert.Equal(10m, OvertimeAmountCalculator.ComputeHourlyRate(1733.30m, WeeklyWorkRegime.FortyHours));
    }

    [Fact]
    public void ComputeAmount_FortyHoursRegime_125Percent()
    {
        // 1000 / 173,33 = 5,769 ; × 10 h × 1,25 = 72,113 (arrondi 3 déc. sur le taux horaire)
        var amount = OvertimeAmountCalculator.ComputeAmount(1000m, 10m, 125m, regime: WeeklyWorkRegime.FortyHours);
        Assert.Equal(72.113m, amount);
    }

    [Fact]
    public void ComputeAmount_FortyEightHoursRegime_175Percent_AllowedWithoutExtendedFlag()
    {
        // 175 % est le taux légal du régime 48 h : accepté même sans l'option « taux étendus ».
        var amount = OvertimeAmountCalculator.ComputeAmount(2080m, 2m, 175m, regime: WeeklyWorkRegime.FortyEightHours);
        Assert.Equal(35m, amount);
    }

    [Fact]
    public void ComputeAmount_FortyHoursRegime_175Percent_StillRequiresExtendedFlag()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            OvertimeAmountCalculator.ComputeAmount(2080m, 2m, 175m, regime: WeeklyWorkRegime.FortyHours));
    }

    [Fact]
    public void IsValid_RegimeAware_Accepts175ForFortyEightHours()
    {
        Assert.True(OvertimeRatePercentExtensions.IsValid(175m, false, WeeklyWorkRegime.FortyEightHours));
        Assert.False(OvertimeRatePercentExtensions.IsValid(175m, false, WeeklyWorkRegime.FortyHours));
        Assert.False(OvertimeRatePercentExtensions.IsValid(200m, false, WeeklyWorkRegime.FortyEightHours));
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

    [Fact]
    public void SumPendingPaidLeaveDays_CountsUnapprovedPaidOnly()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var leaves = new[]
        {
            (id1, LeaveType.Paid, false, 4m, 2026),
            (id2, LeaveType.Paid, true, 3m, 2026),
            (Guid.NewGuid(), LeaveType.Unpaid, false, 2m, 2026),
            (Guid.NewGuid(), LeaveType.Paid, false, 1m, 2025)
        };

        Assert.Equal(4m, LeaveBalanceService.SumPendingPaidLeaveDays(leaves, 2026));
    }

    [Fact]
    public void SumPendingPaidLeaveDays_ExcludesSpecifiedLeave()
    {
        var excluded = Guid.NewGuid();
        var leaves = new[]
        {
            (excluded, LeaveType.Paid, false, 4m, 2026),
            (Guid.NewGuid(), LeaveType.Paid, false, 1m, 2026)
        };

        Assert.Equal(1m, LeaveBalanceService.SumPendingPaidLeaveDays(leaves, 2026, excluded));
    }

    [Fact]
    public void ComputeAvailable_SubtractsPendingFromRemaining()
    {
        Assert.Equal(2m, LeaveBalanceService.ComputeAvailable(6m, 4m));
    }
}
