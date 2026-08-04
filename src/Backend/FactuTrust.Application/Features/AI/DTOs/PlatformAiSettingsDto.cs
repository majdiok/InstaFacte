using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Features.AI.DTOs;

/// <summary>
/// Platform-wide AI settings for the back-office: the configured default model plus the
/// data needed to choose one (available Ollama models + hardware recommendation),
/// and shared OpenRouter credentials (masked).
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
    AiModelRecommendationDto? Recommendation,
    PlatformOpenRouterSettingsDto OpenRouter);

/// <summary>Masked OpenRouter credentials for the platform back-office.</summary>
public sealed record PlatformOpenRouterSettingsDto(
    bool IsEnabled,
    string? DisplayName,
    string? BaseUrl,
    string DefaultBaseUrl,
    bool IsApiKeyConfigured,
    string? ApiKeyLast4);

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

    /// <summary>When set, updates shared OpenRouter credentials. Empty ApiKey keeps the existing secret.</summary>
    public UpdatePlatformOpenRouterRequest? OpenRouter { get; init; }
}

/// <summary>OpenRouter credential update payload (nested under UpdatePlatformAiSettingsRequest).</summary>
public sealed class UpdatePlatformOpenRouterRequest
{
    public bool IsEnabled { get; init; }
    public string? DisplayName { get; init; }
    public string? BaseUrl { get; init; }
    /// <summary>Plaintext API key. Null/empty = keep existing encrypted key.</summary>
    public string? ApiKey { get; init; }
}

/// <summary>Runtime OpenRouter credentials resolved from platform Master DB.</summary>
public sealed record PlatformOpenRouterCredentials(
    bool IsEnabled,
    string? ApiKey,
    string BaseUrl);
