using System.Collections.Generic;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Application.Features.AI.Tools;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class AiDeterministicToolCacheTests
{
    private static AiDeterministicToolCache CreateCache(bool enabled = true)
    {
        var memory = new MemoryCache(new MemoryCacheOptions());
        var settings = Options.Create(new OllamaSettings { EnableDeterministicToolCache = enabled });
        return new AiDeterministicToolCache(memory, settings);
    }

    [Fact]
    public void IsCacheable_WhitelistedTool_ReturnsTrue()
    {
        var cache = CreateCache();
        Assert.True(cache.IsCacheable("resolve_reporting_period"));
        Assert.True(cache.IsCacheable("get_tunisian_commercial_calendar"));
    }

    [Fact]
    public void IsCacheable_NonWhitelistedTool_ReturnsFalse()
    {
        var cache = CreateCache();
        Assert.False(cache.IsCacheable("get_sales_revenue"));
        Assert.False(cache.IsCacheable("create_client"));
        Assert.False(cache.IsCacheable(""));
    }

    [Fact]
    public void IsCacheable_WhenDisabled_AlwaysFalse()
    {
        var cache = CreateCache(enabled: false);
        Assert.False(cache.IsCacheable("resolve_reporting_period"));
        Assert.False(cache.IsCacheable("get_tunisian_commercial_calendar"));
    }

    [Fact]
    public void TryGet_AfterSet_ReturnsHit()
    {
        var cache = CreateCache();
        var args = new Dictionary<string, object?> { ["preset"] = "current_month" };
        var result = AiToolResult.Ok("{\"label\":\"Mai 2026\"}");

        cache.Set("resolve_reporting_period", args, result);

        Assert.True(cache.TryGet("resolve_reporting_period", args, out var hit));
        Assert.Equal(result.Data, hit.Data);
        Assert.True(hit.Success);
    }

    [Fact]
    public void TryGet_DifferentArgs_MissesEvenIfToolMatches()
    {
        var cache = CreateCache();
        cache.Set(
            "resolve_reporting_period",
            new Dictionary<string, object?> { ["preset"] = "current_month" },
            AiToolResult.Ok("{}"));

        Assert.False(cache.TryGet(
            "resolve_reporting_period",
            new Dictionary<string, object?> { ["preset"] = "last_month" },
            out _));
    }

    [Fact]
    public void TryGet_ArgsOrderInsensitive()
    {
        var cache = CreateCache();
        cache.Set(
            "get_tunisian_commercial_calendar",
            new Dictionary<string, object?> { ["a"] = 1, ["b"] = 2 },
            AiToolResult.Ok("{}"));

        // Mêmes args dans un autre ordre → doit hit.
        Assert.True(cache.TryGet(
            "get_tunisian_commercial_calendar",
            new Dictionary<string, object?> { ["b"] = 2, ["a"] = 1 },
            out _));
    }

    [Fact]
    public void Set_FailedResult_NotCached()
    {
        var cache = CreateCache();
        var args = new Dictionary<string, object?>();

        cache.Set("resolve_reporting_period", args, AiToolResult.Error("invalid preset"));

        Assert.False(cache.TryGet("resolve_reporting_period", args, out _));
    }

    [Fact]
    public void Set_NonWhitelistedTool_NoOp()
    {
        var cache = CreateCache();
        var args = new Dictionary<string, object?> { ["from_date"] = "2026-01-01" };

        cache.Set("get_sales_revenue", args, AiToolResult.Ok("{}"));

        Assert.False(cache.TryGet("get_sales_revenue", args, out _));
    }
}
