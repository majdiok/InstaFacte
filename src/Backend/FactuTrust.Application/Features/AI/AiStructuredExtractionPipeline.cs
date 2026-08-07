using System.Diagnostics;
using System.Text;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Domain.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.AI;

/// <summary>
/// Demande d'extraction structurée : un fichier + un prompt système décrivant le JSON attendu.
/// Le pipeline ne connaît pas le schéma — il rend le texte brut du LLM, à charge de l'appelant
/// de le désérialiser.
/// </summary>
public sealed record AiStructuredExtractionRequest
{
    public required Stream FileStream { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }

    /// <summary>Prompt système décrivant le schéma JSON attendu.</summary>
    public required string SystemPrompt { get; init; }

    /// <summary>Modèle explicite (<c>ollama:…</c> / <c>openrouter:…</c>), sinon résolution par configuration.</summary>
    public string? ModelOverride { get; init; }

    /// <summary>
    /// Code porté par les <see cref="Error.Validation(string, string)"/> renvoyées et préfixe des logs.
    /// L'import de facture du wizard passe « InvoiceImport » afin de conserver ses messages à l'identique.
    /// </summary>
    public string ErrorCode { get; init; } = "AiExtraction";

    public int MaxTextChars { get; init; } = 60_000;

    public int MaxImages { get; init; } = 10;
}

/// <summary>Résultat brut d'un appel d'extraction structurée, avant désérialisation.</summary>
public sealed record AiStructuredExtractionOutcome(
    string RawContent,
    AiDocumentExtractionResult Extraction,
    bool TextTruncated,
    string? ModelUsed,
    bool VisionUsed,
    int StreamChunkCount,
    long? FirstTokenMs);

/// <summary>
/// Mécanique commune à toutes les extractions documentaires pilotées par un LLM :
/// extraction texte/OCR → décision de repli vision → contrôle de disponibilité du fournisseur →
/// appel one-shot → texte brut.
///
/// Volontairement agnostique du domaine : l'import de facture du wizard de facturation et
/// l'import de pièce comptable partagent ce pipeline et ne diffèrent que par leur prompt et
/// leur désérialisation.
/// </summary>
public interface IAiStructuredExtractionPipeline
{
    Task<Result<AiStructuredExtractionOutcome>> RunAsync(
        AiStructuredExtractionRequest request,
        CancellationToken cancellationToken);

    /// <summary>Résout le modèle d'extraction retenu (utile pour les diagnostics/capacités).</summary>
    Task<ParsedModelRef> ResolveModelRefAsync(string? explicitModel, CancellationToken cancellationToken);

    /// <summary>Identifiant du modèle vision d'import, ou null si aucun n'est configuré.</summary>
    string? ResolveVisionModelId();

    /// <summary>Préchauffage best-effort du modèle d'extraction (et du modèle vision si présent).</summary>
    Task<InvoiceImportWarmUpResult> WarmUpAsync(CancellationToken cancellationToken);
}

/// <inheritdoc cref="IAiStructuredExtractionPipeline"/>
public sealed class AiStructuredExtractionPipeline : IAiStructuredExtractionPipeline
{
    private const int DefaultImportMaxOutputTokens = 1536;

    private readonly IOllamaClient _ollamaClient;
    private readonly IOpenAiChatCompletionsClient _openAiClient;
    private readonly IAiDocumentTextExtractor _documentTextExtractor;
    private readonly IOllamaModelReadinessChecker _readinessChecker;
    private readonly IPlatformAiSettingsService _platformAiSettings;
    private readonly IOllamaInferenceProfileResolver _inferenceProfileResolver;
    private readonly ILogger<AiStructuredExtractionPipeline> _logger;
    private readonly OllamaSettings _ollamaSettings;

    public AiStructuredExtractionPipeline(
        IOllamaClient ollamaClient,
        IOpenAiChatCompletionsClient openAiClient,
        IAiDocumentTextExtractor documentTextExtractor,
        IOllamaModelReadinessChecker readinessChecker,
        IPlatformAiSettingsService platformAiSettings,
        IOllamaInferenceProfileResolver inferenceProfileResolver,
        ILogger<AiStructuredExtractionPipeline> logger,
        IOptions<OllamaSettings> ollamaSettings)
    {
        _ollamaClient = ollamaClient;
        _openAiClient = openAiClient;
        _documentTextExtractor = documentTextExtractor;
        _readinessChecker = readinessChecker;
        _platformAiSettings = platformAiSettings;
        _inferenceProfileResolver = inferenceProfileResolver;
        _logger = logger;
        _ollamaSettings = ollamaSettings.Value;
    }

