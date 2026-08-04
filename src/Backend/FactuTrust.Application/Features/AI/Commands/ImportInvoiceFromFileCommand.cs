using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Application.Features.Clients.Queries;
using FactuTrust.Application.Features.Products.Queries;
using FactuTrust.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.AI.Commands;

/// <summary>
/// Commande d'import d'une facture à partir d'un fichier (PDF, image, Word, Excel).
/// Le contenu est extrait puis structuré par le LLM, sans passer par le chat de l'assistant.
/// </summary>
public sealed record ImportInvoiceFromFileCommand(
    Stream FileStream,
    string FileName,
    string ContentType,
    string? Model = null);

/// <summary>
/// Handler one-shot (requête/réponse, sans streaming SSE ni conversation persistée) :
/// extraction documentaire → appel LLM structuré → parsing/normalisation → rapprochement
/// clients/produits → <see cref="InvoiceImportResultDto"/>.
/// </summary>
public sealed class ImportInvoiceFromFileHandler
{
    private readonly IOllamaClient _ollamaClient;
    private readonly IOpenAiChatCompletionsClient _openAiClient;
    private readonly IAiDocumentTextExtractor _documentTextExtractor;
    private readonly IOllamaModelReadinessChecker _readinessChecker;
    private readonly IPlatformAiSettingsService _platformAiSettings;
    private readonly IOllamaInferenceProfileResolver _inferenceProfileResolver;
    private readonly IMediator _mediator;
    private readonly ILogger<ImportInvoiceFromFileHandler> _logger;
    private readonly OllamaSettings _ollamaSettings;

    private const int MaxTextChars = 60_000;
    private const int MaxImages = 10;
    private const int MaxLinesToMatch = 50;
    private const int DefaultImportMaxOutputTokens = 1536;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private const string SystemPrompt = """
Tu es un moteur d'extraction de données de factures pour un logiciel de facturation tunisien.
Ta SEULE tâche est d'analyser le document fourni et d'en extraire les informations sous la forme d'un unique objet JSON.

RÈGLES ABSOLUES :
1. Réponds UNIQUEMENT avec un objet JSON valide. Aucun texte, aucune explication, aucun bloc markdown avant ou après.
2. N'invente JAMAIS de données. Si une information est absente du document, mets null. Pour une liste vide, mets [].
3. Tous les montants sont des nombres décimaux : séparateur décimal point, sans symbole monétaire, sans séparateur de milliers.
4. "unitPriceHT" est le prix unitaire HORS TAXES (hors TVA).
5. "vatRatePercent" doit valoir 0, 7, 13 ou 19 (taux de TVA tunisiens, en pourcentage). Arrondis au plus proche.
6. Les dates sont au format ISO "AAAA-MM-JJ". Convertis "JJ/MM/AAAA" vers "AAAA-MM-JJ". Date absente => null.
7. "currency" vaut "TND", "EUR" ou "USD". En l'absence d'indication, mets "TND".
8. Montants tunisiens : "650,000" ou "650.000" signifient 650 dinars (3 décimales TND) → renvoie 650.000 en JSON.
9. "documentType" vaut "INVOICE", "CREDIT_NOTE", "DELIVERY_NOTE" (bon de livraison), "PROFORMA" (devis), ou "UNKNOWN" si illisible.
10. Si bon de livraison / devis : extrais quand même client, lignes et montants ; documentType adapté ; ajoute un warning en français.
11. "invoiceNumber" : numéro de facture ou de BL (ex. 00396) si présent.
12. Chaque ligne d'article DOIT avoir une "designation" non vide. N'inclus pas les lignes sans désignation.
13. "client" est le DESTINATAIRE (acheteur), distinct de l'émetteur "seller". Conserve le nom en arabe ou français tel qu'affiché.
14. "confidence" vaut "high", "medium" ou "low" selon ta certitude globale.
15. "warnings" est une liste de messages courts en français signalant toute ambiguïté ou donnée douteuse.

SCHÉMA JSON EXACT À RESPECTER :
{
  "documentType": "INVOICE",
  "invoiceNumber": "string|null",
  "issueDate": "AAAA-MM-JJ|null",
  "dueDate": "AAAA-MM-JJ|null",
  "currency": "TND",
  "seller": { "name": "string|null", "nif": "string|null" },
  "client": {
    "name": "string|null",
    "nif": "string|null",
    "email": "string|null",
    "phone": "string|null",
    "address": { "street": "string|null", "city": "string|null", "postalCode": "string|null", "governorate": "string|null" }
  },
  "lines": [
    { "designation": "string", "description": "string|null", "quantity": 0, "unit": "string|null", "unitPriceHT": 0, "discountPercent": 0, "vatRatePercent": 19 }
  ],
  "totals": { "totalHT": 0, "totalVat": 0, "totalTTC": 0 },
  "confidence": "high",
  "warnings": []
}
""";

