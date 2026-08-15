using System.Diagnostics;
using System.Text;
using System.Text.Json;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Application.Features.AI.Json;
using FactuTrust.Domain.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.AI;

/// <summary>Demande de rédaction : deux prompts, un schéma de sortie, rien d'autre.</summary>
public sealed record AiNarrativeCompletionRequest
{
    /// <summary>Prompt système : rôle, règles absolues, schéma attendu.</summary>
    public required string SystemPrompt { get; init; }

    /// <summary>Prompt utilisateur : les données déjà établies à mettre en forme.</summary>
    public required string UserPrompt { get; init; }

    /// <summary>
    /// Schéma JSON contraignant le décodage côté Ollama. Null = mode <c>"json"</c> (syntaxe seule).
    /// Sans effet sur OpenRouter, et abandonné automatiquement si le serveur le rejette.
    /// </summary>
    public JsonElement? OutputSchema { get; init; }

    /// <summary>Modèle explicite ; sinon le modèle plateforme par défaut.</summary>
    public string? ModelOverride { get; init; }

    /// <summary>Préfixe des logs et code porté par les erreurs renvoyées.</summary>
    public string ErrorCode { get; init; } = "AiNarrative";

    public int MaxOutputTokens { get; init; } = 2048;

    /// <summary>Budget total de l'appel. Dépassement ⇒ erreur, jamais d'attente indéfinie.</summary>
    public TimeSpan? Timeout { get; init; }
}

/// <summary>Texte brut rendu par le modèle, avant toute désérialisation.</summary>
public sealed record AiNarrativeCompletionOutcome(
    string RawContent,
    string? ModelUsed,
    int StreamChunkCount,
    long? FirstTokenMs);

/// <summary>
/// Complétion « texte en entrée, JSON typé en sortie ».
///
/// <para><b>Pourquoi un service distinct de <see cref="IAiStructuredExtractionPipeline"/>.</b> Ce
/// dernier exige un <c>Stream</c> de fichier et construit un prompt utilisateur orienté document
/// (« Analyse le document suivant… »). Le rendre générique aurait touché le chemin critique de
/// l'import de facture, éprouvé en production. On duplique donc la seule mécanique d'aiguillage de
/// fournisseur — une soixantaine de lignes — et l'on réutilise tout le reste : résolution de modèle,
/// schéma de sortie, gestion des identifiants, tolérance JSON.</para>
///
/// <para>Le service ne désérialise pas : il rend le texte brut. C'est l'appelant qui possède le
/// schéma, le valide et décide quoi faire d'une réponse écartée.</para>
/// </summary>
public interface IAiNarrativeCompletionService
{
    Task<Result<AiNarrativeCompletionOutcome>> CompleteAsync(
        AiNarrativeCompletionRequest request,
        CancellationToken cancellationToken);
}

