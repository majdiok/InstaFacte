using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.FirmGovernance;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

public sealed class FirmLeaveWorkingDaysCalculatorTests
{
    [Fact]
    public void Full_week_mon_to_fri_is_5()
    {
        var days = FirmLeaveWorkingDaysCalculator.ComputeDays(
            new DateTime(2026, 8, 10), new DateTime(2026, 8, 14),
            FirmLeaveDayUnit.FullDay, FirmLeaveDayUnit.FullDay, allowHalfDays: true);
        Assert.Equal(5m, days);
    }

    [Fact]
    public void Weekend_excluded()
    {
        var days = FirmLeaveWorkingDaysCalculator.ComputeDays(
            new DateTime(2026, 8, 14), new DateTime(2026, 8, 17),
            FirmLeaveDayUnit.FullDay, FirmLeaveDayUnit.FullDay, allowHalfDays: true);
        Assert.Equal(2m, days); // Fri + Mon
    }

    [Fact]
    public void Half_day_afternoon_start()
    {
        var days = FirmLeaveWorkingDaysCalculator.ComputeDays(
            new DateTime(2026, 8, 10), new DateTime(2026, 8, 10),
            FirmLeaveDayUnit.Afternoon, FirmLeaveDayUnit.Afternoon, allowHalfDays: true);
        Assert.Equal(0.5m, days);
    }

    [Fact]
    public void Holiday_excluded_when_callback_provided()
    {
        var days = FirmLeaveWorkingDaysCalculator.ComputeDays(
            new DateTime(2026, 8, 10), new DateTime(2026, 8, 12),
            FirmLeaveDayUnit.FullDay, FirmLeaveDayUnit.FullDay, allowHalfDays: true,
            isHoliday: d => d.Day == 11);
        Assert.Equal(2m, days);
    }

    [Fact]
    public void Half_days_disabled_forces_full_day()
    {
        var days = FirmLeaveWorkingDaysCalculator.ComputeDays(
            new DateTime(2026, 8, 10), new DateTime(2026, 8, 10),
            FirmLeaveDayUnit.Morning, FirmLeaveDayUnit.Morning, allowHalfDays: false);
        Assert.Equal(1m, days);
    }
}