    private const string CompactSystemPrompt = """
Tu extrais une facture ou pièce commerciale tunisienne (facture, bon de livraison, devis) en JSON strict. Réponds UNIQUEMENT avec un objet JSON valide, sans markdown.
Montants décimaux (point), TND 3 décimales (650,000 → 650.000), TVA 0/7/13/19, dates ISO AAAA-MM-JJ, devise TND/EUR/USD, client=acheteur.
documentType: INVOICE|CREDIT_NOTE|DELIVERY_NOTE|PROFORMA|UNKNOWN. Bon de livraison → DELIVERY_NOTE + warnings.
Schéma: documentType, invoiceNumber, issueDate, dueDate, currency, seller{name,nif}, client{name,nif,email,phone,address{street,city,postalCode,governorate}}, lines[{designation,description,quantity,unit,unitPriceHT,discountPercent,vatRatePercent}], totals{totalHT,totalVat,totalTTC}, confidence, warnings[].
""";

    public ImportInvoiceFromFileHandler(
        IOllamaClient ollamaClient,
        IOpenAiChatCompletionsClient openAiClient,
        IAiDocumentTextExtractor documentTextExtractor,
        IOllamaModelReadinessChecker readinessChecker,
        IPlatformAiSettingsService platformAiSettings,
        IOllamaInferenceProfileResolver inferenceProfileResolver,
        IMediator mediator,
        ILogger<ImportInvoiceFromFileHandler> logger,
        IOptions<OllamaSettings> ollamaSettings)
    {
        _ollamaClient = ollamaClient;
        _openAiClient = openAiClient;
        _documentTextExtractor = documentTextExtractor;
        _readinessChecker = readinessChecker;
        _platformAiSettings = platformAiSettings;
        _inferenceProfileResolver = inferenceProfileResolver;
        _mediator = mediator;
        _logger = logger;
        _ollamaSettings = ollamaSettings.Value;
    }

