using FactuTrust.Application.Features.AI.DTOs;

namespace FactuTrust.Application.Features.AI;

/// <summary>
/// Restricts AI assistant scope for accounting firms operating in delegated client context.
/// Only the accounting expert assistant is allowed; sales/purchases/treasury scopes are denied.
/// </summary>
public static class FirmDelegatedAiScopePolicy
{
    public const string DeniedScopeMessage =
        "L'assistant IA de ce module n'est pas disponible pour les cabinets comptables.";

    public static AssistantAgentScope ResolveAllowedScope(
        bool isFirmDelegated,
        AssistantAgentScope requested)
    {
        if (!isFirmDelegated)
            return requested;

        if (requested is AssistantAgentScope.Sales
            or AssistantAgentScope.Purchases
            or AssistantAgentScope.Stock
            or AssistantAgentScope.Treasury
            or AssistantAgentScope.Crm)
        {
            throw new UnauthorizedAccessException(DeniedScopeMessage);
        }

        return AssistantAgentScope.Accounting;
    }
}