/// <inheritdoc cref="IAiNarrativeCompletionService"/>
public sealed class AiNarrativeCompletionService : IAiNarrativeCompletionService
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(90);

    private readonly IOllamaClient _ollamaClient;
    private readonly IOpenAiChatCompletionsClient _openAiClient;
    private readonly IPlatformAiSettingsService _platformAiSettings;
    private readonly IOllamaInferenceProfileResolver _inferenceProfileResolver;
    private readonly OllamaSettings _ollamaSettings;
    private readonly ILogger<AiNarrativeCompletionService> _logger;

    public AiNarrativeCompletionService(
        IOllamaClient ollamaClient,
        IOpenAiChatCompletionsClient openAiClient,
        IPlatformAiSettingsService platformAiSettings,
        IOllamaInferenceProfileResolver inferenceProfileResolver,
        IOptions<OllamaSettings> ollamaSettings,
        ILogger<AiNarrativeCompletionService> logger)
    {
        _ollamaClient = ollamaClient;
        _openAiClient = openAiClient;
        _platformAiSettings = platformAiSettings;
        _inferenceProfileResolver = inferenceProfileResolver;
        _ollamaSettings = ollamaSettings.Value;
        _logger = logger;
    }

    public async Task<Result<AiNarrativeCompletionOutcome>> CompleteAsync(
        AiNarrativeCompletionRequest request,
        CancellationToken cancellationToken)
    {
        var code = request.ErrorCode;

        if (string.IsNullOrWhiteSpace(request.SystemPrompt) || string.IsNullOrWhiteSpace(request.UserPrompt))
            return Result.Failure<AiNarrativeCompletionOutcome>(
                Error.Validation(code, "Les deux prompts sont obligatoires."));

        var modelRef = await ResolveModelRefAsync(request.ModelOverride, cancellationToken);
        var timeout = request.Timeout ?? DefaultTimeout;
        var outputCap = Math.Clamp(request.MaxOutputTokens, 256, 8192);

        try
        {
            var (raw, chunks, firstTokenMs) = modelRef.Kind switch
            {
                LlmProviderKind.Ollama => await CallOllamaAsync(
                    modelRef, request, outputCap, timeout, cancellationToken),
                LlmProviderKind.OpenRouter => await CallOpenRouterAsync(
                    modelRef, request, outputCap, cancellationToken),
                _ => throw new NotSupportedException(
                    $"Fournisseur non géré pour la rédaction : {modelRef.Kind}.")
            };

            if (string.IsNullOrWhiteSpace(raw))
                return Result.Failure<AiNarrativeCompletionOutcome>(
                    Error.Validation(code, "Le modèle n'a rien renvoyé."));

            return Result.Success(new AiNarrativeCompletionOutcome(
                raw, modelRef.CanonicalModelRef, chunks, firstTokenMs));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Toute panne est convertie en échec métier : l'appelant retombe sur son déterministe.
            _logger.LogWarning(ex,
                "[{Scope}] Rédaction indisponible (modèle {Model}).", code, modelRef.CanonicalModelRef);
            return Result.Failure<AiNarrativeCompletionOutcome>(
                Error.Validation(code, $"Rédaction indisponible : {ex.Message}"));
        }
    }

    /// <summary>
    /// Modèle explicite, sinon modèle plateforme, sinon configuration serveur. L'échec de lecture
    /// de la plateforme ne fait pas échouer la rédaction : on retombe sur la configuration.
    /// </summary>
    private async Task<ParsedModelRef> ResolveModelRefAsync(
        string? explicitModel, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(explicitModel))
            return ModelRef.Parse(explicitModel.Trim());

        try
        {
            var platformDefault = await _platformAiSettings.GetDefaultModelRefAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(platformDefault))
                return ModelRef.Parse(platformDefault.Trim());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Rédaction : lecture du modèle plateforme impossible ; repli sur la configuration serveur.");
        }

        return ModelRef.Parse(_ollamaSettings.DefaultModel);
    }

    private async Task<(string Raw, int Chunks, long? FirstTokenMs)> CallOllamaAsync(
        ParsedModelRef modelRef,
        AiNarrativeCompletionRequest request,
        int outputCap,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var profile = await _inferenceProfileResolver.ResolveForPlatformAsync(cancellationToken);

        var chatRequest = new OllamaChatRequest
        {
            Model = modelRef.ProviderModelId,
            Messages =
            [
                new OllamaChatMessage { Role = "system", Content = request.SystemPrompt },
                new OllamaChatMessage { Role = "user", Content = request.UserPrompt }
            ],
            Stream = true,
            Format = request.OutputSchema.HasValue
                ? request.OutputSchema.Value
                : LlmOutputSchemas.PlainJson,
            KeepAlive = $"{Math.Clamp(_ollamaSettings.KeepAliveMinutes, 1, 1440)}m",
            Options = profile.ApplyTo(new OllamaOptions
            {
                // Température nulle : deux rédactions du même dossier doivent se ressembler.
                // Un dossier de révision n'est pas un exercice de style.
                Temperature = 0,
                NumPredict = outputCap,
                NumCtx = ResolveNumCtx(request, outputCap)
            })
        };

        return await ConsumeAsync(
            _ollamaClient.StreamChatAsync(chatRequest, cancellationToken, timeout));
    }

    private async Task<(string Raw, int Chunks, long? FirstTokenMs)> CallOpenRouterAsync(
        ParsedModelRef modelRef,
        AiNarrativeCompletionRequest request,
        int outputCap,
        CancellationToken cancellationToken)
    {
        var credentials = await _platformAiSettings.GetOpenRouterCredentialsAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(credentials.ApiKey))
            throw new InvalidOperationException(
                "Aucune clé OpenRouter configurée. Renseignez-la dans le back-office plateforme.");

        var messages = new List<OpenAiChatMessagePayload>
        {
            new() { Role = "system", Content = request.SystemPrompt },
            new() { Role = "user", Content = request.UserPrompt }
        };

        return await ConsumeAsync(_openAiClient.StreamChatAsOllamaCompatibleAsync(
            credentials.BaseUrl,
            credentials.ApiKey!,
            modelRef.ProviderModelId,
            messages,
            Array.Empty<OllamaToolDefinition>(),
            0d,
            outputCap,
            cancellationToken));
    }

    private static async Task<(string Raw, int Chunks, long? FirstTokenMs)> ConsumeAsync(
        IAsyncEnumerable<OllamaChatChunk> stream)
    {
        var builder = new StringBuilder();
        var chunks = 0;
        long? firstTokenMs = null;
        var stopwatch = Stopwatch.StartNew();

        await foreach (var chunk in stream)
        {
            chunks++;
            if (string.IsNullOrEmpty(chunk.Message?.Content)) continue;

            firstTokenMs ??= stopwatch.ElapsedMilliseconds;
            builder.Append(chunk.Message.Content);
        }

        return (builder.ToString(), chunks, firstTokenMs);
    }

    /// <summary>
    /// Fenêtre de contexte dimensionnée sur la taille réelle des prompts : un dossier de révision
    /// long tronqué en entrée produirait une note qui oublie la moitié des anomalies.
    /// </summary>
    private int ResolveNumCtx(AiNarrativeCompletionRequest request, int outputCap)
    {
        // ~4 caractères par jeton, plus une marge pour la sortie.
        var estimatedTokens = (request.SystemPrompt.Length + request.UserPrompt.Length) / 4 + outputCap + 512;
        var configured = _ollamaSettings.NumCtx > 0 ? _ollamaSettings.NumCtx : 8192;
        return Math.Clamp(Math.Max(estimatedTokens, configured), 4096, 32768);
    }
}
