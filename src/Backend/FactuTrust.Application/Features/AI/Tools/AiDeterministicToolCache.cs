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
/// Cache très court (60 s par défaut) pour les outils purement déterministes :
/// même entrée → même sortie, et les valeurs ne dépendent que de l'instant courant
/// (qui change peu sur 60 s, ex. "aujourd'hui en Tunisie"). Utilisé par
/// <c>AiToolExecutor</c> pour éviter de recalculer des résultats identiques pendant
/// un même tour d'agent (le modèle appelle parfois le même outil avec les mêmes args).
///
/// Le cache est volontairement minimal : il est désactivable via
/// <c>OllamaSettings.EnableDeterministicToolCache</c>, ne couvre qu'une whitelist stricte,
/// et expire à 60 s pour éviter qu'un résultat ne dépasse une frontière journalière.
/// </summary>
public interface IAiDeterministicToolCache
{
    bool TryGet(string toolName, Dictionary<string, object?> arguments, out AiToolResult cached);
    void Set(string toolName, Dictionary<string, object?> arguments, AiToolResult result);
    bool IsCacheable(string toolName);
}

public sealed class AiDeterministicToolCache : IAiDeterministicToolCache
{
    private static readonly HashSet<string> WhitelistedTools = new(System.StringComparer.OrdinalIgnoreCase)
    {
        "resolve_reporting_period",
        "get_tunisian_commercial_calendar"
    };

    private const int CacheTtlSeconds = 60;
    private const string CachePrefix = "factutrust:ai:tool-cache:";

    private readonly IMemoryCache _memoryCache;
    private readonly OllamaSettings _ollamaSettings;

    public AiDeterministicToolCache(IMemoryCache memoryCache, IOptions<OllamaSettings> ollamaSettings)
    {
        _memoryCache = memoryCache;
        _ollamaSettings = ollamaSettings.Value;
    }

    public bool IsCacheable(string toolName) =>
        _ollamaSettings.EnableDeterministicToolCache && WhitelistedTools.Contains(toolName);

    public bool TryGet(string toolName, Dictionary<string, object?> arguments, out AiToolResult cached)
    {
        cached = default!;
        if (!IsCacheable(toolName))
            return false;

        var key = BuildKey(toolName, arguments);
        if (_memoryCache.TryGetValue<AiToolResult>(key, out var hit) && hit is not null)
        {
            cached = hit;
            return true;
        }
        return false;
    }

    public void Set(string toolName, Dictionary<string, object?> arguments, AiToolResult result)
    {
        if (!IsCacheable(toolName) || !result.Success)
            return;

        var key = BuildKey(toolName, arguments);
        _memoryCache.Set(key, result, System.TimeSpan.FromSeconds(CacheTtlSeconds));
    }

    private static string BuildKey(string toolName, Dictionary<string, object?> arguments)
    {
        // Sérialisation canonique : tri par clé pour que l'ordre n'affecte pas l'identité.
        var canonical = new SortedDictionary<string, object?>(arguments ?? new(), System.StringComparer.Ordinal);
        var json = JsonSerializer.Serialize(canonical);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        var hash = System.Convert.ToHexString(bytes);
        return string.Concat(CachePrefix, toolName, ":", hash);
    }
}
