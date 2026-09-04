using FactuTrust.Application.Features.AI;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class ReportingPeriodResolverTests
{
    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public FakeTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void AdvanceUtc(DateTimeOffset utc) => _utcNow = utc;
    }

    [Fact]
    public void GetLastCompletedQuarterRange_April_Returns_PreviousCalendarQuarter()
    {
        var today = new DateOnly(2026, 4, 7);
        var (from, to) = ReportingPeriodResolver.GetLastCompletedQuarterRange(today);
        Assert.Equal(new DateOnly(2026, 1, 1), from);
        Assert.Equal(new DateOnly(2026, 3, 31), to);
    }

    [Fact]
    public void GetLastCompletedQuarterRange_February_Returns_Q4_PreviousYear()
    {
        var today = new DateOnly(2026, 2, 15);
        var (from, to) = ReportingPeriodResolver.GetLastCompletedQuarterRange(today);
        Assert.Equal(new DateOnly(2025, 10, 1), from);
        Assert.Equal(new DateOnly(2025, 12, 31), to);
    }

    [Fact]
    public void GetLastCompletedQuarterRange_January_Returns_Q4_PreviousYear()
    {
        var today = new DateOnly(2026, 1, 20);
        var (from, to) = ReportingPeriodResolver.GetLastCompletedQuarterRange(today);
        Assert.Equal(new DateOnly(2025, 10, 1), from);
        Assert.Equal(new DateOnly(2025, 12, 31), to);
    }

    [Fact]
    public void Resolve_LastCompletedQuarter_UsesTunisiaClock()
    {
        var utc = new DateTimeOffset(2026, 4, 7, 10, 0, 0, TimeSpan.Zero);
        var fake = new FakeTimeProvider(utc);
        var r = ReportingPeriodResolver.Resolve(ReportingPeriodResolver.PresetLastCompletedQuarter, fake);
        Assert.Equal(new DateOnly(2026, 1, 1), r.FromDate);
        Assert.Equal(new DateOnly(2026, 3, 31), r.ToDate);
    }

    [Fact]
    public void Resolve_CurrentQuarter_StartsAtFirstDayOfQuarter()
    {
        var utc = new DateTimeOffset(2026, 4, 7, 10, 0, 0, TimeSpan.Zero);
        var fake = new FakeTimeProvider(utc);
        var r = ReportingPeriodResolver.Resolve(ReportingPeriodResolver.PresetCurrentQuarter, fake);
        Assert.Equal(new DateOnly(2026, 4, 1), r.FromDate);
        Assert.Equal(new DateOnly(2026, 4, 7), r.ToDate);
    }

    [Fact]
    public void Resolve_Last30Days_Returns_30DayInclusiveWindow()
    {
        var utc = new DateTimeOffset(2026, 4, 7, 10, 0, 0, TimeSpan.Zero);
        var fake = new FakeTimeProvider(utc);
        var r = ReportingPeriodResolver.Resolve(ReportingPeriodResolver.PresetLast30Days, fake);
        Assert.Equal(new DateOnly(2026, 3, 9), r.FromDate);
        Assert.Equal(new DateOnly(2026, 4, 7), r.ToDate);
    }

    [Fact]
    public void Resolve_YearToDate_Starts_January_First()
    {
        var utc = new DateTimeOffset(2026, 4, 7, 10, 0, 0, TimeSpan.Zero);
        var fake = new FakeTimeProvider(utc);
        var r = ReportingPeriodResolver.Resolve(ReportingPeriodResolver.PresetYearToDate, fake);
        Assert.Equal(new DateOnly(2026, 1, 1), r.FromDate);
        Assert.Equal(new DateOnly(2026, 4, 7), r.ToDate);
    }

    [Fact]
    public void Resolve_InvalidPreset_ThrowsArgumentException()
    {
        var fake = new FakeTimeProvider(DateTimeOffset.UtcNow);
        Assert.Throws<ArgumentException>(() =>
            ReportingPeriodResolver.Resolve("invalid_preset", fake));
    }

    [Fact]
    public void Resolve_Today_Returns_SingleDay()
    {
        var utc = new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero);
        var fake = new FakeTimeProvider(utc);
        var r = ReportingPeriodResolver.Resolve(ReportingPeriodResolver.PresetToday, fake);
        Assert.Equal(new DateOnly(2026, 6, 3), r.FromDate);
        Assert.Equal(new DateOnly(2026, 6, 3), r.ToDate);
    }

    [Fact]
    public void Resolve_Yesterday_Returns_PreviousDay()
    {
        var utc = new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero);
        var fake = new FakeTimeProvider(utc);
        var r = ReportingPeriodResolver.Resolve(ReportingPeriodResolver.PresetYesterday, fake);
        Assert.Equal(new DateOnly(2026, 6, 2), r.FromDate);
        Assert.Equal(new DateOnly(2026, 6, 2), r.ToDate);
    }

    [Fact]
    public void Resolve_Today_UsesTunisiaCalendar_AcrossMidnight()
    {
        // 23:30 UTC le 02/06 = 00:30 le 03/06 en Afrique/Tunis (UTC+1) → "aujourd'hui" = 03/06.
        var utc = new DateTimeOffset(2026, 6, 2, 23, 30, 0, TimeSpan.Zero);
        var fake = new FakeTimeProvider(utc);
        var r = ReportingPeriodResolver.Resolve(ReportingPeriodResolver.PresetToday, fake);
        Assert.Equal(new DateOnly(2026, 6, 3), r.FromDate);
        Assert.Equal(new DateOnly(2026, 6, 3), r.ToDate);
    }

    [Fact]
    public void ValidPresets_Contains_Today_And_Yesterday()
    {
        Assert.Contains(ReportingPeriodResolver.PresetToday, ReportingPeriodResolver.ValidPresets);
        Assert.Contains(ReportingPeriodResolver.PresetYesterday, ReportingPeriodResolver.ValidPresets);
    }

    [Fact]
    public void LastFiveYears_Spans_The_Current_Year_And_The_Four_Before()
    {
        // Fenêtre indispensable à un état regroupé PAR ANNÉE : « année en cours » n'en rendrait
        // qu'une seule ligne.
        var fake = new FakeTimeProvider(new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero));
        var r = ReportingPeriodResolver.Resolve(ReportingPeriodResolver.PresetLastFiveYears, fake);

        Assert.Equal(new DateOnly(2022, 1, 1), r.FromDate);
        Assert.Equal(new DateOnly(2026, 6, 3), r.ToDate);
        Assert.Contains("2022", r.Label, StringComparison.Ordinal);
        Assert.Contains(ReportingPeriodResolver.PresetLastFiveYears, ReportingPeriodResolver.ValidPresets);
    }
}