    public async Task<Result<AiStructuredExtractionOutcome>> RunAsync(
        AiStructuredExtractionRequest request,
        CancellationToken cancellationToken)
    {
        var code = request.ErrorCode;

        // 1. Résolution du modèle texte (JSON structuré).
        var modelRef = await ResolveModelRefAsync(request.ModelOverride, cancellationToken);
        var visionModelId = ResolveVisionModelId();
        var wantPrimaryVision = AiModelCapabilityDetector.DetectVisionSupport(modelRef.ProviderModelId);

        // 2. Extraction du texte (+ image base64 pour fallback vision sur photos).
        var extractionSw = Stopwatch.StartNew();
        var extraction = await _documentTextExtractor.ExtractAsync(
            request.FileStream,
            request.FileName,
            request.ContentType,
            new AiDocumentExtractOptions { RenderPagesAsImages = wantPrimaryVision },
            cancellationToken);

        if (!extraction.Success)
            return Result.Failure<AiStructuredExtractionOutcome>(Error.Validation(
                code, extraction.ErrorMessage ?? "Le fichier n'a pas pu être lu."));

        var sourceImages = extraction.Pages
            .Select(p => p.ImageBase64)
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .Select(b => b!)
            .Take(request.MaxImages)
            .ToList();

        var extractedText = (extraction.Text ?? string.Empty).Trim();
        var ocrQuality = InvoiceImportOcrQuality.Score(extractedText);
        var useVisionFallback = ShouldUseVisionFallback(extraction, extractedText, sourceImages.Count, visionModelId);

        if (extractedText.Length == 0 && sourceImages.Count == 0)
            return Result.Failure<AiStructuredExtractionOutcome>(Error.Validation(
                code, "Aucun contenu exploitable n'a été trouvé dans le fichier."));

        if (extractedText.Length == 0 && !useVisionFallback)
        {
            return Result.Failure<AiStructuredExtractionOutcome>(Error.Validation(code,
                "Photo illisible : OCR vide et aucun modèle vision d'import configuré (clé InvoiceImportVisionModel, ex. llava). "
                + "Exécutez scripts/install-tessdata.ps1 ou contactez l'administrateur plateforme."));
        }

        var textTruncated = extraction.Truncated;
        if (extractedText.Length > request.MaxTextChars)
        {
            extractedText = extractedText[..request.MaxTextChars];
            textTruncated = true;
        }

        extractionSw.Stop();
        _logger.LogInformation(
            "[{Scope}] Extraction terminée en {ElapsedMs} ms : format={Format} ocr={Ocr} caractères={Chars} "
            + "ocrQuality={OcrQuality:F2} sourceImages={ImageCount} visionFallback={VisionFallback}",
            code, extractionSw.ElapsedMilliseconds, extraction.Format, extraction.OcrApplied, extractedText.Length,
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
                    return Result.Failure<AiStructuredExtractionOutcome>(Error.Validation(code,
                        "Le moteur IA InstaFact est indisponible. Vérifiez que le service est démarré sur le serveur."));

                var visionReadiness = await _readinessChecker.CheckAsync(activeModelRef.ProviderModelId!, cancellationToken);
                if (!visionReadiness.IsReady)
                    return Result.Failure<AiStructuredExtractionOutcome>(Error.Validation(code,
                        visionReadiness.UserMessage ?? "Le modèle vision d'import IA n'est pas prêt."));
            }
        }
        else if (modelRef.Kind == LlmProviderKind.Ollama)
        {
            if (!await _ollamaClient.IsAvailableAsync(cancellationToken))
                return Result.Failure<AiStructuredExtractionOutcome>(Error.Validation(code,
                    "Le moteur IA InstaFact est indisponible. Vérifiez que le service est démarré sur le serveur."));

            var readiness = await _readinessChecker.CheckAsync(modelRef.ProviderModelId!, cancellationToken);
            if (!readiness.IsReady)
                return Result.Failure<AiStructuredExtractionOutcome>(Error.Validation(code,
                    readiness.UserMessage ?? "Le modèle d'import IA n'est pas prêt."));
        }
        else
        {
            var credentials = await _platformAiSettings.GetOpenRouterCredentialsAsync(cancellationToken);
            if (string.IsNullOrEmpty(credentials.ApiKey))
                return Result.Failure<AiStructuredExtractionOutcome>(Error.Validation(code,
                    "Aucune clé API OpenRouter configurée. Configurez-la dans le back-office plateforme > Configuration IA (OpenRouter)."));
        }

