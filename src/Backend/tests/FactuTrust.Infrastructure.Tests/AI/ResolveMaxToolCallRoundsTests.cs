using FactuTrust.Application.Features.AI;
using FactuTrust.Application.Features.AI.Commands;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class ResolveMaxToolCallRoundsTests
{
    private static readonly OllamaInferenceProfile CpuProfile = new(
        OllamaInferenceDevice.CpuOnly,
        NumGpu: 0,
        NumThread: 8,
        NumBatch: 128,
        PreferAdaptiveChatNumCtx: false);

    [Fact]
    public void Cpu_SalesIntent_UsesCpuCap()
    {
        var rounds = SendChatMessageHandler.ResolveMaxToolCallRounds(
            isScreenAnalysis: false,
            screenAnalysisMaxRounds: 2,
            defaultMaxRounds: 2,
            cpuMaxToolCallRounds: 1,
            AssistantMode.Default,
            AiToolIntentRouter.AiToolIntent.Sales,
            CpuProfile);

        Assert.Equal(1, rounds);
    }

    [Fact]
    public void Cpu_FallbackIntent_KeepsDefaultRounds()
    {
        var rounds = SendChatMessageHandler.ResolveMaxToolCallRounds(
            isScreenAnalysis: false,
            screenAnalysisMaxRounds: 2,
            defaultMaxRounds: 2,
            cpuMaxToolCallRounds: 1,
            AssistantMode.Default,
            AiToolIntentRouter.AiToolIntent.Fallback,
            CpuProfile);

        Assert.Equal(2, rounds);
    }

    [Fact]
    public void Gpu_SalesIntent_KeepsDefaultRounds()
    {
        var rounds = SendChatMessageHandler.ResolveMaxToolCallRounds(
            isScreenAnalysis: false,
            screenAnalysisMaxRounds: 2,
            defaultMaxRounds: 2,
            cpuMaxToolCallRounds: 1,
            AssistantMode.Default,
            AiToolIntentRouter.AiToolIntent.Sales,
            new OllamaInferenceProfile(OllamaInferenceDevice.Gpu, null, null, 256, false));

        Assert.Equal(2, rounds);
    }

    [Fact]
    public void ScreenAnalysis_UsesScreenCap()
    {
        var rounds = SendChatMessageHandler.ResolveMaxToolCallRounds(
            isScreenAnalysis: true,
            screenAnalysisMaxRounds: 3,
            defaultMaxRounds: 2,
            cpuMaxToolCallRounds: 1,
            AssistantMode.ScreenAnalysis,
            AiToolIntentRouter.AiToolIntent.Fallback,
            CpuProfile);

        Assert.Equal(3, rounds);
    }

    // ── Lot 2.2 : budget explicite de rounds pour les tours du scope FirmMission ──
    // Un tour cabinet ne doit jamais dépasser 2 générations LLM (1 round outil + 1 synthèse) :
    // sur CPU, le scope FirmMission force donc systématiquement 1 round, quel que soit l'intent
    // brut détecté par le routeur (y compris une éventuelle mauvaise classification Sales).
    [Theory]
    [InlineData(AiToolIntentRouter.AiToolIntent.Sales)]
    [InlineData(AiToolIntentRouter.AiToolIntent.Fallback)]
    [InlineData(AiToolIntentRouter.AiToolIntent.Stock)]
    [InlineData(AiToolIntentRouter.AiToolIntent.Accounting)]
    [InlineData(AiToolIntentRouter.AiToolIntent.Forecasting)]
    public void FirmMission_Cpu_AlwaysOneRound_RegardlessOfIntent(AiToolIntentRouter.AiToolIntent toolIntent)
    {
        var rounds = SendChatMessageHandler.ResolveMaxToolCallRounds(
            isScreenAnalysis: false,
            screenAnalysisMaxRounds: 3,
            defaultMaxRounds: 2,
            cpuMaxToolCallRounds: 1,
            AssistantMode.Default,
            toolIntent,
            CpuProfile,
            agentScope: AssistantAgentScope.FirmMission);

        Assert.Equal(1, rounds);
    }

    [Fact]
    public void FirmMission_Gpu_KeepsDefaultRounds()
    {
        var rounds = SendChatMessageHandler.ResolveMaxToolCallRounds(
            isScreenAnalysis: false,
            screenAnalysisMaxRounds: 3,
            defaultMaxRounds: 2,
            cpuMaxToolCallRounds: 1,
            AssistantMode.Default,
            AiToolIntentRouter.AiToolIntent.Sales,
            new OllamaInferenceProfile(OllamaInferenceDevice.Gpu, null, null, 256, false),
            agentScope: AssistantAgentScope.FirmMission);

        Assert.Equal(2, rounds);
    }

    // ── PR 1.2 : budget dédié au modèle avancé Studio ──
    // Un tour Studio sur le modèle standard garde le comportement historique (intent Fallback ⇒
    // defaultMaxRounds, même sur CPU). Quand le modèle AVANCÉ a été retenu pour ce tour, le budget
    // StudioAdvancedMaxToolCallRounds (4 par défaut) s'applique, quel que soit le profil d'inférence :
    // le modèle avancé ne tourne pas sur le CPU de la plateforme.
    [Fact]
    public void StudioBuilder_Cpu_AdvancedModel_UsesAdvancedBudget()
    {
        var rounds = SendChatMessageHandler.ResolveMaxToolCallRounds(
            isScreenAnalysis: false,
            screenAnalysisMaxRounds: 2,
            defaultMaxRounds: 2,
            cpuMaxToolCallRounds: 1,
            AssistantMode.StudioBuilder,
            AiToolIntentRouter.AiToolIntent.Fallback,
            CpuProfile,
            studioAdvanced: true,
            studioAdvancedMaxToolCallRounds: 4);

        Assert.Equal(4, rounds);
    }

    [Fact]
    public void StudioBuilder_Cpu_StandardModel_KeepsHistoricalDefaultRounds()
    {
        var rounds = SendChatMessageHandler.ResolveMaxToolCallRounds(
            isScreenAnalysis: false,
            screenAnalysisMaxRounds: 2,
            defaultMaxRounds: 2,
            cpuMaxToolCallRounds: 1,
            AssistantMode.StudioBuilder,
            AiToolIntentRouter.AiToolIntent.Fallback,
            CpuProfile,
            studioAdvanced: false,
            studioAdvancedMaxToolCallRounds: 4);

        // Identique à l'appel sans les nouveaux paramètres : le budget avancé n'intervient pas.
        Assert.Equal(2, rounds);
        Assert.Equal(rounds, SendChatMessageHandler.ResolveMaxToolCallRounds(
            isScreenAnalysis: false,
            screenAnalysisMaxRounds: 2,
            defaultMaxRounds: 2,
            cpuMaxToolCallRounds: 1,
            AssistantMode.StudioBuilder,
            AiToolIntentRouter.AiToolIntent.Fallback,
            CpuProfile));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-3, 1)]
    [InlineData(7, 7)]
    [InlineData(20, 20)]
    [InlineData(99, 20)]
    public void StudioBuilder_AdvancedBudget_IsClampedBetween1And20(int configured, int expected)
    {
        var rounds = SendChatMessageHandler.ResolveMaxToolCallRounds(
            isScreenAnalysis: false,
            screenAnalysisMaxRounds: 2,
            defaultMaxRounds: 2,
            cpuMaxToolCallRounds: 1,
            AssistantMode.StudioBuilder,
            AiToolIntentRouter.AiToolIntent.Fallback,
            CpuProfile,
            studioAdvanced: true,
            studioAdvancedMaxToolCallRounds: configured);

        Assert.Equal(expected, rounds);
    }

    [Fact]
    public void NonStudio_Cpu_IgnoresAdvancedFlag()
    {
        // Le drapeau n'a de sens qu'en StudioBuilder : un tour Default sur CPU garde son plafond.
        var rounds = SendChatMessageHandler.ResolveMaxToolCallRounds(
            isScreenAnalysis: false,
            screenAnalysisMaxRounds: 2,
            defaultMaxRounds: 2,
            cpuMaxToolCallRounds: 1,
            AssistantMode.Default,
            AiToolIntentRouter.AiToolIntent.Sales,
            CpuProfile,
            studioAdvanced: true,
            studioAdvancedMaxToolCallRounds: 4);

        Assert.Equal(1, rounds);
    }
}
