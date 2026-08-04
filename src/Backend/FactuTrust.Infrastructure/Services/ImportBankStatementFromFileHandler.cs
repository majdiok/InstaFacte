using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.AI;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Application.Features.Accounting.BankStatementImport;
using FactuTrust.Domain.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services;

/// <summary>Extraction structurée LLM d'un relevé bancaire (fallback OCR/PDF scanné).</summary>
public sealed class ImportBankStatementFromFileHandler
{
    private readonly IOllamaClient _ollamaClient;
    private readonly IOpenAiChatCompletionsClient _openAiClient;
    private readonly IOllamaModelReadinessChecker _readinessChecker;
    private readonly IPlatformAiSettingsService _platformAiSettings;
    private readonly IOllamaInferenceProfileResolver _inferenceProfileResolver;
    private readonly ILogger<ImportBankStatementFromFileHandler> _logger;
    private readonly OllamaSettings _ollamaSettings;

    private const int MaxTextChars = 60_000;
    private const int MaxOutputTokens = 8192;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        AllowTrailingCommas = true
    };

    private const string SystemPrompt = """
Tu es un moteur d'extraction de relevés bancaires tunisiens (BIAT, STB, BNA…).
Réponds UNIQUEMENT avec un objet JSON valide, sans markdown.

RÈGLES :
1. Montants décimaux avec point (ex. 17837.479). Format TN : 17.837,479 → 17837.479
2. Dates ISO AAAA-MM-JJ
3. isDebit : true = décaissement, false = encaissement
4. N'invente pas de lignes absentes du document
5. confidence : "high" | "medium" | "low"
6. warnings : liste de messages en français

Schéma :
{
  "bankName": "string|null",
  "rib": "20 chiffres|null",
  "holderName": "string|null",
  "periodStart": "AAAA-MM-JJ|null",
  "periodEnd": "AAAA-MM-JJ|null",
  "statementDate": "AAAA-MM-JJ|null",
  "openingBalance": 0,
  "closingBalance": 0,
  "currency": "TND",
  "lines": [{ "transactionDate": "AAAA-MM-JJ", "valueDate": "AAAA-MM-JJ|null", "reference": "string", "description": "string", "amount": 0, "isDebit": true }],
  "confidence": "medium",
  "warnings": []
}
""";

    public ImportBankStatementFromFileHandler(
        IOllamaClient ollamaClient,
        IOpenAiChatCompletionsClient openAiClient,
        IOllamaModelReadinessChecker readinessChecker,
        IPlatformAiSettingsService platformAiSettings,
        IOllamaInferenceProfileResolver inferenceProfileResolver,
        ILogger<ImportBankStatementFromFileHandler> logger,
        IOptions<OllamaSettings> ollamaSettings)
    {
        _ollamaClient = ollamaClient;
        _openAiClient = openAiClient;
        _readinessChecker = readinessChecker;
        _platformAiSettings = platformAiSettings;
        _inferenceProfileResolver = inferenceProfileResolver;
        _logger = logger;
        _ollamaSettings = ollamaSettings.Value;
    }

    public async Task<Result<LlmBankStatementExtraction>> ExtractAsync(
        byte[] content,
        string fileName,
        string contentType,
        AiDocumentExtractionResult extraction,
        CancellationToken cancellationToken)
    {
        var text = extraction.Text.Length > MaxTextChars
            ? extraction.Text[..MaxTextChars]
            : extraction.Text;

        var modelRef = await ResolveModelRefAsync(cancellationToken);
        if (modelRef.Kind == LlmProviderKind.Ollama)
        {
            if (!await _ollamaClient.IsAvailableAsync(cancellationToken))
                return Result.Failure<LlmBankStatementExtraction>(Error.Validation("BankStatementImport",
                    "Le moteur IA InstaFact est indisponible pour lire ce relevé scanné."));
            var readiness = await _readinessChecker.CheckAsync(modelRef.ProviderModelId!, cancellationToken);
            if (!readiness.IsReady)
                return Result.Failure<LlmBankStatementExtraction>(Error.Validation("BankStatementImport",
                    readiness.UserMessage ?? "Modèle IA non prêt."));
        }
        else
        {
            var credentials = await _platformAiSettings.GetOpenRouterCredentialsAsync(cancellationToken);
            if (string.IsNullOrEmpty(credentials.ApiKey))
                return Result.Failure<LlmBankStatementExtraction>(Error.Validation("BankStatementImport",
                    "Clé OpenRouter manquante. Configurez-la dans le back-office plateforme > Configuration IA (OpenRouter)."));
        }

        var images = extraction.Pages
            .Where(p => !string.IsNullOrEmpty(p.ImageBase64))
            .Select(p => p.ImageBase64!)
            .Take(10)
            .ToList();

        var raw = await CallLlmAsync(modelRef, text, images, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
            return Result.Failure<LlmBankStatementExtraction>(Error.Validation("BankStatementImport",
                "L'IA n'a pas pu extraire les données du relevé."));

        var json = InvoiceImportParsing.ExtractFirstJsonObject(raw);
        if (string.IsNullOrWhiteSpace(json))
            return Result.Failure<LlmBankStatementExtraction>(Error.Validation("BankStatementImport",
                "Réponse IA non exploitable (JSON absent)."));

        try
        {
            var parsed = JsonSerializer.Deserialize<LlmBankStatementExtraction>(json, JsonOptions);
            if (parsed is null)
                return Result.Failure<LlmBankStatementExtraction>(Error.Validation("BankStatementImport",
                    "Réponse IA vide."));
            return Result.Success(parsed);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Import relevé : JSON LLM invalide.");
            return Result.Failure<LlmBankStatementExtraction>(Error.Validation("BankStatementImport",
                "Réponse IA mal formée."));
        }
    }

    private async Task<string> CallLlmAsync(
        ParsedModelRef modelRef, string text, List<string> images, CancellationToken cancellationToken)
    {
        var userPrompt = $"""
Analyse ce relevé bancaire tunisien et extrais toutes les opérations.

--- DÉBUT DU CONTENU ---
{text}
--- FIN DU CONTENU ---
""";

        var sb = new StringBuilder();
        if (modelRef.Kind == LlmProviderKind.Ollama)
        {
            var messages = new List<OllamaChatMessage>
            {
                new() { Role = "system", Content = SystemPrompt },
                new() { Role = "user", Content = userPrompt, Images = images.Count > 0 ? images : null }
            };
            var inferenceProfile = await _inferenceProfileResolver.ResolveForPlatformAsync(cancellationToken);
            var options = inferenceProfile.ApplyTo(new OllamaOptions
            {
                Temperature = 0,
                NumPredict = MaxOutputTokens,
                NumCtx = Math.Min(_ollamaSettings.NumCtx, 16384)
            });
            var request = new OllamaChatRequest
            {
                Model = modelRef.ProviderModelId,
                Messages = messages,
                Stream = true,
                Format = "json",
                Options = options
            };
            await foreach (var chunk in _ollamaClient.StreamChatAsync(request, cancellationToken))
            {
                if (!string.IsNullOrEmpty(chunk.Message?.Content))
                    sb.Append(chunk.Message.Content);
            }
        }

        return sb.ToString();
    }

    private async Task<ParsedModelRef> ResolveModelRefAsync(CancellationToken cancellationToken)
    {
        var model = !string.IsNullOrWhiteSpace(_ollamaSettings.BankStatementImportModel)
            ? _ollamaSettings.BankStatementImportModel.Trim()
            : !string.IsNullOrWhiteSpace(_ollamaSettings.InvoiceImportModel)
                ? _ollamaSettings.InvoiceImportModel.Trim()
                : _ollamaSettings.DefaultModel.Trim();

        try
        {
            var platform = await _platformAiSettings.GetInvoiceImportModelRefAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(platform))
                model = platform;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Import relevé : modèle plateforme indisponible.");
        }

        return ModelRef.Parse(model);
    }
}
