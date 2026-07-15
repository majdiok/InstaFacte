using FactuTrust.Application.Features.AI.DTOs;

namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Exécute un outil IA dans un scope DI isolé (DbContext dédié) pour permettre la parallélisation
/// des outils read-only qui passent par MediatR.
/// </summary>
public interface IAiToolExecutorScopeFactory
{
    Task<AiToolResult> ExecuteAsync(
        string toolName,
        Dictionary<string, object?> arguments,
        AiToolExecutionContext context,
        CancellationToken cancellationToken = default);
}