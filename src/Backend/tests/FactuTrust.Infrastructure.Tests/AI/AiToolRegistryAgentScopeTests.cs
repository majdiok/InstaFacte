using FactuTrust.Application.Features.AI;
using FactuTrust.Application.Features.AI.Commands;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Application.Features.AI.Tools;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class AiToolRegistryAgentScopeTests
{
    /// <summary>
    /// GARDE DE NON-RÉGRESSION : sans scope, GetDefinitionsForMode retourne EXACTEMENT le même catalogue
    /// qu'avant l'introduction du paramètre (assistant global inchangé).
    /// </summary>
    [Theory]
    [InlineData(AssistantMode.Default, false)]
    [InlineData(AssistantMode.Default, true)]
    [InlineData(AssistantMode.Compliance, false)]
    [InlineData(AssistantMode.ScreenAnalysis, false)]
    [InlineData(AssistantMode.StudioBuilder, true)]
    public void Default_Overload_Equals_Explicit_None(AssistantMode mode, bool enableMutationTools)
    {
        var withoutScope = AiToolRegistry.GetDefinitionsForMode(mode, enableMutationTools)
            .Select(t => t.Name).ToList();
        var withNone = AiToolRegistry.GetDefinitionsForMode(mode, enableMutationTools, AssistantAgentScope.None)
            .Select(t => t.Name).ToList();
        Assert.Equal(withoutScope, withNone);
    }

    [Fact]
    public void Scoped_Default_Mode_Returns_Only_Scope_Tools()
    {
        foreach (var scope in AiAgentScopeCatalog.AllScopes)
        {
            var scopeTools = AiAgentScopeCatalog.GetToolNames(scope);
            var defs = AiToolRegistry.GetDefinitionsForMode(AssistantMode.Default, enableMutationTools: false, scope);

            Assert.NotEmpty(defs);
            Assert.All(defs, d => Assert.Contains(d.Name, scopeTools));
            // Toujours strictement plus petit que le catalogue global (prefill réduit).
            var globalCount = AiToolRegistry.GetDefinitionsForMode(AssistantMode.Default, enableMutationTools: false).Count;
            Assert.True(defs.Count < globalCount, $"Scope {scope} : catalogue non réduit ({defs.Count}/{globalCount})");
        }
    }

    [Fact]
    public void Scoped_Default_Mode_Still_Excludes_Mutating_Tools_When_Disabled()
    {
        foreach (var scope in AiAgentScopeCatalog.AllScopes)
        {
            var defs = AiToolRegistry.GetDefinitionsForMode(AssistantMode.Default, enableMutationTools: false, scope);
            Assert.All(defs, d => Assert.False(d.IsMutating));
        }
    }

    /// <summary>Le scope est ignoré dans les modes non-Default (catalogues focalisés existants).</summary>
    [Theory]
    [InlineData(AssistantMode.Compliance)]
    [InlineData(AssistantMode.ScreenAnalysis)]
    [InlineData(AssistantMode.StudioBuilder)]
    public void Scope_Is_Ignored_For_Non_Default_Modes(AssistantMode mode)
    {
        var unscoped = AiToolRegistry.GetDefinitionsForMode(mode, enableMutationTools: false)
            .Select(t => t.Name).ToList();
        var scoped = AiToolRegistry.GetDefinitionsForMode(mode, enableMutationTools: false, AssistantAgentScope.Stock)
            .Select(t => t.Name).ToList();
        Assert.Equal(unscoped, scoped);
    }

    // ── ShouldApplyIntentSuffixForScope : les suffixes Sales/Fallback citent get_sales_revenue ──

    [Fact]
    public void Intent_Suffix_Always_Applied_Without_Scope()
    {
        foreach (AiToolIntentRouter.AiToolIntent intent in Enum.GetValues<AiToolIntentRouter.AiToolIntent>())
            Assert.True(SendChatMessageHandler.ShouldApplyIntentSuffixForScope(intent, AssistantAgentScope.None));
    }

    [Theory]
    [InlineData(AssistantAgentScope.Sales, true)]
    [InlineData(AssistantAgentScope.Accounting, true)]
    [InlineData(AssistantAgentScope.Treasury, true)]
    [InlineData(AssistantAgentScope.Crm, true)]
    [InlineData(AssistantAgentScope.Purchases, false)]
    [InlineData(AssistantAgentScope.Stock, false)]
    public void Sales_Intent_Suffix_Requires_Sales_Revenue_Tool_In_Scope(AssistantAgentScope scope, bool expected)
    {
        Assert.Equal(expected, SendChatMessageHandler.ShouldApplyIntentSuffixForScope(
            AiToolIntentRouter.AiToolIntent.Sales, scope));
        Assert.Equal(expected, SendChatMessageHandler.ShouldApplyIntentSuffixForScope(
            AiToolIntentRouter.AiToolIntent.Fallback, scope));
    }

    [Fact]
    public void Generic_Intent_Suffixes_Are_Kept_For_Scoped_Requests()
    {
        Assert.True(SendChatMessageHandler.ShouldApplyIntentSuffixForScope(
            AiToolIntentRouter.AiToolIntent.Synthesis, AssistantAgentScope.Stock));
        Assert.True(SendChatMessageHandler.ShouldApplyIntentSuffixForScope(
            AiToolIntentRouter.AiToolIntent.Greeting, AssistantAgentScope.Purchases));
        Assert.True(SendChatMessageHandler.ShouldApplyIntentSuffixForScope(
            AiToolIntentRouter.AiToolIntent.Chart, AssistantAgentScope.Stock));
    }

    /// <summary>
    /// Épinglage : compliance_check_invoice résout lui-même la facture — aucun paramètre requis
    /// (le modèle peut l'appeler sans identifiant) et plus aucun jargon « GUID » dans le schéma
    /// envoyé au modèle (il le répercutait mot pour mot à l'utilisateur).
    /// </summary>
    [Fact]
    public void ComplianceCheckInvoice_Has_No_Required_Params_And_No_Guid_Jargon()
    {
        var def = AiToolRegistry.GetToolDefinition("compliance_check_invoice");
        Assert.NotNull(def);
        Assert.Empty(def!.RequiredParameters);
        Assert.True(def.Parameters.ContainsKey("invoice_number"));
        Assert.DoesNotContain("GUID", def.Description, StringComparison.OrdinalIgnoreCase);
        foreach (var (name, param) in def.Parameters)
            Assert.DoesNotContain("GUID", param.Description, StringComparison.OrdinalIgnoreCase);
    }
}
