using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.AI;
using FactuTrust.Application.Features.AI.DTOs;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services.AI;

public sealed class OllamaHttpClient : IOllamaClient
{
    private const string ModelListCacheKey = "factutrust:ollama:tags";
    private const string AvailabilityCacheKey = "factutrust:ollama:available";
    private const string ModelShowCachePrefix = "factutrust:ollama:show:";
    private const string ProcessListCacheKey = "factutrust:ollama:ps";

    private readonly HttpClient _httpClient;
    private readonly OllamaSettings _settings;
    private readonly ILogger<OllamaHttpClient> _logger;
    private readonly IMemoryCache _memoryCache;
    private readonly OllamaGenerationGate _generationGate;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public OllamaHttpClient(
        HttpClient httpClient,
        IOptions<OllamaSettings> settings,
        ILogger<OllamaHttpClient> logger,
        IMemoryCache memoryCache,
        OllamaGenerationGate generationGate)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
        _memoryCache = memoryCache;
        _generationGate = generationGate;
    }

    public async IAsyncEnumerable<OllamaChatChunk> StreamChatAsync(
        OllamaChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default,
        TimeSpan? streamReadTimeout = null,
        bool useGenerationGate = true)
    {
        // Sérialise les générations Ollama concurrentes (protège un moteur mono-instance / CPU).
        // Le chat acquiert la porte en amont pour exposer la durée d'attente (SSE) ; l'import conserve ce chemin.
        using var gateLease = useGenerationGate
            ? await _generationGate.AcquireAsync(cancellationToken)
            : null;

        var jsonContent = JsonSerializer.Serialize(request, JsonOptions);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            InvalidateAvailabilityCache();
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Ollama POST /api/chat failed: {StatusCode} RequestUri={Uri} Body={Body}",
                (int)response.StatusCode,
                response.RequestMessage?.RequestUri,
                body);
            var userMessage = BuildChatErrorUserMessage(response.StatusCode, body);
            throw new OllamaRequestException(userMessage, body, null, (int)response.StatusCode);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        // Import : budget total (paramètre fourni). Chat : délai d'inactivité par fragment (config).
        var totalBudgetSw = streamReadTimeout.HasValue ? System.Diagnostics.Stopwatch.StartNew() : null;
        TimeSpan? inactivityTimeout = streamReadTimeout.HasValue
            ? null
            : (_settings.ChatStreamInactivityTimeoutSeconds > 0
                ? TimeSpan.FromSeconds(_settings.ChatStreamInactivityTimeoutSeconds)
                : null);

        // Le délai d'inactivité ne s'applique qu'APRÈS le premier fragment : la latence initiale
        // (chargement du modèle + évaluation du prompt, potentiellement longue sur CPU) reste régie
        // par le délai HTTP global et la gestion gracieuse côté contrôleur, jamais faussement coupée.
        var firstChunkSeen = false;

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            if (totalBudgetSw is not null && totalBudgetSw.Elapsed > streamReadTimeout!.Value)
            {
                throw new OllamaRequestException(
                    "L'analyse IA a dépassé le délai maximal. Le modèle est peut-être en cours de chargement "
                    + "ou la mémoire est insuffisante — réessayez ou choisissez un modèle plus léger.",
                    null,
                    null,
                    408);
            }

            string? line;
            if (inactivityTimeout is not null && firstChunkSeen)
            {
                // Délai mesuré entre deux fragments : remis à zéro à chaque lecture (vraie inactivité,
                // pas un plafond global). Un dépassement => 408 explicite, jamais une coupure silencieuse.
                using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                readCts.CancelAfter(inactivityTimeout.Value);
                try
                {
                    line = await reader.ReadLineAsync(readCts.Token);
                }
                catch (OperationCanceledException) when (readCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    throw new OllamaRequestException(
                        "Le service IA n'a renvoyé aucune réponse dans le délai imparti. Le modèle est peut-être "
                        + "surchargé ou en cours de chargement — réessayez dans un instant ou choisissez un modèle plus léger.",
                        null,
                        null,
                        408);
                }
            }
            else
            {
                line = await reader.ReadLineAsync(cancellationToken);
            }

            if (string.IsNullOrWhiteSpace(line))
                continue;

            OllamaChatChunk? chunk;
            try
            {
                chunk = JsonSerializer.Deserialize<OllamaChatChunk>(line, JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse Ollama chunk: {Line}", line);
                continue;
            }

            if (chunk is not null)
            {
                yield return chunk;
                firstChunkSeen = true;
            }

            if (chunk?.Done == true)
                yield break;
        }
    }

    public async Task<IReadOnlyList<OllamaModelInfo>> ListModelsAsync(
        CancellationToken cancellationToken = default)
    {
        var listTtl = TimeSpan.FromSeconds(Math.Clamp(_settings.ModelListCacheSeconds, 5, 600));
        return await _memoryCache.GetOrCreateAsync(ModelListCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = listTtl;
            try
            {
                var response = await _httpClient.GetAsync("/api/tags", cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("Ollama GET /api/tags failed: {Status} {Body}", (int)response.StatusCode, body);
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(5);
                    return (IReadOnlyList<OllamaModelInfo>)new List<OllamaModelInfo>();
                }

                var result = await response.Content.ReadFromJsonAsync<OllamaModelListResponse>(JsonOptions, cancellationToken);
                return (IReadOnlyList<OllamaModelInfo>)(result?.Models ?? new List<OllamaModelInfo>());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to list Ollama models");
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(5);
                return (IReadOnlyList<OllamaModelInfo>)new List<OllamaModelInfo>();
            }
        }) ?? Array.Empty<OllamaModelInfo>();
    }

    public async Task<bool> IsModelInstalledAsync(string modelName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelName))
            return false;

        var models = await ListModelsAsync(cancellationToken);
        if (models.Count == 0)
            return true;

        foreach (var m in models)
        {
            if (ModelNameMatches(modelName, m.Name))
                return true;
        }

        return false;
    }

    public async Task<bool> WarmUpModelAsync(
        string modelName,
        string? keepAlive = null,
        CancellationToken cancellationToken = default,
        int? numCtx = null,
        int? numBatch = null,
        OllamaInferenceProfile? inferenceProfile = null)
    {
        if (string.IsNullOrWhiteSpace(modelName))
            return false;

        var request = new OllamaChatRequest
        {
            Model = modelName.Trim(),
            Messages = new List<OllamaChatMessage>
            {
                new()
                {
                    Role = "user",
                    Content = "ping"
                }
            },
            Stream = false,
            KeepAlive = keepAlive,
            Options = (inferenceProfile?.ApplyTo(new OllamaOptions
            {
                NumPredict = 1,
                // Précharger avec les MÊMES options de chargement que le chat (num_ctx ET num_batch) → Ollama
                // réutilise exactement la même instance (aucun rechargement au 1ᵉʳ message). num_ctx/num_batch
                // sont ignorés à la sérialisation s'ils sont null (comportement historique préservé pour l'import).
                NumCtx = numCtx,
                NumBatch = numBatch
            }) ?? new OllamaOptions
            {
                NumPredict = 1,
                NumCtx = numCtx,
                NumBatch = numBatch
            })
        };

        var jsonContent = JsonSerializer.Serialize(request, JsonOptions);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
        };

        try
        {
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                InvalidateAvailabilityCache();
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Ollama warm-up failed: status={StatusCode} model={Model} body={Body}",
                    (int)response.StatusCode,
                    modelName,
                    body);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            InvalidateAvailabilityCache();
            _logger.LogDebug(ex, "Ollama warm-up request failed for model={Model}", modelName);
            return false;
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        var ttl = TimeSpan.FromSeconds(Math.Clamp(_settings.AvailabilityCacheSeconds, 0, 120));
        if (ttl <= TimeSpan.Zero)
            return await ProbeAvailabilityAsync(cancellationToken);

        if (_memoryCache.TryGetValue(AvailabilityCacheKey, out bool cachedOk))
            return cachedOk;

        var ok = await ProbeAvailabilityAsync(cancellationToken);
        if (ok)
        {
            _memoryCache.Set(
                AvailabilityCacheKey,
                true,
                new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl });
        }

        return ok;
    }

    public async Task<OllamaModelShowResponse?> ShowModelAsync(
        string modelName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelName))
            return null;

        var cacheKey = $"{ModelShowCachePrefix}{modelName.Trim().ToLowerInvariant()}";
        var showTtl = TimeSpan.FromSeconds(Math.Clamp(_settings.ModelListCacheSeconds, 30, 600));

        return await _memoryCache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = showTtl;
            try
            {
                var payload = JsonSerializer.Serialize(new { name = modelName.Trim() }, JsonOptions);
                using var request = new HttpRequestMessage(HttpMethod.Post, "/api/show")
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };
                using var response = await _httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("Ollama POST /api/show failed for {Model}: {Status}",
                        modelName, (int)response.StatusCode);
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10);
                    return (OllamaModelShowResponse?)null;
                }

                return await response.Content.ReadFromJsonAsync<OllamaModelShowResponse>(JsonOptions, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to show Ollama model {Model}", modelName);
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10);
                return null;
            }
        });
    }

    public async Task<OllamaProcessListResponse?> ListRunningModelsAsync(
        CancellationToken cancellationToken = default)
    {
        var psTtl = TimeSpan.FromSeconds(30);

        return await _memoryCache.GetOrCreateAsync(ProcessListCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = psTtl;
            try
            {
                var response = await _httpClient.GetAsync("/api/ps", cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("Ollama GET /api/ps failed: {Status}", (int)response.StatusCode);
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(5);
                    return (OllamaProcessListResponse?)null;
                }

                return await response.Content.ReadFromJsonAsync<OllamaProcessListResponse>(JsonOptions, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to list running Ollama models");
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(5);
                return null;
            }
        });
    }

    private async Task<bool> ProbeAvailabilityAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/tags", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Ollama health check failed");
            return false;
        }
    }

    private void InvalidateAvailabilityCache()
    {
        _memoryCache.Remove(AvailabilityCacheKey);
    }

    private static bool ModelNameMatches(string requested, string installedName)
    {
        requested = requested.Trim();
        installedName = installedName.Trim();
        if (string.Equals(requested, installedName, StringComparison.OrdinalIgnoreCase))
            return true;

        var reqBase = requested.Split(':')[0];
        var insBase = installedName.Split(':')[0];
        return string.Equals(reqBase, insBase, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildChatErrorUserMessage(System.Net.HttpStatusCode statusCode, string body)
    {
        var ollamaError = TryExtractOllamaErrorJson(body);
        if (statusCode == System.Net.HttpStatusCode.NotFound)
        {
            if (!string.IsNullOrEmpty(ollamaError))
                return $"Modèle introuvable ou non installé : {ollamaError} Utilisez « ollama pull <nom> » ou choisissez un modèle installé.";
            return "Modèle introuvable ou non installé dans Ollama. Exécutez « ollama pull <nom> » ou sélectionnez un modèle disponible.";
        }

        if (!string.IsNullOrEmpty(ollamaError))
            return $"Erreur Ollama : {ollamaError}";

        return "Le moteur IA a renvoyé une erreur. Vérifiez qu’Ollama est à jour et que le modèle est disponible.";
    }

    private static string? TryExtractOllamaErrorJson(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err))
                return err.GetString();
        }
        catch (JsonException)
        {
            // ignore
        }

        return null;
    }
}
