using FactuTrust.Application.Features.AI;
using FactuTrust.Application.Features.AI.DTOs;

namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Client du sidecar TypeScript @cursor/sdk (catalogue + inférence locale).
/// Inerte tant que CursorSdk:Enabled est false ou que le pont n'a pas démarré.
/// </summary>
public interface ICursorAgentClient
{
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CursorRemoteModelInfo>> ListModelsAsync(
        string apiKey,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<CursorAgentStreamEvent> RunChatAsync(
        CursorChatRunRequest request,
        CancellationToken cancellationToken = default);

    Task<string> ExtractAsync(
        CursorExtractRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CursorRemoteModelInfo(
    string Id,
    string DisplayName,
    string? Description,
    IReadOnlyList<string> Aliases,
    IReadOnlyList<CursorRemoteModelParameter> Parameters,
    IReadOnlyList<CursorRemoteModelVariant> Variants);

public sealed record CursorRemoteModelParameter(
    string Id,
    string? DisplayName,
    IReadOnlyList<CursorRemoteModelParameterValue> Values);

public sealed record CursorRemoteModelParameterValue(string Value, string? DisplayName);

public sealed record CursorRemoteModelVariant(
    IReadOnlyList<CursorModelParam> Params,
    string DisplayName,
    string? Description,
    bool IsDefault);

public sealed record CursorToolSpec(
    string Name,
    string Description,
    IReadOnlyDictionary<string, object?> InputSchema);

public sealed record CursorImagePayload(string Data, string MimeType);

public sealed record CursorChatRunRequest(
    string ApiKey,
    ParsedModelRef Model,
    string UserText,
    IReadOnlyList<CursorImagePayload> Images,
    IReadOnlyList<CursorToolSpec> Tools,
    Guid RunId,
    string CallbackUrl,
    string CallbackToken,
    string ScratchDirectory);

public sealed record CursorExtractRequest(
    string ApiKey,
    ParsedModelRef Model,
    string SystemPrompt,
    string UserText,
    IReadOnlyList<CursorImagePayload> Images,
    string ScratchDirectory);

public sealed record CursorAgentStreamEvent(
    string Type,
    string? Text = null,
    string? ToolName = null,
    string? ToolCallId = null,
    string? AgentId = null,
    string? RunId = null,
    string? Status = null,
    string? Error = null,
    bool IsRetryable = false);
