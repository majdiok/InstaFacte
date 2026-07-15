using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.AI;
using FactuTrust.Application.Features.AI.DTOs;
using Microsoft.Extensions.Options;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class AiVolatileContextFormatterTests
{
    private readonly AiVolatileContextFormatter _formatter =
        AiVolatileContextFormatter.CreateDefault();

    [Fact]
    public void Format_with_analysisSummary_includes_screen_analysis_mode()
    {
        var ctx = new ChatUiContextDto
        {
            Route = "/accounting/ledger",
            ScreenId = "accounting-ledger",
            AnalysisSummary = """{"schemaVersion":"2","screenId":"accounting-ledger","payload":{}}"""
        };

        var result = _formatter.Format(ctx);

        Assert.NotNull(result);
        Assert.Contains("MODE ANALYSE ÉCRAN", result);
        Assert.Contains("accounting-ledger", result);
        Assert.Contains("schemaVersion", result);
        Assert.Contains("Utiliser le snapshot ci-dessus comme base de l'analyse demandée.", result);
    }

    [Fact]
    public void Format_with_analysisSummary_does_not_require_tools_for_all_numbers()
    {
        var ctx = new ChatUiContextDto
        {
            ScreenId = "accounting-balance",
            AnalysisSummary = """{"rows":[]}"""
        };

        var result = _formatter.Format(ctx);

        Assert.NotNull(result);
        Assert.DoesNotContain("les données chiffrées viennent toujours des outils", result);
        Assert.Contains("source PRIMAIRE", result);
    }

    [Fact]
    public void Format_without_analysisSummary_keeps_tool_first_guidance()
    {
        var ctx = new ChatUiContextDto
        {
            Route = "/dashboard",
            ScreenId = "dashboard"
        };

        var result = _formatter.Format(ctx);

        Assert.NotNull(result);
        Assert.DoesNotContain("MODE ANALYSE ÉCRAN", result);
        Assert.Contains("les données chiffrées viennent toujours des outils", result);
    }

    [Fact]
    public void Format_includes_server_enrichment_when_provided()
    {
        var ctx = new ChatUiContextDto
        {
            ScreenId = "dashboard",
            AnalysisSummary = """{"schemaVersion":"2"}"""
        };

        var result = _formatter.Format(ctx, "ENRICHISSEMENT SERVEUR (get_sales_revenue) :\n{}");

        Assert.NotNull(result);
        Assert.Contains("ENRICHISSEMENT SERVEUR", result);
    }
}

public sealed class AiScreenAnalysisPromptBuilderTests
{
    [Fact]
    public void BuildScreenAnalysisSystemSection_includes_required_sections()
    {
        var options = new ScreenAnalysisOptions { EnhancedPromptsEnabled = true };
        var result = AiScreenAnalysisPromptBuilder.BuildScreenAnalysisSystemSection("accounting-income-statement", options);

        Assert.Contains("Synthèse exécutive", result);
        Assert.Contains("Anomalies et risques", result);
        Assert.Contains("generate_dashboard_config", result);
        // Durcissement anti-boucle : un seul appel dashboard, puis rédaction obligatoire des sections.
        Assert.Contains("UNE SEULE FOIS", result);
        Assert.Contains("OBLIGATOIREMENT", result);
    }

    [Fact]
    public void ForcedSynthesisSection_lists_markdown_sections_and_forbids_tools()
    {
        Assert.Contains("SYNTHÈSE FINALE", AiScreenAnalysisPromptBuilder.ForcedSynthesisSection);
        Assert.Contains("N'appelle aucun outil", AiScreenAnalysisPromptBuilder.ForcedSynthesisSection);
        Assert.Contains("## Synthèse exécutive", AiScreenAnalysisPromptBuilder.ForcedSynthesisSection);
    }

    [Fact]
    public void IsEnhancedForScreen_respects_per_screen_override()
    {
        var options = new ScreenAnalysisOptions
        {
            Enabled = true,
            EnhancedPromptsEnabled = false,
            PerScreenOverrides = new Dictionary<string, ScreenAnalysisScreenOverride>
            {
                ["accounting-ledger"] = new() { EnhancedPrompt = true }
            }
        };

        Assert.True(AiScreenAnalysisPromptBuilder.IsEnhancedForScreen("accounting-ledger", options));
        Assert.False(AiScreenAnalysisPromptBuilder.IsEnhancedForScreen("dashboard", options));
    }
}

public sealed class AiScreenAnalysisModeDetectorTests
{
    [Fact]
    public void IsScreenAnalysis_true_when_assistant_mode_set()
    {
        var options = new ChatRequestOptionsDto { AssistantMode = AssistantMode.ScreenAnalysis };
        Assert.True(AiScreenAnalysisModeDetector.IsScreenAnalysis(null, options));
    }

    [Fact]
    public void ResolveScreenId_from_json_when_ui_context_missing_screen_id()
    {
        var ctx = new ChatUiContextDto
        {
            AnalysisSummary = """{"schemaVersion":"2","screenId":"cash-desk","payload":{}}"""
        };

        Assert.Equal("cash-desk", AiScreenAnalysisModeDetector.ResolveScreenId(ctx));
    }
}
