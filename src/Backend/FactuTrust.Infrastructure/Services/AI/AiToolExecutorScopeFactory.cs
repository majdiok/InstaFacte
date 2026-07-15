using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.AI.DTOs;
using Microsoft.Extensions.DependencyInjection;

namespace FactuTrust.Infrastructure.Services.AI;

public sealed class AiToolExecutorScopeFactory : IAiToolExecutorScopeFactory
{
    private readonly IServiceScopeFactory _scopeFactory;

    public AiToolExecutorScopeFactory(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<AiToolResult> ExecuteAsync(
        string toolName,
        Dictionary<string, object?> arguments,
        AiToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<IAiToolExecutor>();
        return await executor.ExecuteAsync(toolName, arguments, context, cancellationToken);
    }
}