using FactuTrust.Application.Features.AI;
using FactuTrust.Application.Features.AI.DTOs;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class FirmDelegatedAiScopePolicyTests
{
    [Theory]
    [InlineData(AssistantAgentScope.None)]
    [InlineData(AssistantAgentScope.Accounting)]
    public void ResolveAllowedScope_firm_delegated_allows_accounting_scopes(AssistantAgentScope requested)
    {
        var resolved = FirmDelegatedAiScopePolicy.ResolveAllowedScope(true, requested);
        Assert.Equal(AssistantAgentScope.Accounting, resolved);
    }

    [Theory]
    [InlineData(AssistantAgentScope.Sales)]
    [InlineData(AssistantAgentScope.Purchases)]
    [InlineData(AssistantAgentScope.Stock)]
    [InlineData(AssistantAgentScope.Treasury)]
    [InlineData(AssistantAgentScope.Crm)]
    public void ResolveAllowedScope_firm_delegated_rejects_non_accounting_scopes(AssistantAgentScope requested)
    {
        var ex = Assert.Throws<UnauthorizedAccessException>(() =>
            FirmDelegatedAiScopePolicy.ResolveAllowedScope(true, requested));

        Assert.Equal(FirmDelegatedAiScopePolicy.DeniedScopeMessage, ex.Message);
    }

    [Theory]
    [InlineData(AssistantAgentScope.Sales)]
    [InlineData(AssistantAgentScope.Accounting)]
    [InlineData(AssistantAgentScope.None)]
    public void ResolveAllowedScope_non_firm_returns_requested(AssistantAgentScope requested)
    {
        Assert.Equal(requested, FirmDelegatedAiScopePolicy.ResolveAllowedScope(false, requested));
    }
}
