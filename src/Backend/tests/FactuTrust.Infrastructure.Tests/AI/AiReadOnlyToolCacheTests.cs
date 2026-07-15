using System.Collections.Generic;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Application.Features.AI.Tools;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class AiReadOnlyToolCacheTests
{
    private static AiReadOnlyToolCache CreateCache(bool enabled = true) =>
        new(new MemoryCache(new MemoryCacheOptions()), Options.Create(new OllamaSettings
        {
            EnableReadOnlyToolCache = enabled,
            ReadOnlyToolCacheSeconds = 45
        }));

    [Fact]
    public void IsCacheable_ReadOnlyDbTool_ReturnsTrue()
    {
        var cache = CreateCache();
        Assert.True(cache.IsCacheable("get_sales_revenue"));
        Assert.False(cache.IsCacheable("create_product"));
    }

    [Fact]
    public void TryGet_AfterSet_ReturnsHitForSameTenantAndArgs()
    {
        var cache = CreateCache();
        var args = new Dictionary<string, object?> { ["from_date"] = "2026-03-01", ["to_date"] = "2026-03-31" };
        var result = AiToolResult.Ok("{\"rows\":[]}");

        cache.Set("tenant-a", "get_sales_revenue", args, result);

        Assert.True(cache.TryGet("tenant-a", "get_sales_revenue", args, out var hit));
        Assert.True(hit.Success);
        Assert.False(cache.TryGet("tenant-b", "get_sales_revenue", args, out _));
    }

    [Fact]
    public void IsCacheable_WhenDisabled_ReturnsFalse()
    {
        var cache = CreateCache(enabled: false);
        Assert.False(cache.IsCacheable("get_sales_revenue"));
    }
}