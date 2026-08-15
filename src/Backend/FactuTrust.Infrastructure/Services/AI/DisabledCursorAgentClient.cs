using System.Runtime.CompilerServices;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.AI.DTOs;

namespace FactuTrust.Infrastructure.Services.AI;

/// <summary>Client inerte : aucun process Node, aucun appel réseau. Utilisé si le pont n'est pas enregistré.</summary>
public sealed class DisabledCursorAgentClient : ICursorAgentClient
{
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<IReadOnlyList<CursorRemoteModelInfo>> ListModelsAsync(
        string apiKey,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CursorRemoteModelInfo>>(Array.Empty<CursorRemoteModelInfo>());

    public async IAsyncEnumerable<CursorAgentStreamEvent> RunChatAsync(
        CursorChatRunRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _ = request;
        _ = cancellationToken;
        yield return new CursorAgentStreamEvent(
            "error",
            Error: "Le pont Cursor SDK n'est pas démarré.");
        await Task.CompletedTask;
    }

    public Task<string> ExtractAsync(
        CursorExtractRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = request;
        _ = cancellationToken;
        throw new InvalidOperationException("Le pont Cursor SDK n'est pas démarré.");
    }
}
