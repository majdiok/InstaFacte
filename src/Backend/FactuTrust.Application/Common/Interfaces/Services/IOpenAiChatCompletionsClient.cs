using FactuTrust.Application.Features.AI.DTOs;

namespace FactuTrust.Application.Common.Interfaces.Services;

public interface IOpenAiChatCompletionsClient
{
    /// <summary>
    /// Streams chat completions and yields Ollama-shaped chunks so the existing tool loop can stay unchanged.
    /// </summary>
    IAsyncEnumerable<OllamaChatChunk> StreamChatAsOllamaCompatibleAsync(
        string baseUrl,
        string apiKey,
        string model,
        IReadOnlyList<OpenAiChatMessagePayload> messages,
        IReadOnlyList<OllamaToolDefinition> tools,
        double temperature,
        int maxTokens,
        CancellationToken cancellationToken = default,
        int? seed = null,
        OpenAiCompatibleCallOptions? options = null);

    Task<IReadOnlyList<OpenAiRemoteModelInfo>> ListModelsAsync(
        string baseUrl,
        string apiKey,
        CancellationToken cancellationToken = default,
        OpenAiCompatibleCallOptions? options = null);
}
