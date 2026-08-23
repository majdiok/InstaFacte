using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Common.Interfaces.Treasury;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.AI;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Application.Features.AI.Json;
using FactuTrust.Domain.Entities.Treasury;
using FactuTrust.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services.Treasury;

/// <summary>
/// Interroge le modèle de langage pour pondérer les scénarios et rédiger l'analyse.
/// </summary>
/// <remarks>
/// <para>
/// Le modèle reçoit un instantané compact du résultat déterministe et ne renvoie que des
/// probabilités et du texte. Aucun montant, aucune date, aucun solde ne lui est demandé.
/// </para>
/// <para>
/// Toute défaillance — provider éteint, délai dépassé, JSON invalide, réponse incomplète —
/// se solde par un <c>null</c>. L'appelant conserve alors le résultat déterministe : un recalcul
/// de trésorerie ne doit jamais échouer parce qu'un modèle n'a pas répondu.
/// </para>
/// </remarks>
public sealed class CashFlowAiAdvisor : ICashFlowAiAdvisor
{
    private readonly IOllamaClient _ollama;
    private readonly IOpenAiChatCompletionsClient _openAi;
    private readonly IPlatformAiSettingsService _platformSettings;
    private readonly IModalCredentialsResolver _modalCredentials;
    private readonly ITenantContext _tenantContext;
    private readonly TreasuryForecastOptions _options;
    private readonly ILogger<CashFlowAiAdvisor> _logger;

    public CashFlowAiAdvisor(
        IOllamaClient ollama,
        IOpenAiChatCompletionsClient openAi,
        IPlatformAiSettingsService platformSettings,
        IModalCredentialsResolver modalCredentials,
        ITenantContext tenantContext,
        IOptions<TreasuryForecastOptions> options,
        ILogger<CashFlowAiAdvisor> logger)
    {
        _ollama = ollama;
        _openAi = openAi;
        _platformSettings = platformSettings;
        _modalCredentials = modalCredentials;
        _tenantContext = tenantContext;
        _options = options.Value;
        _logger = logger;
    }

    private const string SystemPrompt = """
        Tu es un directeur financier expérimenté qui commente une projection de trésorerie déjà
        calculée. Tu ne calcules RIEN : les montants, les dates et les soldes te sont donnés et
        font foi.

        Ta mission :
        1. répartir 100 points de probabilité entre les trois scénarios (optimiste, réaliste,
           pessimiste), en restant proche de la répartition déterministe fournie ;
        2. rédiger au plus trois alertes datées, trois facteurs d'influence et deux recommandations.

        Réponds UNIQUEMENT par un objet JSON conforme à ce schéma, sans texte autour :
        {
          "scenarioProbabilities": { "optimistic": <nombre>, "realistic": <nombre>, "pessimistic": <nombre> },
          "scenarioRationale":     { "optimistic": "<texte>", "realistic": "<texte>", "pessimistic": "<texte>" },
          "alerts":  [ { "severity": "critical|warning|info", "title": "<texte>", "detail": "<texte>", "periodStart": "AAAA-MM-JJ" } ],
          "drivers": [ { "label": "<texte>", "detail": "<texte>", "impact": "low|medium|high", "direction": "up|down" } ],
          "recommendations": [ { "title": "<texte>", "detail": "<texte>" } ]
        }

        Rédige en français, dans un registre professionnel et sobre. Ne mentionne jamais que tu es
        un modèle de langage.
        """;

