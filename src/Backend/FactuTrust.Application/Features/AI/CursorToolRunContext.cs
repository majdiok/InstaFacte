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
    /// <summary>
    /// Compteur de lectures firm ANCRÉES (Lot 1.3 du plan v3) : incrémenté par
    /// <c>CursorToolCallbackService</c> uniquement quand un outil <c>get_firm_*</c> retourne
    /// <c>Success == true &amp;&amp; GroundedData == true</c> — distinct de <see cref="ToolsExecuted"/>
    /// (incrémenté même en erreur). Relu par le handler avant la persistance du chemin Cursor pour
    /// appliquer le même grounding gate que la boucle normale (couverture garantie, aucune exclusion).
    /// </summary>
    public int FirmGroundedReads;
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