    public async Task<Result<InvoiceImportResultDto>> HandleAsync(
        ImportInvoiceFromFileCommand command,
        CancellationToken cancellationToken)
    {
        var totalSw = Stopwatch.StartNew();

        // 1. Résolution du modèle texte (JSON structuré).
        var modelRef = await ResolveModelRefAsync(command.Model, cancellationToken);
        var visionModelId = ResolveVisionModelId();
        var wantPrimaryVision = AiModelCapabilityDetector.DetectVisionSupport(modelRef.ProviderModelId);

        // 2. Extraction du texte (+ image base64 pour fallback vision sur photos).
        var extractionSw = Stopwatch.StartNew();
        var extraction = await _documentTextExtractor.ExtractAsync(
            command.FileStream,
            command.FileName,
            command.ContentType,
            new AiDocumentExtractOptions { RenderPagesAsImages = wantPrimaryVision },
            cancellationToken);

        if (!extraction.Success)
            return Result.Failure<InvoiceImportResultDto>(Error.Validation(
                "InvoiceImport", extraction.ErrorMessage ?? "Le fichier n'a pas pu être lu."));

        var sourceImages = extraction.Pages
            .Select(p => p.ImageBase64)
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .Select(b => b!)
            .Take(MaxImages)
            .ToList();

        var extractedText = (extraction.Text ?? string.Empty).Trim();
        var ocrQuality = InvoiceImportOcrQuality.Score(extractedText);
        var useVisionFallback = ShouldUseVisionFallback(extraction, extractedText, sourceImages.Count, visionModelId);

        if (extractedText.Length == 0 && sourceImages.Count == 0)
            return Result.Failure<InvoiceImportResultDto>(Error.Validation(
                "InvoiceImport", "Aucun contenu exploitable n'a été trouvé dans le fichier."));

        if (extractedText.Length == 0 && !useVisionFallback)
        {
            return Result.Failure<InvoiceImportResultDto>(Error.Validation("InvoiceImport",
                "Photo illisible : OCR vide et aucun modèle vision d'import configuré (clé InvoiceImportVisionModel, ex. llava). "
                + "Exécutez scripts/install-tessdata.ps1 ou contactez l'administrateur plateforme."));
        }

        var textTruncated = extraction.Truncated;
        if (extractedText.Length > MaxTextChars)
        {
            extractedText = extractedText[..MaxTextChars];
            textTruncated = true;
        }

        extractionSw.Stop();
        _logger.LogInformation(
            "[InvoiceImport] Extraction terminée en {ElapsedMs} ms : format={Format} ocr={Ocr} caractères={Chars} "
            + "ocrQuality={OcrQuality:F2} sourceImages={ImageCount} visionFallback={VisionFallback}",
            extractionSw.ElapsedMilliseconds, extraction.Format, extraction.OcrApplied, extractedText.Length,
            ocrQuality, sourceImages.Count, useVisionFallback);

        // 3. Disponibilité du fournisseur IA (modèle texte ou vision selon le chemin).
        var activeModelRef = modelRef;
        var llmImages = wantPrimaryVision ? sourceImages : new List<string>();

        if (useVisionFallback)
        {
            activeModelRef = NormalizeModelRef(visionModelId!);
            llmImages = sourceImages;
            if (activeModelRef.Kind == LlmProviderKind.Ollama)
            {
                if (!await _ollamaClient.IsAvailableAsync(cancellationToken))
                    return Result.Failure<InvoiceImportResultDto>(Error.Validation("InvoiceImport",
                        "Le moteur IA InstaFact est indisponible. Vérifiez que le service est démarré sur le serveur."));

                var visionReadiness = await _readinessChecker.CheckAsync(activeModelRef.ProviderModelId!, cancellationToken);
                if (!visionReadiness.IsReady)
                    return Result.Failure<InvoiceImportResultDto>(Error.Validation("InvoiceImport",
                        visionReadiness.UserMessage ?? "Le modèle vision d'import IA n'est pas prêt."));
            }
        }
        else if (modelRef.Kind == LlmProviderKind.Ollama)
        {
            if (!await _ollamaClient.IsAvailableAsync(cancellationToken))
                return Result.Failure<InvoiceImportResultDto>(Error.Validation("InvoiceImport",
                    "Le moteur IA InstaFact est indisponible. Vérifiez que le service est démarré sur le serveur."));

            var readiness = await _readinessChecker.CheckAsync(modelRef.ProviderModelId!, cancellationToken);
            if (!readiness.IsReady)
                return Result.Failure<InvoiceImportResultDto>(Error.Validation("InvoiceImport",
                    readiness.UserMessage ?? "Le modèle d'import IA n'est pas prêt."));
        }
        else
        {
            var credentials = await _platformAiSettings.GetOpenRouterCredentialsAsync(cancellationToken);
            if (string.IsNullOrEmpty(credentials.ApiKey))
                return Result.Failure<InvoiceImportResultDto>(Error.Validation("InvoiceImport",
                    "Aucune clé API OpenRouter configurée. Configurez-la dans le back-office plateforme > Configuration IA (OpenRouter)."));
        }

        // 4-5. Appel LLM one-shot (texte seul ou vision hybride).
        var llmSw = Stopwatch.StartNew();
        var (raw, llmMetrics) = await CallLlmAsync(activeModelRef, extractedText, llmImages, cancellationToken);
        llmSw.Stop();
        if (string.IsNullOrWhiteSpace(raw))
        {
            _logger.LogWarning(
                "[InvoiceImport] Appel LLM sans contenu en {ElapsedMs} ms : modèle={Model} chunks={Chunks} firstTokenMs={FirstTokenMs}",
                llmSw.ElapsedMilliseconds, modelRef.ProviderModelId, llmMetrics.StreamChunkCount, llmMetrics.FirstTokenMs);
            return Result.Failure<InvoiceImportResultDto>(Error.Validation("InvoiceImport",
                "L'IA n'a renvoyé aucune donnée (modèle surchargé ou mémoire insuffisante). "
                + "Réessayez ou configurez un modèle d'import plus léger dans les paramètres plateforme."));
        }

        _logger.LogInformation(
            "[InvoiceImport] Appel LLM terminé en {ElapsedMs} ms : modèle={Model} usedVision={UsedVision} caractères={Chars} "
            + "chunks={Chunks} firstTokenMs={FirstTokenMs} documentTypeDetected=pending",
            llmSw.ElapsedMilliseconds, activeModelRef.ProviderModelId, useVisionFallback, raw.Length,
            llmMetrics.StreamChunkCount, llmMetrics.FirstTokenMs);

        // 6. Parsing JSON robuste.
        var json = InvoiceImportParsing.ExtractFirstJsonObject(raw);
        LlmInvoiceExtraction? parsed = null;
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                parsed = JsonSerializer.Deserialize<LlmInvoiceExtraction>(json, JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Import facture : JSON renvoyé par le LLM non désérialisable.");
            }
        }