    public async Task<CashFlowAiAnalysis?> AnalyzeAsync(
        CashFlowForecastRun run,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Ai.Enabled) return null;
        if (run.Scenarios.Count == 0) return null;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, _options.Ai.TimeoutSeconds)));

        try
        {
            var modelRef = _options.Ai.ModelOverride
                           ?? await _platformSettings.GetDefaultModelRefAsync(timeout.Token);

            var parsed = ModelRef.Parse(modelRef);
            if (string.IsNullOrWhiteSpace(parsed.ProviderModelId))
            {
                _logger.LogDebug("Trésorerie prévisionnelle : aucun modèle par défaut configuré, analyse IA ignorée.");
                return null;
            }

            var userPrompt = BuildSnapshot(run);
            var raw = parsed.Kind switch
            {
                LlmProviderKind.Ollama => await CompleteWithOllamaAsync(parsed.ProviderModelId, userPrompt, timeout.Token),
                LlmProviderKind.OpenRouter => await CompleteWithOpenRouterAsync(parsed.ProviderModelId, userPrompt, timeout.Token),
                LlmProviderKind.Modal => await CompleteWithModalAsync(parsed.ProviderModelId, userPrompt, timeout.Token),
                _ => null
            };

            if (string.IsNullOrWhiteSpace(raw))
            {
                _logger.LogInformation("Trésorerie prévisionnelle : réponse IA vide, projection déterministe conservée.");
                return null;
            }

            return Parse(raw);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Trésorerie prévisionnelle : analyse IA abandonnée après {Timeout} s, projection déterministe conservée.",
                _options.Ai.TimeoutSeconds);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Trésorerie prévisionnelle : analyse IA en échec, projection déterministe conservée.");
            return null;
        }
    }

    /// <summary>
    /// Instantané transmis au modèle : agrégats mensuels, scénarios déterministes et principaux
    /// flux. Aucune donnée nominative superflue, et surtout aucune valeur qu'il aurait à recalculer.
    /// </summary>
    private string BuildSnapshot(CashFlowForecastRun run)
    {
        var payload = new
        {
            devise = run.Currency,
            periode = new { debut = run.PeriodStart.ToString("yyyy-MM-dd"), fin = run.PeriodEnd.ToString("yyyy-MM-dd") },
            soldeOuverture = run.OpeningBalance,
            soldeCloture = run.ClosingBalance,
            encaissements = run.TotalInflows,
            decaissements = run.TotalOutflows,
            indiceConfiance = run.ConfidencePercent,
            mois = run.Buckets
                .OrderBy(b => b.SequenceIndex)
                .Select(b => new
                {
                    mois = b.PeriodStart.ToString("yyyy-MM"),
                    encaissements = b.Inflows,
                    decaissements = b.Outflows,
                    soldeFin = b.ClosingBalance
                }),
            scenariosDeterministes = run.Scenarios.Select(s => new
            {
                type = s.Kind switch
                {
                    CashFlowScenarioKind.Optimistic => "optimistic",
                    CashFlowScenarioKind.Realistic => "realistic",
                    _ => "pessimistic"
                },
                soldeFinal = s.ClosingBalance,
                probabilite = s.DeterministicProbabilityPercent
            }),
            principauxFlux = run.Lines
                .OrderByDescending(l => l.Amount)
                .Take(Math.Max(1, _options.Ai.TopFlowsInPrompt))
                .Select(l => new
                {
                    sens = l.Direction == CashFlowDirection.Inflow ? "encaissement" : "decaissement",
                    libelle = l.Label,
                    date = l.ExpectedDate.ToString("yyyy-MM-dd"),
                    montant = l.Amount,
                    probabilite = l.ProbabilityPercent
                }),
            constatsDeterministes = run.Insights
                .Where(i => i.Origin == CashFlowInsightOrigin.Rule)
                .Select(i => i.Title)
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }

    private async Task<string?> CompleteWithOllamaAsync(
        string model,
        string userPrompt,
        CancellationToken cancellationToken)
    {
        var request = new OllamaChatRequest
        {
            Model = model,
            Stream = true,
            // Sortie contrainte : sans elle, le modèle encadre volontiers son JSON de prose.
            Format = "json",
            Messages = new List<OllamaChatMessage>
            {
                new() { Role = "system", Content = SystemPrompt },
                new() { Role = "user", Content = userPrompt }
            }
        };

        var sb = new StringBuilder();
        await foreach (var chunk in _ollama.StreamChatAsync(request, cancellationToken))
        {
            if (chunk.Message?.Content is { Length: > 0 } content)
                sb.Append(content);
        }

        return sb.ToString();
    }

    private async Task<string?> CompleteWithOpenRouterAsync(
        string model,
        string userPrompt,
        CancellationToken cancellationToken)
    {
        var credentials = await _platformSettings.GetOpenRouterCredentialsAsync(cancellationToken);
        if (!credentials.IsEnabled || string.IsNullOrWhiteSpace(credentials.ApiKey))
        {
            _logger.LogDebug("Trésorerie prévisionnelle : OpenRouter non configuré, analyse IA ignorée.");
            return null;
        }

        var messages = new List<OpenAiChatMessagePayload>
        {
            new() { Role = "system", Content = SystemPrompt },
            new() { Role = "user", Content = userPrompt }
        };

        var sb = new StringBuilder();
        await foreach (var chunk in _openAi.StreamChatAsOllamaCompatibleAsync(
                           credentials.BaseUrl,
                           credentials.ApiKey!,
                           model,
                           messages,
                           Array.Empty<OllamaToolDefinition>(),
                           temperature: 0.2,
                           maxTokens: 1200,
                           cancellationToken))
        {
            if (chunk.Message?.Content is { Length: > 0 } content)
                sb.Append(content);
        }

        return sb.ToString();
    }

    private async Task<string?> CompleteWithModalAsync(
        string model,
        string userPrompt,
        CancellationToken cancellationToken)
    {
        var credentials = await _modalCredentials.ResolveAsync(_tenantContext.TenantId, cancellationToken);
        if (!credentials.IsEnabled || string.IsNullOrWhiteSpace(credentials.ApiKey) || string.IsNullOrWhiteSpace(credentials.BaseUrl))
        {
            _logger.LogDebug("Trésorerie prévisionnelle : Modal non configuré, analyse IA ignorée.");
            return null;
        }

        var messages = new List<OpenAiChatMessagePayload>
        {
            new() { Role = "system", Content = SystemPrompt },
            new() { Role = "user", Content = userPrompt }
        };

        var sb = new StringBuilder();
        await foreach (var chunk in _openAi.StreamChatAsOllamaCompatibleAsync(
                           credentials.BaseUrl,
                           credentials.ApiKey!,
                           model,
                           messages,
                           Array.Empty<OllamaToolDefinition>(),
                           temperature: 0.2,
                           maxTokens: 1200,
                           cancellationToken,
                           seed: null,
                           OpenAiCompatibleCallOptions.ForModal(new ModalSettings(), sessionId: null)))
        {
            if (chunk.Message?.Content is { Length: > 0 } content)
                sb.Append(content);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Désérialise la réponse. Tolérante par construction : le produit a déjà été mordu par un
    /// modèle écrivant <c>19.0</c> là où un entier était attendu.
    /// </summary>
    internal static CashFlowAiAnalysis? Parse(string raw)
    {
        var json = ExtractJsonObject(raw);
        if (json is null) return null;

        var payload = JsonSerializer.Deserialize<AiPayload>(json, LlmJsonOptions.Tolerant);
        if (payload?.ScenarioProbabilities is null) return null;

        var probabilities = new Dictionary<CashFlowScenarioKind, decimal>();
        var rationales = new Dictionary<CashFlowScenarioKind, string?>();

        void Add(CashFlowScenarioKind kind, decimal? value, string? rationale)
        {
            if (value is null) return;
            probabilities[kind] = value.Value;
            rationales[kind] = rationale;
        }

        Add(CashFlowScenarioKind.Optimistic, payload.ScenarioProbabilities.Optimistic, payload.ScenarioRationale?.Optimistic);
        Add(CashFlowScenarioKind.Realistic, payload.ScenarioProbabilities.Realistic, payload.ScenarioRationale?.Realistic);
        Add(CashFlowScenarioKind.Pessimistic, payload.ScenarioProbabilities.Pessimistic, payload.ScenarioRationale?.Pessimistic);

        if (probabilities.Count == 0) return null;

        return new CashFlowAiAnalysis
        {
            ScenarioProbabilities = probabilities,
            ScenarioRationales = rationales,
            Alerts = (payload.Alerts ?? new List<AiAlertPayload>())
                .Where(a => !string.IsNullOrWhiteSpace(a.Title))
                .Select(a => new CashFlowAiInsight
                {
                    Title = a.Title!,
                    Detail = a.Detail,
                    Severity = a.Severity?.Trim().ToLowerInvariant() switch
                    {
                        "critical" => CashFlowInsightSeverity.Critical,
                        "warning" => CashFlowInsightSeverity.Warning,
                        _ => CashFlowInsightSeverity.Info
                    },
                    PeriodStart = DateTime.TryParse(a.PeriodStart, out var d) ? d.Date : null
                })
                .ToList(),
            Drivers = (payload.Drivers ?? new List<AiDriverPayload>())
                .Where(d => !string.IsNullOrWhiteSpace(d.Label))
                .Select(d => new CashFlowAiInsight
                {
                    Title = d.Label!,
                    Detail = d.Detail,
                    Impact = d.Impact?.Trim().ToLowerInvariant() switch
                    {
                        "high" => CashFlowImpactLevel.High,
                        "low" => CashFlowImpactLevel.Low,
                        _ => CashFlowImpactLevel.Medium
                    },
                    ImpactDirection = d.Direction?.Trim().ToLowerInvariant() switch
                    {
                        "up" => CashFlowDirection.Inflow,
                        "down" => CashFlowDirection.Outflow,
                        _ => null
                    }
                })
                .ToList(),
            Recommendations = (payload.Recommendations ?? new List<AiRecommendationPayload>())
                .Where(r => !string.IsNullOrWhiteSpace(r.Title))
                .Select(r => new CashFlowAiInsight { Title = r.Title!, Detail = r.Detail })
                .ToList()
        };
    }

    /// <summary>
    /// Isole le premier objet JSON d'une réponse, même si le modèle l'a encadré de prose ou d'un
    /// bloc de code — ce que font régulièrement les petits modèles malgré la consigne.
    /// </summary>
    internal static string? ExtractJsonObject(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');

        return start >= 0 && end > start ? raw[start..(end + 1)] : null;
    }

    private sealed class AiPayload
    {
        [JsonPropertyName("scenarioProbabilities")]
        public AiScenarioNumbers? ScenarioProbabilities { get; init; }

        [JsonPropertyName("scenarioRationale")]
        public AiScenarioTexts? ScenarioRationale { get; init; }

        [JsonPropertyName("alerts")]
        public List<AiAlertPayload>? Alerts { get; init; }

        [JsonPropertyName("drivers")]
        public List<AiDriverPayload>? Drivers { get; init; }

        [JsonPropertyName("recommendations")]
        public List<AiRecommendationPayload>? Recommendations { get; init; }
    }

    private sealed class AiScenarioNumbers
    {
        [JsonPropertyName("optimistic")] public decimal? Optimistic { get; init; }
        [JsonPropertyName("realistic")] public decimal? Realistic { get; init; }
        [JsonPropertyName("pessimistic")] public decimal? Pessimistic { get; init; }
    }

    private sealed class AiScenarioTexts
    {
        [JsonPropertyName("optimistic")] public string? Optimistic { get; init; }
        [JsonPropertyName("realistic")] public string? Realistic { get; init; }
        [JsonPropertyName("pessimistic")] public string? Pessimistic { get; init; }
    }

    private sealed class AiAlertPayload
    {
        [JsonPropertyName("severity")] public string? Severity { get; init; }
        [JsonPropertyName("title")] public string? Title { get; init; }
        [JsonPropertyName("detail")] public string? Detail { get; init; }
        [JsonPropertyName("periodStart")] public string? PeriodStart { get; init; }
    }

    private sealed class AiDriverPayload
    {
        [JsonPropertyName("label")] public string? Label { get; init; }
        [JsonPropertyName("detail")] public string? Detail { get; init; }
        [JsonPropertyName("impact")] public string? Impact { get; init; }
        [JsonPropertyName("direction")] public string? Direction { get; init; }
    }

    private sealed class AiRecommendationPayload
    {
        [JsonPropertyName("title")] public string? Title { get; init; }
        [JsonPropertyName("detail")] public string? Detail { get; init; }
    }
}
