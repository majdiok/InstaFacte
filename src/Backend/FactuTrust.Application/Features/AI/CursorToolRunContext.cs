using System.Collections.Concurrent;
using System.Security.Cryptography;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Domain.Entities.AI;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Features.AI;

public interface ICursorToolRunRegistry
{
    void Register(CursorToolRunContext context);
    bool TryGet(Guid runId, out CursorToolRunContext context);
    void Complete(Guid runId);
}

public sealed class CursorToolRunContext
{
    public required Guid RunId { get; init; }
    public required string Token { get; init; }
    public required Conversation Conversation { get; init; }
    public required string? CorrelationId { get; init; }
    public required IAiToolExecutor ToolExecutor { get; init; }
    public required IAiToolExecutorScopeFactory ScopeFactory { get; init; }
    public required Guid TenantId { get; init; }
    public required string ConnectionString { get; init; }
    public required Guid UserId { get; init; }
    public string? Email { get; init; }
    public required UserRole Role { get; init; }
    public required IReadOnlySet<string> Permissions { get; init; }
    public ConcurrentQueue<ChatStreamEvent> ExtraEvents { get; } = new();
    public int ToolsExecuted;
    public string? StudioBuilderToolError;
    public string? AccumulatedDashboardJson;
    public List<string> AccumulatedSuggestedPrompts { get; } = new();
    public List<(string ToolName, string CallId)> ToolSources { get; } = new();

    public static string CreateToken()
    {
        Span<byte> nonce = stackalloc byte[32];
        RandomNumberGenerator.Fill(nonce);
        return Convert.ToHexString(nonce);
    }
}
