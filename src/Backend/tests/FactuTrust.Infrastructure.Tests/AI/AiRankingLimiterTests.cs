using System.Collections.Generic;
using System.Linq;
using FactuTrust.Application.Features.AI;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class AiRankingLimiterTests
{
    private sealed record Row(string Name, decimal Amount);

    private static IReadOnlyList<Row> Rows(params (string Name, decimal Amount)[] items)
        => items.Select(i => new Row(i.Name, i.Amount)).ToList();

    [Fact]
    public void SmallList_NoTopN_ReturnsSameInstanceUntouched()
    {
        var rows = Rows(("A", 10m), ("B", 30m), ("C", 20m));

        var result = AiRankingLimiter.Apply(rows, hasExplicitTopN: false, requestedTopN: 0, defaultCap: 50, r => r.Amount);

        // En deçà du plafond et sans top_n explicite : aucune transformation ni réordonnancement.
        Assert.Same(rows, result);
    }

    [Fact]
    public void EmptyList_ReturnsEmpty()
    {
        var rows = Rows();

        var result = AiRankingLimiter.Apply(rows, hasExplicitTopN: false, requestedTopN: 0, defaultCap: 50, r => r.Amount);

        Assert.Empty(result);
    }

    [Fact]
    public void ExplicitTopN_AlwaysSortsDescending_ThenTakesN()
    {
        var rows = Rows(("A", 10m), ("B", 30m), ("C", 20m), ("D", 40m));

        var result = AiRankingLimiter.Apply(rows, hasExplicitTopN: true, requestedTopN: 2, defaultCap: 50, r => r.Amount);

        Assert.Equal(new[] { "D", "B" }, result.Select(r => r.Name).ToArray());
    }

    [Fact]
    public void ExplicitTopN_GreaterThanCount_ReturnsAllSortedDescending()
    {
        var rows = Rows(("A", 10m), ("B", 30m), ("C", 20m));

        var result = AiRankingLimiter.Apply(rows, hasExplicitTopN: true, requestedTopN: 5, defaultCap: 50, r => r.Amount);

        Assert.Equal(new[] { "B", "C", "A" }, result.Select(r => r.Name).ToArray());
    }

    [Fact]
    public void LargeList_NoTopN_AppliesDefaultCap_SortedDescending()
    {
        var rows = Enumerable.Range(1, 60).Select(i => new Row($"R{i}", i)).ToList();

        var result = AiRankingLimiter.Apply(rows, hasExplicitTopN: false, requestedTopN: 0, defaultCap: 50, r => r.Amount);

        Assert.Equal(50, result.Count);
        Assert.Equal(60m, result[0].Amount); // plus gros montant en tête
        Assert.Equal(11m, result[^1].Amount); // 60..11 = 50 lignes
    }

    [Fact]
    public void ExplicitTopN_AboveMax_IsClampedTo200()
    {
        var rows = Enumerable.Range(1, 300).Select(i => new Row($"R{i}", i)).ToList();

        var result = AiRankingLimiter.Apply(rows, hasExplicitTopN: true, requestedTopN: 1000, defaultCap: 50, r => r.Amount);

        Assert.Equal(AiRankingLimiter.MaxRows, result.Count);
    }

    [Fact]
    public void ExplicitTopN_ZeroOrNegative_IsClampedToOne()
    {
        var rows = Rows(("A", 10m), ("B", 30m), ("C", 20m));

        var result = AiRankingLimiter.Apply(rows, hasExplicitTopN: true, requestedTopN: 0, defaultCap: 50, r => r.Amount);

        Assert.Single(result);
        Assert.Equal("B", result[0].Name);
    }

    [Fact]
    public void NonPositiveDefaultCap_FallsBackTo50()
    {
        var rows = Enumerable.Range(1, 60).Select(i => new Row($"R{i}", i)).ToList();

        var result = AiRankingLimiter.Apply(rows, hasExplicitTopN: false, requestedTopN: 0, defaultCap: 0, r => r.Amount);

        Assert.Equal(AiRankingLimiter.FallbackDefaultCap, result.Count);
    }
}
