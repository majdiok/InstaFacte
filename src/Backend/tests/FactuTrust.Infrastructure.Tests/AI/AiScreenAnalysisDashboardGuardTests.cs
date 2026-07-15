using FactuTrust.Application.Features.AI.Commands;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

/// <summary>
/// Anti-boucle analyse d'écran : une fois un tableau de bord accumulé, les appels
/// generate_dashboard_config suivants sont court-circuités (message déterministe qui pousse le
/// modèle à rédiger l'analyse, aucune ré-exécution, aucun second événement SSE). Gated ScreenAnalysis.
/// </summary>
public sealed class AiScreenAnalysisDashboardGuardTests
{
    private const string SomeDashboardJson = """{"title":"Analyse","sections":[]}""";

    [Fact]
    public void ShortCircuits_InScreenAnalysis_WhenDashboardAlreadyAccumulated()
    {
        Assert.True(SendChatMessageHandler.ShouldShortCircuitScreenAnalysisDashboardCall(
            isScreenAnalysis: true,
            toolName: "generate_dashboard_config",
            accumulatedDashboardJson: SomeDashboardJson));
    }

    [Fact]
    public void DoesNotShortCircuit_InDefaultMode()
    {
        Assert.False(SendChatMessageHandler.ShouldShortCircuitScreenAnalysisDashboardCall(
            isScreenAnalysis: false,
            toolName: "generate_dashboard_config",
            accumulatedDashboardJson: SomeDashboardJson));
    }

    [Fact]
    public void DoesNotShortCircuit_ForOtherTools()
    {
        Assert.False(SendChatMessageHandler.ShouldShortCircuitScreenAnalysisDashboardCall(
            isScreenAnalysis: true,
            toolName: "get_sales_revenue",
            accumulatedDashboardJson: SomeDashboardJson));
    }

    [Fact]
    public void DoesNotShortCircuit_WhenNoDashboardYet()
    {
        Assert.False(SendChatMessageHandler.ShouldShortCircuitScreenAnalysisDashboardCall(
            isScreenAnalysis: true,
            toolName: "generate_dashboard_config",
            accumulatedDashboardJson: null));
        Assert.False(SendChatMessageHandler.ShouldShortCircuitScreenAnalysisDashboardCall(
            isScreenAnalysis: true,
            toolName: "generate_dashboard_config",
            accumulatedDashboardJson: ""));
    }

    /// <summary>Le message-nudge ne cite aucun identifiant snake_case (garde anti-fuite jamais sollicitée).</summary>
    [Fact]
    public void Nudge_Message_Contains_No_Snake_Case()
    {
        Assert.False(SendChatMessageHandler.ScreenAnalysisDashboardAlreadyGeneratedMessage.Contains('_'));
        Assert.Contains("sections markdown", SendChatMessageHandler.ScreenAnalysisDashboardAlreadyGeneratedMessage);
    }
}
