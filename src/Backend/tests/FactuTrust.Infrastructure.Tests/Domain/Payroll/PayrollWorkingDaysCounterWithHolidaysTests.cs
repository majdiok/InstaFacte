using FactuTrust.Domain.Services.Payroll;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain.Payroll;

public sealed class PayrollWorkingDaysCounterWithHolidaysTests
{
    [Fact]
    public void CountWeekdays_WithoutHolidays_MatchesLegacyBehavior()
    {
        var start = new DateTime(2026, 3, 1);
        var end = new DateTime(2026, 3, 31);

        Assert.Equal(
            PayrollWorkingDaysCounter.CountWeekdays(start, end),
            PayrollWorkingDaysCounter.CountWeekdays(start, end, holidays: null));
    }

    [Fact]
    public void CountWeekdays_ExcludesWeekdayHoliday()
    {
        var start = new DateTime(2026, 3, 16);
        var end = new DateTime(2026, 3, 20);
        var holidays = new HashSet<DateTime> { new DateTime(2026, 3, 20) };

        var without = PayrollWorkingDaysCounter.CountWeekdays(start, end);
        var withHoliday = PayrollWorkingDaysCounter.CountWeekdays(start, end, holidays);

        Assert.Equal(5, without);
        Assert.Equal(4, withHoliday);
    }

    [Fact]
    public void CountWeekdays_IgnoresWeekendHoliday()
    {
        var start = new DateTime(2026, 3, 16);
        var end = new DateTime(2026, 3, 20);
        var holidays = new HashSet<DateTime> { new DateTime(2026, 3, 14) };

        Assert.Equal(5, PayrollWorkingDaysCounter.CountWeekdays(start, end, holidays));
    }

    [Fact]
    public void CountWeekdaysInMonth_ExcludesHolidaysInMonth()
    {
        var holidays = new HashSet<DateTime> { new DateTime(2026, 3, 20) };

        var baseline = PayrollWorkingDaysCounter.CountWeekdaysInMonth(2026, 3);
        var adjusted = PayrollWorkingDaysCounter.CountWeekdaysInMonth(2026, 3, holidays);

        Assert.Equal(baseline - 1, adjusted);
    }

    [Fact]
    public void CountInMonth_ExcludesHolidayInRange()
    {
        var holidays = new HashSet<DateTime> { new DateTime(2026, 3, 20) };

        var count = PayrollWorkingDaysCounter.CountInMonth(
            2026, 3,
            new DateTime(2026, 3, 16),
            new DateTime(2026, 3, 20),
            holidays);

        var expected = PayrollWorkingDaysCounter.CountWeekdays(
            new DateTime(2026, 3, 16),
            new DateTime(2026, 3, 20),
            holidays);

        Assert.Equal(expected, count);
        Assert.Equal(4, count);
    }
}
