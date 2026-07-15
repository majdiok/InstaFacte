using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Features.AI;

/// <summary>
/// Options matérielles Ollama résolues pour la plateforme (chat, import, warm-up, keep-alive).
/// </summary>
public sealed record OllamaInferenceProfile(
    OllamaInferenceDevice Device,
    int? NumGpu,
    int? NumThread,
    int? NumBatch,
    bool PreferAdaptiveChatNumCtx)
{
    public OllamaOptions ApplyTo(OllamaOptions options) =>
        options with
        {
            NumGpu = NumGpu,
            NumThread = NumThread,
            NumBatch = NumBatch
        };
}