using FactuTrust.Application.Features.AI.DTOs;

namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Recommends the best locally-installed Ollama model based on hardware capabilities
/// (RAM, CPU, GPU/VRAM) and model characteristics (parameter count, tool-calling support,
/// quantization quality).
/// </summary>
public interface IAiModelRecommender
{
    /// <summary>
    /// Evaluates all installed Ollama models against the host hardware profile and returns
    /// the best-fit model, or <c>null</c> when no compatible local model is available.
    /// Results are cached for a short TTL to avoid repeated Ollama API calls.
    /// </summary>
    Task<AiModelRecommendationDto?> RecommendBestLocalModelAsync(CancellationToken cancellationToken = default);

    /// <summary>Invalidates the short-lived recommendation cache (e.g. after inference device change).</summary>
    void InvalidateCache();
}
