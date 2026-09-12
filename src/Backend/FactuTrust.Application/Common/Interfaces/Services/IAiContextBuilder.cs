using FactuTrust.Application.Features.AI.DTOs;

namespace FactuTrust.Application.Common.Interfaces.Services;

public interface IAiContextBuilder
{
    /// <param name="studioOptions">
    /// Options du prompt StudioBuilder (modèle effectif, intention, tenant / utilisateur pour les
    /// digests de contexte). <c>null</c> hors mode Studio — comportement historique inchangé.
    /// </param>
    Task<string> BuildSystemPromptAsync(
        AssistantMode assistantMode = AssistantMode.Default,
        string? screenId = null,
        AssistantAgentScope agentScope = AssistantAgentScope.None,
        StudioPromptOptions? studioOptions = null,
        CancellationToken cancellationToken = default);
}
