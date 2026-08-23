using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.AI.DTOs;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services.AI;

public sealed class OpenAiChatCompletionsClient : IOpenAiChatCompletionsClient
{
    private const string ModelListCacheKeyPrefix = "factutrust:openrouter:models:";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OpenRouterSettings _settings;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<OpenAiChatCompletionsClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public OpenAiChatCompletionsClient(
        IHttpClientFactory httpClientFactory,
        IOptions<OpenRouterSettings> settings,
        IMemoryCache memoryCache,
        ILogger<OpenAiChatCompletionsClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async IAsyncEnumerable<OllamaChatChunk> StreamChatAsOllamaCompatibleAsync(
        string baseUrl,
        string apiKey,
        string model,
        IReadOnlyList<OpenAiChatMessagePayload> messages,
        IReadOnlyList<OllamaToolDefinition> tools,
        double temperature,
        int maxTokens,
        [EnumeratorCancellation] CancellationToken cancellationToken = default,
        int? seed = null,
        OpenAiCompatibleCallOptions? options = null)
    {
        var url = $"{baseUrl.TrimEnd('/')}/chat/completions";
        var omitEmptyTools = options?.OmitEmptyTools == true;
        var body = new ChatCompletionRequest
        {
            Model = model,
            Messages = messages.ToList(),
            Tools = tools.Count == 0 && omitEmptyTools ? null : tools.ToList(),
            Stream = true,
            Temperature = temperature,
            MaxTokens = maxTokens,
            Seed = seed,
            ReasoningEffort = string.IsNullOrWhiteSpace(options?.ReasoningEffort) ? null : options.ReasoningEffort
        };

        var json = JsonSerializer.Serialize(body, JsonOptions);
        var extraRetries = options?.RetryOnServiceUnavailable == true
            ? Math.Max(0, options.ColdStartRetries)
            : 0;
        var attempts = 1 + extraRetries;
        HttpResponseMessage? response = null;
        Exception? lastSendError = null;

        for (var attempt = 0; attempt < attempts; attempt++)
        {
            response?.Dispose();
            var client = _httpClientFactory.CreateClient(ResolveClientName(options));
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            ApplyCallHeaders(req, options);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                response = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            }
            catch (Exception ex)
            {
                lastSendError = ex;
                if (attempt >= attempts - 1)
                    throw;
                await DelayColdStartAsync(attempt, cancellationToken);
                continue;
            }

            if (response.StatusCode != HttpStatusCode.ServiceUnavailable || attempt >= attempts - 1)
                break;

            _logger.LogInformation(
                "OpenAI-compatible chat 503 (attempt {Attempt}/{Attempts}), retrying cold start.",
                attempt + 1,
                attempts);
            await DelayColdStartAsync(attempt, cancellationToken);
        }

        if (response is null)
            throw lastSendError ?? new OpenAiCompatibleRequestException("Le fournisseur de modèles n'a pas répondu.");

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var errBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("OpenAI-compatible chat failed: {Status} {Body}", (int)response.StatusCode, errBody);
                throw new OpenAiCompatibleRequestException(BuildUserError(response.StatusCode, errBody), errBody, (int)response.StatusCode);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            var contentBuffer = new StringBuilder();
            var toolAcc = new Dictionary<int, ToolCallAccumulator>();

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                if (!line.StartsWith("data:", StringComparison.Ordinal))
                    continue;
                var payload = line["data:".Length..].Trim();
                if (payload == "[DONE]")
                    break;

                OpenAiSseChunk? chunk;
                try
                {
                    chunk = JsonSerializer.Deserialize<OpenAiSseChunk>(payload, JsonOptions);
                }
                catch (JsonException ex)
                {
                    _logger.LogDebug(ex, "OpenAI SSE parse skip: {Line}", line);
                    continue;
                }

                if (chunk?.Choices is not { Count: > 0 } choices)
                    continue;

                var choice = choices[0];
                var delta = choice.Delta;
                var text = ExtractDeltaText(delta?.Content ?? default);
                if (text is { Length: > 0 })
                {
                    contentBuffer.Append(text);
                    yield return new OllamaChatChunk
                    {
                        Message = new OllamaChatMessage { Role = "assistant", Content = text }
                    };
                }

                if (delta?.ToolCalls is { Count: > 0 } tcDeltas)
                {
                    foreach (var d in tcDeltas)
                    {
                        var idx = d.Index ?? 0;
                        if (!toolAcc.TryGetValue(idx, out var acc))
                        {
                            acc = new ToolCallAccumulator();
                            toolAcc[idx] = acc;
                        }

                        if (!string.IsNullOrEmpty(d.Id))
                            acc.Id = d.Id;
                        if (d.Function?.Name is { Length: > 0 } n)
                            acc.Name = n;
                        if (d.Function?.Arguments is { Length: > 0 } a)
                            acc.Arguments.Append(a);
                    }
                }
            }

