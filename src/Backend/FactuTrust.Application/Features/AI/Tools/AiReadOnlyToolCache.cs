using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FactuTrust.Application.Common;
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
    bool TryGet(string tenantKey, string toolName, Dictionary<string, object?> arguments, out AiToolResult cached, FirmDossierAccessScope? scope = null);
    void Set(string tenantKey, string toolName, Dictionary<string, object?> arguments, AiToolResult result, FirmDossierAccessScope? scope = null);
    bool IsCacheable(string toolName, FirmDossierAccessScope? scope = null);
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

    /// <summary>
    /// Outil firm en lecture seule : éligible uniquement si le flag DÉDIÉ
    /// <see cref="OllamaSettings.FirmMissionReadOnlyToolCacheEnabled"/> est actif ET que le
    /// <paramref name="scope"/> est fourni ET correspond à <c>FirmManager</c>
    /// (<see cref="FirmDossierAccessScope.RequiresAccountantAssignmentFilter"/> == <c>false</c>) —
    /// <c>FirmAccountant</c> n'est JAMAIS caché inter-requêtes (ACL par affectation : une révocation
    /// resterait visible jusqu'au TTL). <c>send_fiscal_deadline_reminder</c> n'est pas dans la liste
    /// firm read-only ⇒ jamais cacheable. Outil tenant : régi par <see cref="OllamaSettings.EnableReadOnlyToolCache"/>.
    /// </summary>
    public bool IsCacheable(string toolName, FirmDossierAccessScope? scope = null)
    {
        if (AiParallelDbToolPolicy.IsFirmReadOnly(toolName))
        {
            return _ollamaSettings.FirmMissionReadOnlyToolCacheEnabled
                && scope is { } s
                && !s.RequiresAccountantAssignmentFilter;
        }

        return _ollamaSettings.EnableReadOnlyToolCache && AiParallelDbToolPolicy.IsSafe(toolName);
    }

    public bool TryGet(string tenantKey, string toolName, Dictionary<string, object?> arguments, out AiToolResult cached, FirmDossierAccessScope? scope = null)
    {
        cached = default!;
        if (!IsCacheable(toolName, scope))
            return false;

        var key = BuildKey(tenantKey, toolName, arguments, scope);
        if (_memoryCache.TryGetValue<AiToolResult>(key, out var hit) && hit is not null)
        {
            cached = hit;
            return true;
        }

        return false;
    }

    public void Set(string tenantKey, string toolName, Dictionary<string, object?> arguments, AiToolResult result, FirmDossierAccessScope? scope = null)
    {
        if (!IsCacheable(toolName, scope) || !result.Success)
            return;

        var ttlSeconds = Math.Clamp(_ollamaSettings.ReadOnlyToolCacheSeconds, 5, 300);
        var key = BuildKey(tenantKey, toolName, arguments, scope);
        _memoryCache.Set(key, result, TimeSpan.FromSeconds(ttlSeconds));
    }

    private static string BuildKey(string tenantKey, string toolName, Dictionary<string, object?> arguments, FirmDossierAccessScope? scope = null)
    {
        var canonical = new SortedDictionary<string, object?>(arguments ?? new(), StringComparer.Ordinal);
        var json = JsonSerializer.Serialize(canonical);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        var hash = Convert.ToHexString(bytes);

        // Lot 3.3 — clé user-scopée pour les outils firm : "{TenantId}:{UserId}:{Role}" (alignée sur
        // FirmDossierAccessScope qui ne porte que UserId + Role). Aucun hit croisé entre deux utilisateurs
        // du même cabinet. Les outils tenant conservent la clé historique "{TenantId}:...". Pas de
        // collision : un outil firm a toujours un scope, un outil tenant jamais (clés de structures différentes).
        if (scope is { } s && AiParallelDbToolPolicy.IsFirmReadOnly(toolName))
            return string.Concat(CachePrefix, tenantKey, ":", s.UserId, ":", s.Role, ":", toolName, ":", hash);

        return string.Concat(CachePrefix, tenantKey, ":", toolName, ":", hash);
    }
}