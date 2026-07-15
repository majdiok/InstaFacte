using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.AI.DTOs;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.AI.Tools;

/// <summary>
/// Short-lived cache for read-only DB-backed AI tools (same tenant + args within TTL).
/// </summary>
public interface IAiReadOnlyToolCache
{
    bool TryGet(string tenantKey, string toolName, Dictionary<string, object?> arguments, out AiToolResult cached);
    void Set(string tenantKey, string toolName, Dictionary<string, object?> arguments, AiToolResult result);
    bool IsCacheable(string toolName);
}

public sealed class AiReadOnlyToolCache : IAiReadOnlyToolCache
{
    private const string CachePrefix = "factutrust:ai:readonly-tool:";

    private readonly IMemoryCache _memoryCache;
    private readonly OllamaSettings _ollamaSettings;

    public AiReadOnlyToolCache(IMemoryCache memoryCache, IOptions<OllamaSettings> ollamaSettings)
    {
        _memoryCache = memoryCache;
        _ollamaSettings = ollamaSettings.Value;
    }

    public bool IsCacheable(string toolName) =>
        _ollamaSettings.EnableReadOnlyToolCache && AiParallelDbToolPolicy.IsSafe(toolName);

    public bool TryGet(string tenantKey, string toolName, Dictionary<string, object?> arguments, out AiToolResult cached)
    {
        cached = default!;
        if (!IsCacheable(toolName))
            return false;

        var key = BuildKey(tenantKey, toolName, arguments);
        if (_memoryCache.TryGetValue<AiToolResult>(key, out var hit) && hit is not null)
        {
            cached = hit;
            return true;
        }

        return false;
    }

    public void Set(string tenantKey, string toolName, Dictionary<string, object?> arguments, AiToolResult result)
    {
        if (!IsCacheable(toolName) || !result.Success)
            return;

        var ttlSeconds = Math.Clamp(_ollamaSettings.ReadOnlyToolCacheSeconds, 5, 300);
        var key = BuildKey(tenantKey, toolName, arguments);
        _memoryCache.Set(key, result, TimeSpan.FromSeconds(ttlSeconds));
    }

    private static string BuildKey(string tenantKey, string toolName, Dictionary<string, object?> arguments)
    {
        var canonical = new SortedDictionary<string, object?>(arguments ?? new(), StringComparer.Ordinal);
        var json = JsonSerializer.Serialize(canonical);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        var hash = Convert.ToHexString(bytes);
        return string.Concat(CachePrefix, tenantKey, ":", toolName, ":", hash);
    }
}