            if (toolAcc.Count > 0)
            {
                var ollamaCalls = new List<OllamaToolCall>();
                foreach (var kv in toolAcc.OrderBy(k => k.Key))
                {
                    var acc = kv.Value;
                    Dictionary<string, object?> args = new();
                    var argStr = acc.Arguments.ToString();
                    if (!string.IsNullOrWhiteSpace(argStr))
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(argStr);
                            foreach (var prop in doc.RootElement.EnumerateObject())
                                args[prop.Name] = JsonElementToObject(prop.Value);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to parse tool arguments: {Args}", argStr);
                        }
                    }

                    ollamaCalls.Add(new OllamaToolCall
                    {
                        Id = string.IsNullOrEmpty(acc.Id) ? Guid.NewGuid().ToString("N")[..16] : acc.Id,
                        Function = new OllamaToolCallFunction
                        {
                            Name = acc.Name ?? "",
                            Arguments = args
                        }
                    });
                }

                yield return new OllamaChatChunk
                {
                    Message = new OllamaChatMessage
                    {
                        Role = "assistant",
                        Content = string.Empty,
                        ToolCalls = ollamaCalls
                    },
                    Done = true
                };
            }
        }
    }

    public async Task<IReadOnlyList<OpenAiRemoteModelInfo>> ListModelsAsync(
        string baseUrl,
        string apiKey,
        CancellationToken cancellationToken = default,
        OpenAiCompatibleCallOptions? options = null)
    {
        var cacheKey = ModelListCacheKeyPrefix + baseUrl.TrimEnd('/');
        var ttl = TimeSpan.FromSeconds(Math.Clamp(_settings.ModelListCacheSeconds, 10, 600));
        return await _memoryCache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = ttl;
            var url = $"{baseUrl.TrimEnd('/')}/models";
            var client = _httpClientFactory.CreateClient(ResolveClientName(options));
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            ApplyCallHeaders(req, options);

            using var response = await client.SendAsync(req, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OpenAI-compatible list models failed: {Status}", (int)response.StatusCode);
                return (IReadOnlyList<OpenAiRemoteModelInfo>)Array.Empty<OpenAiRemoteModelInfo>();
            }

            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(text);
            var list = new List<OpenAiRemoteModelInfo>();
            if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in data.EnumerateArray())
                {
                    var id = el.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
                    if (string.IsNullOrEmpty(id))
                        continue;
                    var name = el.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
                    list.Add(new OpenAiRemoteModelInfo(id, name));
                }
            }

            return (IReadOnlyList<OpenAiRemoteModelInfo>)list;
        }) ?? Array.Empty<OpenAiRemoteModelInfo>();
    }

    private void ApplyCallHeaders(HttpRequestMessage req, OpenAiCompatibleCallOptions? options)
    {
        if (options is null || options.AddOpenRouterHeaders)
            AddOpenRouterHeaders(req);

        if (!string.IsNullOrWhiteSpace(options?.SessionId))
            req.Headers.TryAddWithoutValidation("Modal-Session-ID", options.SessionId);
    }

    private static string ResolveClientName(OpenAiCompatibleCallOptions? options) =>
        string.IsNullOrWhiteSpace(options?.HttpClientName)
            ? OpenAiCompatibleCallOptions.OpenAiCompatibleClientName
            : options.HttpClientName;

    private static async Task DelayColdStartAsync(int attempt, CancellationToken cancellationToken)
    {
        var ms = Math.Min(2000, 250 * (1 << Math.Max(0, attempt)));
        await Task.Delay(ms, cancellationToken);
    }

    private static string? ExtractDeltaText(JsonElement content)
    {
        switch (content.ValueKind)
        {
            case JsonValueKind.String:
                return content.GetString();
            case JsonValueKind.Array:
                var sb = new StringBuilder();
                foreach (var part in content.EnumerateArray())
                {
                    if (part.ValueKind == JsonValueKind.String)
                        sb.Append(part.GetString());
                    else if (part.ValueKind == JsonValueKind.Object
                             && part.TryGetProperty("text", out var textEl)
                             && textEl.ValueKind == JsonValueKind.String)
                    {
                        sb.Append(textEl.GetString());
                    }
                }

                return sb.Length == 0 ? null : sb.ToString();
            default:
                return null;
        }
    }

    private void AddOpenRouterHeaders(HttpRequestMessage req)
    {
        if (!string.IsNullOrWhiteSpace(_settings.HttpReferer))
            req.Headers.TryAddWithoutValidation("HTTP-Referer", _settings.HttpReferer);
        if (!string.IsNullOrWhiteSpace(_settings.AppTitle))
            req.Headers.TryAddWithoutValidation("X-Title", _settings.AppTitle);
    }

    private static object? JsonElementToObject(JsonElement el)
    {
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => el.GetRawText()
        };
    }

    private static string BuildUserError(System.Net.HttpStatusCode status, string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return "Le fournisseur de modèles a renvoyé une erreur. Vérifiez la clé API et le modèle.";
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err))
            {
                if (err.ValueKind == JsonValueKind.Object && err.TryGetProperty("message", out var msg))
                    return $"Erreur fournisseur : {msg.GetString()}";
                if (err.ValueKind == JsonValueKind.String)
                    return $"Erreur fournisseur : {err.GetString()}";
            }
        }
        catch
        {
            // ignore
        }

        return "Le fournisseur de modèles a renvoyé une erreur. Vérifiez la clé API et le modèle.";
    }

    private sealed class ToolCallAccumulator
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public StringBuilder Arguments { get; } = new();
    }

    private sealed class ChatCompletionRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; init; }

        [JsonPropertyName("messages")]
        public required List<OpenAiChatMessagePayload> Messages { get; init; }

        [JsonPropertyName("tools")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<OllamaToolDefinition>? Tools { get; init; }

        [JsonPropertyName("stream")]
        public bool Stream { get; init; }

        [JsonPropertyName("temperature")]
        public double Temperature { get; init; }

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; init; }

        [JsonPropertyName("seed")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Seed { get; init; }

        [JsonPropertyName("reasoning_effort")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ReasoningEffort { get; init; }
    }

    private sealed class OpenAiSseChunk
    {
        [JsonPropertyName("choices")]
        public List<OpenAiChoice>? Choices { get; set; }
    }

    private sealed class OpenAiChoice
    {
        [JsonPropertyName("delta")]
        public OpenAiDelta? Delta { get; set; }

        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }
    }

    private sealed class OpenAiDelta
    {
        [JsonPropertyName("content")]
        public JsonElement Content { get; set; }

        [JsonPropertyName("tool_calls")]
        public List<OpenAiToolCallDelta>? ToolCalls { get; set; }
    }

    private sealed class OpenAiToolCallDelta
    {
        [JsonPropertyName("index")]
        public int? Index { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("function")]
        public OpenAiFunctionDelta? Function { get; set; }
    }

    private sealed class OpenAiFunctionDelta
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("arguments")]
        public string? Arguments { get; set; }
    }
}

public sealed class OpenAiCompatibleRequestException : Exception
{
    public string UserMessage { get; }
    public string? ResponseBody { get; }
    public int? HttpStatusCode { get; }

    public OpenAiCompatibleRequestException(string userMessage, string? responseBody = null, int? httpStatusCode = null, Exception? inner = null)
        : base(userMessage, inner)
    {
        UserMessage = userMessage;
        ResponseBody = responseBody;
        HttpStatusCode = httpStatusCode;
    }
}
