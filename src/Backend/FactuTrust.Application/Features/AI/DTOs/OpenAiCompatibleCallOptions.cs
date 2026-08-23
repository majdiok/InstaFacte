using FactuTrust.Application.Configuration;

namespace FactuTrust.Application.Features.AI.DTOs;

/// <summary>
/// Options additive for OpenAI-compatible chat completions.
/// Null / default preserves the historical OpenRouter client behaviour.
/// </summary>
public sealed class OpenAiCompatibleCallOptions
{
    public const string OpenAiCompatibleClientName = "OpenAiCompatible";
    public const string ModalClientName = "ModalOpenAiCompatible";

    public bool AddOpenRouterHeaders { get; init; } = true;

    public string? SessionId { get; init; }

    public string? ReasoningEffort { get; init; }

    public bool OmitEmptyTools { get; init; }

    public bool RetryOnServiceUnavailable { get; init; }

    public int ColdStartRetries { get; init; }

    public string HttpClientName { get; init; } = OpenAiCompatibleClientName;

    public static OpenAiCompatibleCallOptions ForModal(ModalSettings settings, string? sessionId) => new()
    {
        AddOpenRouterHeaders = false,
        SessionId = sessionId,
        ReasoningEffort = string.IsNullOrWhiteSpace(settings.ReasoningEffort) ? "none" : settings.ReasoningEffort.Trim(),
        OmitEmptyTools = true,
        RetryOnServiceUnavailable = true,
        ColdStartRetries = Math.Max(0, settings.ColdStartRetries),
        HttpClientName = ModalClientName
    };
}
