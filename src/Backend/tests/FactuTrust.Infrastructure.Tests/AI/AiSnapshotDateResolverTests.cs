using System;
using FactuTrust.Application.Features.AI;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class AiSnapshotDateResolverTests
{
    private static readonly DateOnly Today = new(2026, 6, 16);
    private static DateTime TodayDt => Today.ToDateTime(TimeOnly.MinValue);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("pas une date")]
    [InlineData("2026-13-99")]
    public void MissingOrInvalid_DefaultsToToday(string? raw)
    {
        Assert.Equal(TodayDt, AiSnapshotDateResolver.Resolve(raw, Today));
    }

    [Fact]
    public void FutureDate_ClampsToToday()
    {
        Assert.Equal(TodayDt, AiSnapshotDateResolver.Resolve("2026-06-21", Today));
    }

    [Fact]
    public void ValidPastIsoDate_IsPreserved()
    {
        Assert.Equal(new DateTime(2026, 6, 6), AiSnapshotDateResolver.Resolve("2026-06-06", Today));
    }

    [Fact]
    public void FrenchDateFormat_IsAccepted()
    {
        Assert.Equal(new DateTime(2026, 6, 6), AiSnapshotDateResolver.Resolve("06/06/2026", Today));
    }

    [Fact]
    public void IsoDateTimeFormat_IsAcceptedAndTruncatedToDate()
    {
        Assert.Equal(new DateTime(2026, 6, 6), AiSnapshotDateResolver.Resolve("2026-06-06T14:30:00", Today));
    }

    [Fact]
    public void Today_ReturnsToday()
    {
        Assert.Equal(TodayDt, AiSnapshotDateResolver.Resolve("2026-06-16", Today));
    }
}