        if (parsed is null)
            return Result.Failure<InvoiceImportResultDto>(Error.Validation("InvoiceImport",
                "L'IA n'a pas pu produire des données exploitables à partir de ce fichier. "
                + "Réessayez ou saisissez la facture manuellement."));

        _logger.LogInformation(
            "[InvoiceImport] documentTypeDetected={DocumentType} confidence={Confidence}",
            parsed.DocumentType, parsed.Confidence);

        // 7-9. Validation, normalisation fiscale, rapprochement clients/produits.
        var matchSw = Stopwatch.StartNew();
        var dto = await MapValidateAndMatchAsync(parsed, extraction, textTruncated, cancellationToken);
        matchSw.Stop();

        totalSw.Stop();
        _logger.LogInformation(
            "[InvoiceImport] Import terminé en {TotalMs} ms (extraction={ExtractionMs} ms, LLM={LlmMs} ms, "
            + "rapprochement={MatchMs} ms) : lignes={LineCount}",
            totalSw.ElapsedMilliseconds, extractionSw.ElapsedMilliseconds, llmSw.ElapsedMilliseconds,
            matchSw.ElapsedMilliseconds, dto.Lines.Count);

        return Result.Success(dto);
    }

    // ========================================================================
    // Préchauffage & résolution du modèle
    // ========================================================================

    /// <summary>
    /// Préchauffe le modèle d'import : charge Ollama en mémoire et signale les problèmes RAM.
    /// </summary>
    public async Task<InvoiceImportWarmUpResult> WarmUpAsync(CancellationToken cancellationToken)
    {
        var modelRef = await ResolveModelRefAsync(null, cancellationToken);
        if (modelRef.Kind != LlmProviderKind.Ollama || string.IsNullOrWhiteSpace(modelRef.ProviderModelId))
        {
            return new InvoiceImportWarmUpResult(true, true, modelRef.ProviderModelId, null, null, null);
        }

        var modelName = modelRef.ProviderModelId;
        var readiness = await _readinessChecker.CheckAsync(modelName, cancellationToken);
        if (!readiness.IsReady)
        {
            return new InvoiceImportWarmUpResult(
                true,
                false,
                modelName,
                readiness.UserMessage,
                readiness.RequiredGiB,
                readiness.AvailableGiB);
        }

        try
        {
            var keepAlive = $"{Math.Clamp(_ollamaSettings.KeepAliveMinutes, 1, 1440)}m";
            var inferenceProfile = await _inferenceProfileResolver.ResolveForPlatformAsync(cancellationToken);
            var ok = await _ollamaClient.WarmUpModelAsync(
                modelName,
                keepAlive,
                cancellationToken,
                inferenceProfile: inferenceProfile);
            if (!ok)
            {
                return new InvoiceImportWarmUpResult(
                    true,
                    false,
                    modelName,
                    "Le préchauffage du modèle IA a échoué. L'import peut être plus long.",
                    readiness.RequiredGiB,
                    readiness.AvailableGiB);
            }

            await WarmUpVisionModelBestEffortAsync(keepAlive, inferenceProfile, cancellationToken);
            return new InvoiceImportWarmUpResult(true, true, modelName, null, readiness.RequiredGiB, readiness.AvailableGiB);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Import facture : préchauffage du modèle échoué pour {Model}", modelName);
            return new InvoiceImportWarmUpResult(
                true,
                false,
                modelName,
                "Le préchauffage du modèle IA a échoué. L'import peut être plus long.",
                readiness.RequiredGiB,
                readiness.AvailableGiB);
        }
    }

    private async Task WarmUpVisionModelBestEffortAsync(
        string keepAlive,
        OllamaInferenceProfile inferenceProfile,
        CancellationToken cancellationToken)
    {
        var visionId = ResolveVisionModelId();
        if (string.IsNullOrWhiteSpace(visionId))
            return;

        try
        {
            var readiness = await _readinessChecker.CheckAsync(visionId, cancellationToken);
            if (readiness.IsReady)
            {
                await _ollamaClient.WarmUpModelAsync(
                    visionId,
                    keepAlive,
                    cancellationToken,
                    inferenceProfile: inferenceProfile);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Import facture : préchauffage modèle vision {Model} ignoré", visionId);
        }
    }

    /// <summary>
    /// Résout le modèle à utiliser pour l'import : modèle explicite de la commande, sinon
    /// <see cref="OllamaSettings.InvoiceImportModel"/>, sinon <see cref="OllamaSettings.DefaultModel"/>.
    /// </summary>
    private async Task<ParsedModelRef> ResolveModelRefAsync(string? explicitModel, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(explicitModel))
            return NormalizeModelRef(explicitModel.Trim());

        try
        {
            var platformImport = await _platformAiSettings.GetInvoiceImportModelRefAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(platformImport))
                return NormalizeModelRef(platformImport);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Import facture : lecture du modèle d'import plateforme impossible ; utilisation de la configuration serveur.");
        }

        var model =
            !string.IsNullOrWhiteSpace(_ollamaSettings.InvoiceImportModel) ? _ollamaSettings.InvoiceImportModel.Trim()
            : !string.IsNullOrWhiteSpace(_ollamaSettings.DefaultModel) ? _ollamaSettings.DefaultModel.Trim()
            : "mistral";

        return NormalizeModelRef(model);
    }

    private string? ResolveVisionModelId()
    {
        var model = _ollamaSettings.InvoiceImportVisionModel?.Trim();
        return string.IsNullOrWhiteSpace(model) ? null : model;
    }

    private bool ShouldUseVisionFallback(
        AiDocumentExtractionResult extraction,
        string extractedText,
        int sourceImageCount,
        string? visionModelId)
    {
        if (string.IsNullOrWhiteSpace(visionModelId) || sourceImageCount == 0)
            return false;

        var isImage = string.Equals(extraction.Format, "image", StringComparison.OrdinalIgnoreCase);
        var isOcrPdf = extraction.OcrApplied
            && string.Equals(extraction.Format, "pdf", StringComparison.OrdinalIgnoreCase);
        if (!isImage && !isOcrPdf)
            return false;

        var minChars = Math.Max(0, _ollamaSettings.InvoiceImportVisionMinOcrChars);
        if (_ollamaSettings.InvoiceImportVisionOnEmptyOcr && extractedText.Length == 0)
            return true;

        return !InvoiceImportOcrQuality.IsSufficient(extractedText, minChars, 0.35);
    }

    private static ParsedModelRef NormalizeModelRef(string model)
    {
        var modelRef = ModelRef.Parse(model);
        if (string.IsNullOrEmpty(modelRef.CanonicalModelRef))
            modelRef = ModelRef.Parse($"{ModelRef.OllamaPrefix}{model}");
        return modelRef;
    }

    private int ResolveImportMaxOutputTokens() =>
        Math.Clamp(
            _ollamaSettings.ImportMaxOutputTokens > 0
                ? _ollamaSettings.ImportMaxOutputTokens
                : DefaultImportMaxOutputTokens,
            256,
            3072);

    private TimeSpan ResolveImportLlmTimeout() =>
        TimeSpan.FromSeconds(Math.Clamp(_ollamaSettings.ImportLlmTimeoutSeconds, 30, 600));

    private string ResolveSystemPrompt() =>
        _ollamaSettings.UseCompactImportPrompt ? CompactSystemPrompt : SystemPrompt;

    // ========================================================================
    // Appel LLM
    // ========================================================================

    private sealed record InvoiceImportLlmMetrics(int StreamChunkCount, long? FirstTokenMs);

    private async Task<(string Raw, InvoiceImportLlmMetrics Metrics)> CallLlmAsync(
        ParsedModelRef modelRef,
        string text,
        List<string> images,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        var userPrompt = BuildUserPrompt(text, images.Count > 0);
        var systemPrompt = ResolveSystemPrompt();
        var outputCap = Math.Min(Math.Max(1, _ollamaSettings.MaxTokens), ResolveImportMaxOutputTokens());
        var streamTimeout = ResolveImportLlmTimeout();
        var chunkCount = 0;
        long? firstTokenMs = null;
        var llmSw = Stopwatch.StartNew();

        if (modelRef.Kind == LlmProviderKind.Ollama)
        {
            var messages = new List<OllamaChatMessage>
            {
                new() { Role = "system", Content = systemPrompt },
                new()
                {
                    Role = "user",
                    Content = userPrompt,
                    Images = images.Count > 0 ? images : null
                }
            };

            var numCtx = InvoiceImportParsing.ResolveImportNumCtx(
                _ollamaSettings.NumCtx,
                systemPrompt.Length + userPrompt.Length,
                outputCap,
                images.Count > 0);
            var inferenceProfile = await _inferenceProfileResolver.ResolveForPlatformAsync(cancellationToken);
            var options = inferenceProfile.ApplyTo(new OllamaOptions
            {
                Temperature = 0,
                NumPredict = outputCap,
                NumCtx = numCtx
            });

            var request = new OllamaChatRequest
            {
                Model = modelRef.ProviderModelId,
                Messages = messages,
                Stream = true,
                Format = "json",
                KeepAlive = $"{Math.Clamp(_ollamaSettings.KeepAliveMinutes, 1, 1440)}m",
                Options = options
            };

            _logger.LogInformation(
                "[InvoiceImport] Appel LLM Ollama : modèle={Model} numCtx={NumCtx} numPredict={NumPredict} images={ImageCount} timeoutSec={TimeoutSec} inference_device={InferenceDevice} num_gpu_effective={NumGpuEffective}",
                modelRef.ProviderModelId,
                numCtx,
                outputCap,
                images.Count,
                (int)streamTimeout.TotalSeconds,
                inferenceProfile.Device,
                inferenceProfile.NumGpu);

            await foreach (var chunk in _ollamaClient.StreamChatAsync(request, cancellationToken, streamTimeout))
            {
                chunkCount++;
                if (!string.IsNullOrEmpty(chunk.Message?.Content))
                {
                    firstTokenMs ??= llmSw.ElapsedMilliseconds;
                    sb.Append(chunk.Message.Content);
                }
            }
        }
        else
        {
            var openRouter = await _platformAiSettings.GetOpenRouterCredentialsAsync(cancellationToken);
            var baseUrl = openRouter.BaseUrl;
            var apiKey = openRouter.ApiKey!;
            var messages = new List<OpenAiChatMessagePayload>
            {
                new() { Role = "system", Content = systemPrompt },
                BuildOpenAiUserMessage(text, images)
            };

            _logger.LogInformation(
                "[InvoiceImport] Appel LLM OpenRouter : modèle={Model} maxTokens={MaxTokens} images={ImageCount}",
                modelRef.ProviderModelId, outputCap, images.Count);

            await foreach (var chunk in _openAiClient.StreamChatAsOllamaCompatibleAsync(
                               baseUrl,
                               apiKey,
                               modelRef.ProviderModelId,
                               messages,
                               Array.Empty<OllamaToolDefinition>(),
                               0d,
                               outputCap,
                               cancellationToken))
            {
                chunkCount++;
                if (!string.IsNullOrEmpty(chunk.Message?.Content))
                {
                    firstTokenMs ??= llmSw.ElapsedMilliseconds;
                    sb.Append(chunk.Message.Content);
                }
            }
        }

        return (sb.ToString(), new InvoiceImportLlmMetrics(chunkCount, firstTokenMs));
    }

    private static string BuildUserPrompt(string text, bool hasImages)
    {
        var intro = hasImages
            ? "Analyse le document (image jointe"
            : "Analyse le document suivant";
        if (hasImages && !string.IsNullOrWhiteSpace(text))
            intro += " et texte OCR ci-dessous";
        else if (hasImages)
            intro += " uniquement";
        intro += ") et extrais les données commerciales au format JSON demandé.\n\n";

        if (string.IsNullOrWhiteSpace(text))
            return intro;

        return intro
            + "--- DÉBUT DU CONTENU OCR ---\n"
            + text
            + "\n--- FIN DU CONTENU OCR ---";
    }

    private static OpenAiChatMessagePayload BuildOpenAiUserMessage(string text, List<string> images)
    {
        var prompt = BuildUserPrompt(text, images.Count > 0);
        if (images.Count == 0)
            return new OpenAiChatMessagePayload { Role = "user", Content = prompt };

        var parts = new List<OpenAiContentPart> { new() { Type = "text", Text = prompt } };
        foreach (var b64 in images)
        {
            parts.Add(new OpenAiContentPart
            {
                Type = "image_url",
                ImageUrl = new OpenAiImageUrl { Url = "data:image/png;base64," + b64 }
            });
        }
        return new OpenAiChatMessagePayload { Role = "user", Content = parts };
    }

    // ========================================================================
    // Validation / normalisation / rapprochement
    // ========================================================================

    private async Task<InvoiceImportResultDto> MapValidateAndMatchAsync(
        LlmInvoiceExtraction parsed,
        AiDocumentExtractionResult extraction,
        bool textTruncated,
        CancellationToken cancellationToken)
    {
        var warnings = new List<string>();
        if (parsed.Warnings is { Count: > 0 })
        {
            warnings.AddRange(parsed.Warnings
                .Where(w => !string.IsNullOrWhiteSpace(w))
                .Select(w => w.Trim()));
        }

        if (textTruncated)
            warnings.Add("Le document est volumineux : seule une partie a été analysée. Vérifiez que rien ne manque.");

        if (InvoiceImportParsing.IsUnknownDocumentType(parsed.DocumentType))
            warnings.Add("Le document ne semble pas être une facture : vérifiez attentivement les données extraites.");
        if (InvoiceImportParsing.IsDeliveryNoteDocumentType(parsed.DocumentType))
            warnings.Add("Document importé depuis un bon de livraison : vérifiez la TVA, le numéro de facture et les montants avant validation.");
        if (InvoiceImportParsing.IsProformaDocumentType(parsed.DocumentType))
            warnings.Add("Document importé depuis un devis : vérifiez les conditions commerciales avant de facturer.");
        var documentType = InvoiceImportParsing.NormalizeDocumentType(parsed.DocumentType);

        var currency = InvoiceImportParsing.NormalizeCurrency(parsed.Currency);

        var issueDate = InvoiceImportParsing.ParseDate(parsed.IssueDate);
        var dueDate = InvoiceImportParsing.ParseDate(parsed.DueDate);

        // Lignes.
        var lines = new List<InvoiceImportLineDto>();
        var skipped = 0;
        foreach (var l in parsed.Lines ?? new List<LlmInvoiceLine>())
        {
            var mapped = InvoiceImportParsing.MapLine(l);
            if (mapped is null)
            {
                skipped++;
                continue;
            }

            lines.Add(mapped);
        }

        if (skipped > 0)
            warnings.Add($"{skipped} ligne(s) sans désignation ont été ignorées.");
        if (lines.Count == 0)
            warnings.Add("Aucune ligne d'article n'a pu être extraite : ajoutez les articles manuellement.");

        // Totaux + contrôle de cohérence.
        InvoiceImportTotalsDto? totals = parsed.Totals is null
            ? null
            : new InvoiceImportTotalsDto
            {
                TotalHT = parsed.Totals.TotalHT,
                TotalVat = parsed.Totals.TotalVat,
                TotalTTC = parsed.Totals.TotalTTC
            };

        if (lines.Count > 0 && lines.All(l => l.VatRatePercent == 19)
            && (InvoiceImportParsing.IsDeliveryNoteDocumentType(parsed.DocumentType)
                || totals?.TotalVat is null or 0))
            warnings.Add("TVA par défaut (19 %) appliquée : le document source ne mentionnait pas la TVA.");

        if (totals?.TotalHT is { } declaredHt && declaredHt > 0m && lines.Count > 0)
        {
            var computedHt = lines.Sum(x =>
                x.Quantity * x.UnitPriceHT * (1m - (x.DiscountPercent ?? 0m) / 100m));
            if (Math.Abs(computedHt - declaredHt) > declaredHt * 0.01m)
                warnings.Add("Le total HT extrait ne correspond pas exactement à la somme des lignes : vérifiez les montants.");
        }

        // Rapprochement clients/produits.
        var client = await MatchClientAsync(parsed.Client, warnings, cancellationToken);
        lines = await MatchProductsAsync(lines, warnings, cancellationToken);

        var confidence = InvoiceImportParsing.NormalizeConfidence(parsed.Confidence);

        return new InvoiceImportResultDto
        {
            DocumentType = documentType,
            InvoiceNumber = InvoiceImportParsing.CleanOrNull(parsed.InvoiceNumber),
            IssueDate = issueDate,
            DueDate = dueDate,
            Currency = currency,
            Client = client,
            Lines = lines,
            Totals = totals,
            Confidence = confidence,
            Warnings = warnings,
            ExtractionFormat = extraction.Format,
            OcrApplied = extraction.OcrApplied
        };
    }

    private async Task<InvoiceImportClientDto> MatchClientAsync(
        LlmParty? client,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var name = (client?.Name ?? string.Empty).Trim();
        var nif = (client?.Nif ?? string.Empty).Trim();
        var address = client?.Address;
        var hasNif = nif.Length > 0;

        var fallback = new InvoiceImportClientDto
        {
            IsNewClient = true,
            Name = InvoiceImportParsing.CleanOrNull(name),
            Nif = InvoiceImportParsing.CleanOrNull(nif),
            Email = InvoiceImportParsing.CleanOrNull(client?.Email),
            Phone = InvoiceImportParsing.CleanOrNull(client?.Phone),
            Street = InvoiceImportParsing.CleanOrNull(address?.Street),
            City = InvoiceImportParsing.CleanOrNull(address?.City),
            PostalCode = InvoiceImportParsing.CleanOrNull(address?.PostalCode),
            Governorate = InvoiceImportParsing.CleanOrNull(address?.Governorate),
            TaxType = hasNif ? "TAX_SUBJECT" : "NON_TAX_SUBJECT"
        };

        if (name.Length == 0 && !hasNif)
            return fallback;

        try
        {
            if (hasNif)
            {
                var byNif = await _mediator.Send(new GetClientsQuery(Search: nif, PageSize: 10), cancellationToken);
                var normalizedNif = InvoiceImportParsing.NormalizeNif(nif);
                var nifHit = byNif.Items.FirstOrDefault(c =>
                    !string.IsNullOrEmpty(c.Nif) && InvoiceImportParsing.NormalizeNif(c.Nif!) == normalizedNif);
                if (nifHit is not null)
                    return ToMatchedClient(nifHit);
            }

            if (name.Length > 0)
            {
                var byName = await _mediator.Send(new GetClientsQuery(Search: name, PageSize: 10), cancellationToken);
                var nameHits = byName.Items
                    .Where(c => InvoiceImportParsing.Similarity(c.Name, name) >= InvoiceImportParsing.NameMatchThreshold)
                    .ToList();
                if (nameHits.Count == 1)
                    return ToMatchedClient(nameHits[0]);
                if (nameHits.Count > 1)
                    warnings.Add($"Plusieurs clients existants correspondent à « {name} » : sélectionnez le bon dans le wizard.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Import facture : rapprochement client échoué, bascule en nouveau client.");
        }

        return fallback;
    }

    private static InvoiceImportClientDto ToMatchedClient(ClientListDto c) => new()
    {
        MatchedClientId = c.Id,
        MatchedClientName = c.Name,
        IsNewClient = false,
        Name = c.Name,
        Nif = c.Nif,
        Email = c.Email,
        Phone = c.Phone,
        Street = c.Street,
        City = c.City,
        PostalCode = c.PostalCode,
        Governorate = c.Governorate,
        TaxType = !string.IsNullOrWhiteSpace(c.Nif) ? "TAX_SUBJECT" : "NON_TAX_SUBJECT"
    };

    private async Task<List<InvoiceImportLineDto>> MatchProductsAsync(
        List<InvoiceImportLineDto> lines,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        if (lines.Count == 0)
            return lines;

        if (lines.Count > MaxLinesToMatch)
            warnings.Add($"Le rapprochement automatique des articles s'est limité aux {MaxLinesToMatch} premières lignes.");

        var result = new List<InvoiceImportLineDto>(lines.Count);
        var matchTasks = new List<Task<(int Index, InvoiceImportLineDto Line)>>(Math.Min(lines.Count, MaxLinesToMatch));

        for (var i = 0; i < Math.Min(lines.Count, MaxLinesToMatch); i++)
            matchTasks.Add(MatchSingleProductLineAsync(i, lines[i], cancellationToken));

        var matched = await Task.WhenAll(matchTasks);
        var matchedByIndex = matched.ToDictionary(m => m.Index, m => m.Line);

        for (var i = 0; i < lines.Count; i++)
        {
            if (matchedByIndex.TryGetValue(i, out var matchedLine))
                result.Add(matchedLine);
            else
                result.Add(lines[i]);
        }

        return result;
    }

    private async Task<(int Index, InvoiceImportLineDto Line)> MatchSingleProductLineAsync(
        int index,
        InvoiceImportLineDto line,
        CancellationToken cancellationToken)
    {
        try
        {
            var query = new GetProductsQuery(Search: line.Designation, IsActive: true, PageSize: 10);
            var res = await _mediator.Send(query, cancellationToken);
            if (res.IsSuccess)
            {
                var hits = res.Value.Items
                    .Where(p => InvoiceImportParsing.Similarity(p.Name, line.Designation) >= InvoiceImportParsing.NameMatchThreshold
                                || string.Equals(p.Code, line.Designation, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (hits.Count == 1)
                    return (index, line with { MatchedProductId = hits[0].Id, MatchedProductCode = hits[0].Code });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Import facture : rapprochement produit échoué pour « {Designation} ».", line.Designation);
        }

        return (index, line);
    }
}
