using FactuTrust.Application.Features.AI.DTOs;

namespace FactuTrust.Application.Common.Interfaces.Services;

public interface IAiContextBuilder
{
    Task<string> BuildSystemPromptAsync(
        AssistantMode assistantMode = AssistantMode.Default,
        string? screenId = null,
        AssistantAgentScope agentScope = AssistantAgentScope.None,
        CancellationToken cancellationToken = default);
}