        // 4. Appel LLM one-shot (texte seul ou vision hybride).
        var llmSw = Stopwatch.StartNew();
        var (raw, chunkCount, firstTokenMs) = await CallLlmAsync(
            activeModelRef, request.SystemPrompt, extractedText, llmImages, code, cancellationToken);
        llmSw.Stop();

        if (string.IsNullOrWhiteSpace(raw))
        {
            _logger.LogWarning(
                "[{Scope}] Appel LLM sans contenu en {ElapsedMs} ms : modèle={Model} chunks={Chunks} firstTokenMs={FirstTokenMs}",
                code, llmSw.ElapsedMilliseconds, modelRef.ProviderModelId, chunkCount, firstTokenMs);
            return Result.Failure<AiStructuredExtractionOutcome>(Error.Validation(code,
                "L'IA n'a renvoyé aucune donnée (modèle surchargé ou mémoire insuffisante). "
                + "Réessayez ou configurez un modèle d'import plus léger dans les paramètres plateforme."));
        }

        _logger.LogInformation(
            "[{Scope}] Appel LLM terminé en {ElapsedMs} ms : modèle={Model} usedVision={UsedVision} caractères={Chars} "
            + "chunks={Chunks} firstTokenMs={FirstTokenMs}",
            code, llmSw.ElapsedMilliseconds, activeModelRef.ProviderModelId, useVisionFallback, raw.Length,
            chunkCount, firstTokenMs);

        return Result.Success(new AiStructuredExtractionOutcome(
            raw,
            extraction,
            textTruncated,
            activeModelRef.ProviderModelId,
            useVisionFallback,
            chunkCount,
            firstTokenMs));
    }

    // ========================================================================
    // Préchauffage & résolution du modèle
    // ========================================================================

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
            _logger.LogDebug(ex, "Import IA : préchauffage du modèle échoué pour {Model}", modelName);
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
            _logger.LogDebug(ex, "Import IA : préchauffage modèle vision {Model} ignoré", visionId);
        }
    }

    /// <summary>
    /// Résout le modèle à utiliser : modèle explicite de la commande, sinon le modèle d'import
    /// configuré au niveau plateforme, sinon <see cref="OllamaSettings.InvoiceImportModel"/>,
    /// sinon <see cref="OllamaSettings.DefaultModel"/>.
    /// </summary>
    public async Task<ParsedModelRef> ResolveModelRefAsync(string? explicitModel, CancellationToken cancellationToken)
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
                "Import IA : lecture du modèle d'import plateforme impossible ; utilisation de la configuration serveur.");
        }

        var model =
            !string.IsNullOrWhiteSpace(_ollamaSettings.InvoiceImportModel) ? _ollamaSettings.InvoiceImportModel.Trim()
            : !string.IsNullOrWhiteSpace(_ollamaSettings.DefaultModel) ? _ollamaSettings.DefaultModel.Trim()
            : "mistral";

        return NormalizeModelRef(model);
    }

    public string? ResolveVisionModelId()
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

    // ========================================================================
    // Appel LLM
    // ========================================================================

    private async Task<(string Raw, int ChunkCount, long? FirstTokenMs)> CallLlmAsync(
        ParsedModelRef modelRef,
        string systemPrompt,
        string text,
        List<string> images,
        string scope,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        var userPrompt = BuildUserPrompt(text, images.Count > 0);
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
                "[{Scope}] Appel LLM Ollama : modèle={Model} numCtx={NumCtx} numPredict={NumPredict} images={ImageCount} timeoutSec={TimeoutSec} inference_device={InferenceDevice} num_gpu_effective={NumGpuEffective}",
                scope,
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
                "[{Scope}] Appel LLM OpenRouter : modèle={Model} maxTokens={MaxTokens} images={ImageCount}",
                scope, modelRef.ProviderModelId, outputCap, images.Count);

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

        return (sb.ToString(), chunkCount, firstTokenMs);
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
}
