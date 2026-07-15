using FactuTrust.Application.Features.AI;
using FactuTrust.Application.Features.AI.Commands;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Application.Features.AI.Tools;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class AssistantModeResolverTests
{
    [Fact]
    public void Resolve_returns_StudioBuilder_when_option_set()
    {
        var cmd = new SendChatMessageCommand(null, "Table de suivi des conges",
            Options: new ChatRequestOptionsDto { AssistantMode = AssistantMode.StudioBuilder });
        Assert.Equal(AssistantMode.StudioBuilder, AssistantModeResolver.Resolve(cmd));
    }

    [Fact]
    public void ShouldEnableMutationTools_true_for_StudioBuilder_without_mutation_keywords()
    {
        const string prompt = "Table de suivi des conges : employe, type, date debut";
        Assert.False(AssistantModeResolver.QueryLikelyMutating(prompt));
        Assert.True(AssistantModeResolver.ShouldEnableMutationTools(AssistantMode.StudioBuilder, true, prompt));
    }

    [Fact]
    public void StudioBuilder_mode_includes_studio_generate_app_when_mutations_enabled()
    {
        var defs = AiToolRegistry.GetDefinitionsForMode(AssistantMode.StudioBuilder, enableMutationTools: true);
        Assert.Contains(defs, d => d.Name == "studio_generate_app");
        Assert.Contains(defs, d => d.Name == "studio_generate_system");
    }

    // ── ResolveAgentScope (assistants experts par module) ──

    [Fact]
    public void ResolveAgentScope_returns_requested_scope_in_default_mode()
    {
        var cmd = new SendChatMessageCommand(null, "CA du mois ?",
            Options: new ChatRequestOptionsDto { AgentScope = AssistantAgentScope.Sales });
        Assert.Equal(AssistantAgentScope.Sales,
            AssistantModeResolver.ResolveAgentScope(cmd, AssistantMode.Default, enableAgentScopesPlatformFlag: true));
    }

    [Fact]
    public void ResolveAgentScope_returns_None_when_kill_switch_disabled()
    {
        var cmd = new SendChatMessageCommand(null, "CA du mois ?",
            Options: new ChatRequestOptionsDto { AgentScope = AssistantAgentScope.Sales });
        Assert.Equal(AssistantAgentScope.None,
            AssistantModeResolver.ResolveAgentScope(cmd, AssistantMode.Default, enableAgentScopesPlatformFlag: false));
    }

    [Theory]
    [InlineData(AssistantMode.Compliance)]
    [InlineData(AssistantMode.ScreenAnalysis)]
    [InlineData(AssistantMode.StudioBuilder)]
    public void ResolveAgentScope_returns_None_for_non_default_modes(AssistantMode mode)
    {
        var cmd = new SendChatMessageCommand(null, "Question",
            Options: new ChatRequestOptionsDto { AgentScope = AssistantAgentScope.Stock });
        Assert.Equal(AssistantAgentScope.None,
            AssistantModeResolver.ResolveAgentScope(cmd, mode, enableAgentScopesPlatformFlag: true));
    }

    [Fact]
    public void ResolveAgentScope_returns_None_when_options_absent()
    {
        var cmd = new SendChatMessageCommand(null, "Question");
        Assert.Equal(AssistantAgentScope.None,
            AssistantModeResolver.ResolveAgentScope(cmd, AssistantMode.Default, enableAgentScopesPlatformFlag: true));
    }

    [Fact]
    public void ResolveAgentScope_returns_None_for_undefined_enum_value()
    {
        var cmd = new SendChatMessageCommand(null, "Question",
            Options: new ChatRequestOptionsDto { AgentScope = (AssistantAgentScope)99 });
        Assert.Equal(AssistantAgentScope.None,
            AssistantModeResolver.ResolveAgentScope(cmd, AssistantMode.Default, enableAgentScopesPlatformFlag: true));
    }
}