using FactuTrust.Application.Features.AI;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Application.Features.AI.Tools;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

/// <summary>
/// Garde de la panne CPU silencieuse : sur une plateforme réglée en « CPU uniquement », le
/// sous-ensemble d'outils conçu pour dégrossir le catalogue Default s'appliquait AUSSI aux modes
/// focalisés, qui exposent déjà un catalogue curé. StudioBuilder tombait à un seul outil — aucun
/// <c>studio_*</c> — sans le moindre message d'erreur.
/// </summary>
public sealed class CpuToolSubsetScopeTests
{
    // ---- La décision d'armement ----

    [Theory]
    [InlineData(AssistantMode.StudioBuilder)]
    [InlineData(AssistantMode.Compliance)]
    [InlineData(AssistantMode.ScreenAnalysis)]
    public void The_cpu_subset_never_applies_to_a_focused_mode(AssistantMode mode)
    {
        Assert.False(AiToolIntentRouter.CpuSubsetApplies(mode, AssistantAgentScope.None, isCpuOnly: true));
    }

    [Fact]
    public void The_cpu_subset_still_applies_to_the_default_catalogue()
    {
        // C'est sa raison d'être : ~90 outils sur une machine sans GPU.
        Assert.True(AiToolIntentRouter.CpuSubsetApplies(
            AssistantMode.Default, AssistantAgentScope.None, isCpuOnly: true));
    }

    [Fact]
    public void An_expert_scope_is_already_curated_so_the_subset_stands_down()
    {
        Assert.False(AiToolIntentRouter.CpuSubsetApplies(
            AssistantMode.Default, AssistantAgentScope.Sales, isCpuOnly: true));
    }

    [Fact]
    public void On_gpu_nothing_is_ever_trimmed()
    {
        foreach (var mode in new[] { AssistantMode.Default, AssistantMode.StudioBuilder, AssistantMode.Compliance })
            Assert.False(AiToolIntentRouter.CpuSubsetApplies(mode, AssistantAgentScope.None, isCpuOnly: false));
    }

    // ---- Le pipeline complet, reproduit à l'identique de BuildOllamaTools ----

    private static IReadOnlyList<string> ExposedTools(
        AssistantMode mode, bool isCpuOnly, AssistantAgentScope agentScope = AssistantAgentScope.None)
    {
        var isScoped = mode == AssistantMode.Default && agentScope != AssistantAgentScope.None;
        var toolIntent = AiToolIntentRouter.Resolve("créer un rapport de ventes par produit", mode);
        var effectiveIntent = isScoped
            && toolIntent is not (AiToolIntentRouter.AiToolIntent.Greeting or AiToolIntentRouter.AiToolIntent.Synthesis)
            ? AiToolIntentRouter.AiToolIntent.Fallback
            : toolIntent;

        var cpuSubsetApplies = AiToolIntentRouter.CpuSubsetApplies(mode, agentScope, isCpuOnly);
        var useCpuCoreSubset = cpuSubsetApplies && effectiveIntent == AiToolIntentRouter.AiToolIntent.Fallback;
        var useCpuIntentSubset = cpuSubsetApplies && effectiveIntent is AiToolIntentRouter.AiToolIntent.Sales
            or AiToolIntentRouter.AiToolIntent.Stock
            or AiToolIntentRouter.AiToolIntent.Accounting;

        return AiToolRegistry.GetDefinitionsForMode(
                mode, enableMutationTools: true, agentScope,
                studioPlanPreview: true, studioModifyTools: true, studioViewTools: true, studioReportTools: true)
            .Where(t => AiToolIntentRouter.ShouldIncludeTool(
                t.Name, effectiveIntent, enableMutationTools: true, t.IsMutating,
                useCpuCoreSubset, useCpuIntentSubset))
            .Select(t => t.Name)
            .ToList();
    }

    [Fact]
    public void Studio_keeps_its_tools_on_a_cpu_only_platform()
    {
        var onCpu = ExposedTools(AssistantMode.StudioBuilder, isCpuOnly: true);

        Assert.Contains("studio_run_report", onCpu);
        Assert.Contains("studio_plan_report", onCpu);
        Assert.Contains("studio_plan_system", onCpu);
        Assert.Contains("studio_plan_view", onCpu);

        // Le catalogue Studio doit être identique quel que soit le matériel.
        Assert.Equal(
            ExposedTools(AssistantMode.StudioBuilder, isCpuOnly: false).OrderBy(n => n),
            onCpu.OrderBy(n => n));
    }

    [Fact]
    public void Compliance_keeps_its_own_core_tool_on_cpu()
    {
        // compliance_check_invoice est LE contrôle du mode ; il disparaissait sur CPU.
        Assert.Contains("compliance_check_invoice", ExposedTools(AssistantMode.Compliance, isCpuOnly: true));
    }

    [Fact]
    public void Screen_analysis_keeps_the_date_resolution_tool_on_cpu()
    {
        // resolve_reporting_period est la garde anti-hallucination de date : sa perte silencieuse
        // laissait le modèle inventer des périodes.
        Assert.Contains("resolve_reporting_period", ExposedTools(AssistantMode.ScreenAnalysis, isCpuOnly: true));
    }

    [Fact]
    public void The_default_catalogue_is_still_trimmed_on_cpu()
    {
        // Non-régression du comportement voulu : le gros catalogue reste dégrossi sur CPU.
        var onCpu = ExposedTools(AssistantMode.Default, isCpuOnly: true);
        var onGpu = ExposedTools(AssistantMode.Default, isCpuOnly: false);

        Assert.True(onCpu.Count < onGpu.Count);
        Assert.NotEmpty(onCpu);
    }
}
