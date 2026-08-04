using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// Platform-wide AI model configuration (singleton). Drives the model used by the
/// assistant for every tenant, plus shared OpenRouter credentials.
/// </summary>
public interface IPlatformAiSettingsService
{
    /// <summary>Canonical model ref configured for the whole platform, or null when not set.</summary>
    Task<string?> GetDefaultModelRefAsync(CancellationToken cancellationToken = default);

    /// <summary>Persists the platform default model. Returns the normalized stored value.</summary>
    Task<string?> SetDefaultModelRefAsync(
        string? modelRef,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Modèle d'import de factures (prioritaire sur appsettings Ollama:InvoiceImportModel).</summary>
    Task<string?> GetInvoiceImportModelRefAsync(CancellationToken cancellationToken = default);

    /// <summary>Persiste le modèle d'import de factures.</summary>
    Task<string?> SetInvoiceImportModelRefAsync(
        string? modelRef,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Modèle de l'Assistant Studio (prioritaire sur le modèle Assistant plateforme).</summary>
    Task<string?> GetStudioAiModelRefAsync(CancellationToken cancellationToken = default);

    /// <summary>Persiste le modèle de l'Assistant Studio.</summary>
    Task<string?> SetStudioAiModelRefAsync(
        string? modelRef,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Moteur d'inférence Ollama (GPU auto ou CPU uniquement).</summary>
    Task<OllamaInferenceDevice> GetInferenceDeviceAsync(CancellationToken cancellationToken = default);

    /// <summary>Persiste le moteur d'inférence Ollama.</summary>
    Task<OllamaInferenceDevice> SetInferenceDeviceAsync(
        OllamaInferenceDevice device,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Masked OpenRouter settings for the back-office (never returns plaintext key).</summary>
    Task<PlatformOpenRouterSettingsDto> GetOpenRouterSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates OpenRouter settings. Empty/null <paramref name="apiKey"/> keeps the existing secret.
    /// Returns false when enabling without any configured key.
    /// </summary>
    Task<(bool Success, string? Error)> SetOpenRouterConfigAsync(
        bool isEnabled,
        string? displayName,
        string? baseUrl,
        string? apiKey,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves runtime OpenRouter credentials. ApiKey is null when disabled or missing/invalid.
    /// </summary>
    Task<PlatformOpenRouterCredentials> GetOpenRouterCredentialsAsync(CancellationToken cancellationToken = default);
}
