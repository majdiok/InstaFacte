using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.AI;
using FactuTrust.Application.Features.AI.Commands;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class ShouldForceFinalSynthesisTests
{
    [Theory]
    [InlineData(false, true, false, false, true, false)]
    [InlineData(false, true, false, true, true, true)]
    [InlineData(true, true, false, true, true, false)]
    [InlineData(false, false, false, true, true, false)]
    [InlineData(false, true, true, true, true, false)]
    [InlineData(false, true, false, false, false, true)]
    public void Returns_Expected(
        bool isScreenAnalysis,
        bool forceEnabled,
        bool meaningfulResponseDelivered,
        bool toolsWereExecuted,
        bool onlyAfterTools,
        bool expected)
    {
        var result = SendChatMessageHandler.ShouldForceFinalSynthesis(
            isScreenAnalysis,
            forceEnabled,
            meaningfulResponseDelivered,
            toolsWereExecuted,
            onlyAfterTools);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ShortFinalRoundWithoutTools_SkipsSynthesisWhenOnlyAfterTools()
    {
        const string preamble = "Je";
        Assert.False(AssistantVisibleContentFormatter.HasMeaningfulAssistantText(preamble, minChars: 80));

        var synthesis = SendChatMessageHandler.ShouldForceFinalSynthesis(
            isScreenAnalysis: false,
            forceEnabled: true,
            meaningfulResponseDelivered: false,
            toolsWereExecuted: false,
            onlyAfterTools: true);

        Assert.False(synthesis);
    }

    [Fact]
    public void ShortFinalRoundAfterTools_TriggersSynthesis()
    {
        var synthesis = SendChatMessageHandler.ShouldForceFinalSynthesis(
            isScreenAnalysis: false,
            forceEnabled: true,
            meaningfulResponseDelivered: false,
            toolsWereExecuted: true,
            onlyAfterTools: true);

        Assert.True(synthesis);
    }

    [Fact]
    public void FullProse_SkipsSynthesisWhenDelivered()
    {
        var prose = new string('a', 100);
        Assert.True(AssistantVisibleContentFormatter.HasMeaningfulAssistantText(prose, minChars: 80));

        var synthesis = SendChatMessageHandler.ShouldForceFinalSynthesis(
            isScreenAnalysis: false,
            forceEnabled: true,
            meaningfulResponseDelivered: true,
            toolsWereExecuted: true,
            onlyAfterTools: true);

        Assert.False(synthesis);
    }

    [Fact]
    public void GreetingIntent_UsesLowerMeaningfulThreshold()
    {
        var settings = new OllamaSettings { MinAssistantTextCharsForCompleteResponse = 80 };
        var min = SendChatMessageHandler.ResolveMinMeaningfulTextChars(
            AiToolIntentRouter.AiToolIntent.Greeting,
            settings);
        Assert.Equal(20, min);
    }

    // ── Synthèse forcée en analyse d'écran (ScreenAnalysis.ForceFinalSynthesisEnabled) ──

    [Fact]
    public void ScreenAnalysis_FlagEnabled_TriggersSynthesis()
    {
        var synthesis = SendChatMessageHandler.ShouldForceFinalSynthesis(
            isScreenAnalysis: true,
            forceEnabled: true,
            meaningfulResponseDelivered: false,
            toolsWereExecuted: true,
            onlyAfterTools: true,
            screenAnalysisForceFinalSynthesisEnabled: true);

        Assert.True(synthesis);
    }

    /// <summary>Compat épinglée : l'appel 5-args historique exclut toujours l'analyse d'écran.</summary>
    [Fact]
    public void ScreenAnalysis_DefaultParam_KeepsLegacySkip()
    {
        var synthesis = SendChatMessageHandler.ShouldForceFinalSynthesis(
            isScreenAnalysis: true,
            forceEnabled: true,
            meaningfulResponseDelivered: false,
            toolsWereExecuted: true,
            onlyAfterTools: true);

        Assert.False(synthesis);
    }

    [Fact]
    public void ScreenAnalysis_FlagEnabled_RespectsMeaningfulDelivered()
    {
        var synthesis = SendChatMessageHandler.ShouldForceFinalSynthesis(
            isScreenAnalysis: true,
            forceEnabled: true,
            meaningfulResponseDelivered: true,
            toolsWereExecuted: true,
            onlyAfterTools: true,
            screenAnalysisForceFinalSynthesisEnabled: true);

        Assert.False(synthesis);
    }

    [Fact]
    public void ScreenAnalysis_FlagEnabled_RespectsOnlyAfterTools()
    {
        var synthesis = SendChatMessageHandler.ShouldForceFinalSynthesis(
            isScreenAnalysis: true,
            forceEnabled: true,
            meaningfulResponseDelivered: false,
            toolsWereExecuted: false,
            onlyAfterTools: true,
            screenAnalysisForceFinalSynthesisEnabled: true);

        Assert.False(synthesis);
    }

    [Fact]
    public void ScreenAnalysisOptions_ForceFinalSynthesis_EnabledByDefault()
    {
        Assert.True(new ScreenAnalysisOptions().ForceFinalSynthesisEnabled);
    }
}
