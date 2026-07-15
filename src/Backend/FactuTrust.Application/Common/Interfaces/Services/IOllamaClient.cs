using FactuTrust.Application.Features.AI;
using FactuTrust.Application.Features.AI.DTOs;

namespace FactuTrust.Application.Common.Interfaces.Services;

public interface IOllamaClient
{
    IAsyncEnumerable<OllamaChatChunk> StreamChatAsync(
        OllamaChatRequest request,
        CancellationToken cancellationToken = default,
        TimeSpan? streamReadTimeout = null,
        bool useGenerationGate = true);

    Task<IReadOnlyList<OllamaModelInfo>> ListModelsAsync(
        CancellationToken cancellationToken = default);

    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true if the model name matches an installed Ollama model (uses a short-lived cache of /api/tags).
    /// </summary>
    Task<bool> IsModelInstalledAsync(string modelName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a minimal non-streaming request so Ollama loads the target model into memory.
    /// </summary>
    Task<bool> WarmUpModelAsync(
        string modelName,
        string? keepAlive = null,
        CancellationToken cancellationToken = default,
        int? numCtx = null,
        int? numBatch = null,
        OllamaInferenceProfile? inferenceProfile = null);

    /// <summary>
    /// Retrieves detailed metadata for a single model via POST /api/show (parameter size,
    /// quantization level, family). Results are cached per model name.
    /// </summary>
    Task<OllamaModelShowResponse?> ShowModelAsync(string modelName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists models currently loaded in Ollama memory via GET /api/ps, including
    /// VRAM allocation data useful for GPU-fit scoring.
    /// </summary>
    Task<OllamaProcessListResponse?> ListRunningModelsAsync(CancellationToken cancellationToken = default);
}
