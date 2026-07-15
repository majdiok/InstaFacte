using System.Text.Json;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.AI.DTOs;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.AI;

/// <summary>Optional pre-LLM enrichment for screen analysis via deterministic tool calls.</summary>
public sealed class AiScreenAnalysisEnricher
{
    private const int EnrichmentCacheTtlSeconds = 15;
    private const string EnrichmentCachePrefix = "factutrust:ai:enrichment:";

    private readonly IAiToolExecutor _toolExecutor;
    private readonly ScreenAnalysisOptions _options;
    private readonly ILogger<AiScreenAnalysisEnricher> _logger;
    private readonly IMemoryCache? _memoryCache;
    private readonly ITenantContext? _tenantContext;

    public AiScreenAnalysisEnricher(
        IAiToolExecutor toolExecutor,
        IOptions<ScreenAnalysisOptions> options,
        ILogger<AiScreenAnalysisEnricher> logger,
        IMemoryCache? memoryCache = null,
        ITenantContext? tenantContext = null)
    {
        _toolExecutor = toolExecutor;
        _options = options.Value;
        _logger = logger;
        _memoryCache = memoryCache;
        _tenantContext = tenantContext;
    }

    public async Task<string?> TryEnrichAsync(
        ChatUiContextDto? uiContext,
        string? correlationId,
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled || !_options.ServerEnrichmentEnabled)
            return null;

        var screenId = AiScreenAnalysisModeDetector.ResolveScreenId(uiContext);
        if (string.IsNullOrWhiteSpace(screenId))
            return null;

        var toolName = ResolveEnrichmentTool(screenId);
        if (toolName is null)
            return null;

        try
        {
            var args = BuildToolArguments(screenId, uiContext?.AnalysisSummary);

            // Cache court (15 s) par (tenant, screen, args) : un même utilisateur qui ouvre
            // plusieurs fois la même page dans la même demi-minute évite N requêtes SQL identiques.
            // TTL volontairement court pour ne pas masquer une modification de données récente.
            string? cacheKey = null;
            if (_memoryCache is not null)
            {
                var tenantId = _tenantContext?.TenantId?.ToString() ?? "-";
                var argsKey = string.Join(',', args.OrderBy(kv => kv.Key, System.StringComparer.Ordinal)
                    .Select(kv => $"{kv.Key}={kv.Value}"));
                cacheKey = $"{EnrichmentCachePrefix}{tenantId}:{screenId}:{toolName}:{argsKey}";
                if (_memoryCache.TryGetValue<string>(cacheKey, out var cachedEnrichment) && !string.IsNullOrEmpty(cachedEnrichment))
                {
                    _logger.LogDebug("Screen analysis enrichment cache HIT tool={Tool} screen={ScreenId}", toolName, screenId);
                    return cachedEnrichment;
                }
            }

            var result = await _toolExecutor.ExecuteAsync(
                toolName,
                args,
                new AiToolExecutionContext(correlationId, conversationId),
                cancellationToken);

            if (!result.Success || string.IsNullOrWhiteSpace(result.Data))
                return null;

            _logger.LogInformation(
                "Screen analysis enrichment tool={Tool} screen={ScreenId} bytes={Bytes}",
                toolName,
                screenId,
                result.Data.Length);

            var enrichment = $"ENRICHISSEMENT SERVEUR ({toolName}) :\n{Truncate(result.Data, 2000)}";
            if (cacheKey is not null && _memoryCache is not null)
            {
                _memoryCache.Set(cacheKey, enrichment, System.TimeSpan.FromSeconds(EnrichmentCacheTtlSeconds));
            }
            return enrichment;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Screen analysis enrichment failed for {ScreenId}", screenId);
            return null;
        }
    }

    private static string? ResolveEnrichmentTool(string screenId) => screenId switch
    {
        "accounting-aging" => "get_client_aging",
        "dashboard" => "get_sales_revenue",
        "cash-desk" => "get_client_payments",
        _ => null
    };

    private static Dictionary<string, object?> BuildToolArguments(string screenId, string? analysisSummary)
    {
        var fiscalYear = DateTime.UtcNow.Year;
        if (!string.IsNullOrWhiteSpace(analysisSummary))
        {
            try
            {
                using var doc = JsonDocument.Parse(analysisSummary);
                var payload = doc.RootElement;
                if (payload.TryGetProperty("payload", out var p))
                {
                    if (p.TryGetProperty("fiscalYear", out var fy) && fy.TryGetInt32(out var y))
                        fiscalYear = y;
                    if (p.TryGetProperty("period", out var period) && period.TryGetProperty("year", out var py) &&
                        py.TryGetInt32(out var y2))
                        fiscalYear = y2;
                }
            }
            catch
            {
                // ignore
            }
        }

        return screenId switch
        {
            "accounting-aging" => new Dictionary<string, object?>(),
            "dashboard" => new Dictionary<string, object?>
            {
                ["preset"] = "current_month",
                ["group_by"] = "None"
            },
            "cash-desk" => new Dictionary<string, object?>
            {
                ["preset"] = "current_month"
            },
            _ => new Dictionary<string, object?>()
        };
    }

    private static string Truncate(string value, int maxChars) =>
        value.Length <= maxChars ? value : value[..maxChars] + "\n... [tronqué]";
}
