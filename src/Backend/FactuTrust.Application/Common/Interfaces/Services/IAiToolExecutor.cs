using FactuTrust.Application.Features.AI.DTOs;

namespace FactuTrust.Application.Common.Interfaces.Services;

public interface IAiToolExecutor
{
    Task<AiToolResult> ExecuteAsync(
        string toolName,
        Dictionary<string, object?> arguments,
        AiToolExecutionContext context,
        CancellationToken cancellationToken = default);

    IReadOnlyList<AiToolDefinition> GetAvailableTools();
}
