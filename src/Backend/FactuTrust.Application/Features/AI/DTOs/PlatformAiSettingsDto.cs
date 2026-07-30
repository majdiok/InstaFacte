using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Features.AI.DTOs;

/// <summary>
/// Platform-wide AI settings for the back-office: the configured default model plus the
/// data needed to choose one (available Ollama models + hardware recommendation).
/// Returned by GET /api/platform/ai-settings.
/// </summary>
public sealed record PlatformAiSettingsDto(
    string? ConfiguredModelRef,
    string? InvoiceImportModelRef,
    /// <summary>Modèle dédié à l'Assistant Studio (IA) ; null = modèle Assistant / serveur.</summary>
    string? StudioAiModelRef,
    /// <summary>Modèle vision serveur (appsettings Ollama:InvoiceImportVisionModel), lecture seule.</summary>
    string? ServerInvoiceImportVisionModel,
    OllamaInferenceDevice InferenceDevice,
    bool IsOllamaAssistantConfigured,
    IReadOnlyList<UnifiedAiModelInfo> AvailableModels,
    AiModelRecommendationDto? Recommendation);

/// <summary>Request body for PUT /api/platform/ai-settings.</summary>
public sealed class UpdatePlatformAiSettingsRequest
{
    public string? ModelRef { get; init; }

    /// <summary>Modèle dédié à l'import de factures (ex. ollama:qwen2.5:7b-instruct).</summary>
    public string? InvoiceImportModelRef { get; init; }

    /// <summary>Modèle dédié à l'Assistant Studio (ex. ollama:qwen2.5:7b-instruct).</summary>
    public string? StudioAiModelRef { get; init; }

    /// <summary>Moteur d'inférence Ollama (GPU auto ou CPU uniquement).</summary>
    public OllamaInferenceDevice? InferenceDevice { get; init; }
}
