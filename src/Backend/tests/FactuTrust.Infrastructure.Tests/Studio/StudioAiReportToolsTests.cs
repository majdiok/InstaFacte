using System.Text.Json;
using FactuTrust.Application.Features.AI;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Application.Features.AI.Tools;
using FactuTrust.Application.Features.Studio.Ai;
using FactuTrust.Application.Features.Studio.Common;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

/// <summary>
/// Garde anti-régression des outils d'états : drapeau OFF ⇒ le catalogue StudioBuilder est
/// strictement celui d'avant ; drapeau ON ⇒ les outils s'ajoutent sans rien retirer.
/// </summary>
public sealed class StudioAiReportToolsTests
{
    private static IReadOnlyList<string> Catalogue(bool planPreview = false, bool reportTools = false) =>
        AiToolRegistry.GetDefinitionsForMode(
                AssistantMode.StudioBuilder, enableMutationTools: true,
                agentScope: AssistantAgentScope.None,
                studioPlanPreview: planPreview, studioModifyTools: false, studioViewTools: false,
                studioReportTools: reportTools)
            .Select(t => t.Name)
            .ToList();

    [Fact]
    public void Flag_off_leaves_the_catalogue_untouched()
    {
        var withoutReports = Catalogue();
        Assert.DoesNotContain("studio_run_report", withoutReports);
        Assert.DoesNotContain("studio_plan_report", withoutReports);
        Assert.DoesNotContain("studio_list_report_sources", withoutReports);
        Assert.DoesNotContain("studio_describe_report_source", withoutReports);
    }

    [Fact]
    public void Report_tools_are_purely_additive()
    {
        var before = Catalogue().ToHashSet(StringComparer.Ordinal);
        var after = Catalogue(reportTools: true).ToHashSet(StringComparer.Ordinal);

        Assert.Empty(before.Except(after)); // rien n'a disparu
        Assert.Contains("studio_run_report", after);
        Assert.Contains("studio_list_report_sources", after);
        Assert.Contains("studio_describe_report_source", after);
    }

    [Fact]
    public void Saving_a_report_requires_the_preview_flow()
    {
        // Sans aperçu, aucun chemin de confirmation n'existe : on n'expose pas le plan.
        Assert.DoesNotContain("studio_plan_report", Catalogue(reportTools: true));
        Assert.Contains("studio_plan_report", Catalogue(planPreview: true, reportTools: true));
    }

    [Fact]
    public void Running_a_report_is_read_only_and_survives_mutation_lockdown()
    {
        var readOnly = AiToolRegistry.GetDefinitionsForMode(
                AssistantMode.StudioBuilder, enableMutationTools: false,
                agentScope: AssistantAgentScope.None,
                studioPlanPreview: true, studioModifyTools: false, studioViewTools: false,
                studioReportTools: true)
            .Select(t => t.Name)
            .ToList();

        Assert.Contains("studio_run_report", readOnly);
        Assert.Contains("studio_describe_report_source", readOnly);
        Assert.DoesNotContain("studio_plan_report", readOnly); // mutant : retiré
    }

    [Fact]
    public void The_four_dead_tool_names_are_gone()
    {
        // Ils figuraient dans les catalogues sans définition : ils filtraient dans le vide, ce qui
        // privait le modèle de tout outil d'état.
        foreach (var dead in new[] { "studio_build_report", "studio_extract_record", "studio_list_custom_tables", "studio_query_records" })
            Assert.Null(AiToolRegistry.GetToolDefinition(dead));
    }

    [Fact]
    public void Every_report_tool_has_a_french_label()
    {
        foreach (var name in new[] { "studio_list_report_sources", "studio_describe_report_source", "studio_run_report", "studio_plan_report" })
            Assert.True(AiToolFrenchLabels.Labels.ContainsKey(name), name);
    }

    // ---- Spécification ----

    [Fact]
    public void A_preset_spec_materializes_into_the_preset_definition()
    {
        const string json = """
        { "title": "Ventes T1", "preset": "ventes_par_produit", "from": "2026-01-01", "to": "2026-03-31" }
        """;

        Assert.True(StudioAiReportSpec.TryParse(json, out var spec, out var error), error);
        var (factTable, definition) = StudioAiReportSpec.Materialize(spec!);

        Assert.Equal("InvoiceLines", factTable);
        Assert.Contains(definition.Grouping, g => g == "InvoiceLines_ProductName");
        Assert.Contains(definition.Filters, f => f.Field == "Invoices_IssueDate" && f.Op == "between");
    }

    [Fact]
    public void An_unknown_preset_degrades_to_the_free_form_source()
    {
        const string json = """
        { "title": "X", "preset": "ventes_par_licorne", "source": "InvoiceLines",
          "groupBy": ["InvoiceLines_ProductName"], "measures": [{"field":"InvoiceLines_Total","fn":"sum"}] }
        """;

        Assert.True(StudioAiReportSpec.TryParse(json, out var spec, out _));
        Assert.Null(spec!.PresetKey);
        Assert.Contains(spec.Warnings, w => w.Contains("ventes_par_licorne", StringComparison.Ordinal));

        var (factTable, definition) = StudioAiReportSpec.Materialize(spec);
        Assert.Equal("InvoiceLines", factTable);
        Assert.Single(definition.Aggregations);
    }

    [Fact]
    public void Neither_preset_nor_source_is_refused()
    {
        Assert.False(StudioAiReportSpec.TryParse("""{ "title": "X" }""", out _, out var error));
        Assert.Contains("source", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void An_invalid_source_identifier_never_reaches_the_engine()
    {
        Assert.False(StudioAiReportSpec.TryParse(
            """{ "source": "InvoiceLines; DROP TABLE Clients" }""", out _, out var error));
        Assert.Contains("invalide", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void French_operator_aliases_are_understood()
    {
        const string json = """
        { "source": "InvoiceLines", "columns": ["InvoiceLines_Total"],
          "filters": [ { "champ": "InvoiceLines_Total", "operateur": "supérieur", "valeur": 100 } ] }
        """;

        Assert.True(StudioAiReportSpec.TryParse(json, out var spec, out _));
        Assert.Equal("gt", Assert.Single(spec!.Filters).Op);
    }

    [Fact]
    public void An_unknown_operator_drops_the_filter_with_a_warning()
    {
        const string json = """
        { "source": "InvoiceLines",
          "filters": [ { "field": "InvoiceLines_Total", "op": "ressemble", "value": 1 } ] }
        """;

        Assert.True(StudioAiReportSpec.TryParse(json, out var spec, out _));
        Assert.Empty(spec!.Filters);
        Assert.Contains(spec.Warnings, w => w.Contains("ressemble", StringComparison.Ordinal));
    }

    [Fact]
    public void Reversed_period_bounds_are_reordered_rather_than_yielding_nothing()
    {
        const string json = """
        { "source": "InvoiceLines", "from": "2026-03-31", "to": "2026-01-01" }
        """;

        Assert.True(StudioAiReportSpec.TryParse(json, out var spec, out _));
        Assert.Equal(new DateOnly(2026, 1, 1), spec!.From);
        Assert.Equal(new DateOnly(2026, 3, 31), spec.To);
    }

    // ---- Aperçu ----

    [Fact]
    public void The_report_plan_summary_carries_a_real_data_sample()
    {
        var definition = new ReportDefinition
        {
            Grouping = new[] { "InvoiceLines_ProductName" },
            Aggregations = new[] { new ReportAggregation { Field = "InvoiceLines_Total", Fn = "sum" } }
        };
        var sample = new ReportResultDto(
            new[]
            {
                new ReportColumn("InvoiceLines_ProductName", "Product Name", "dimension"),
                new ReportColumn("sum_InvoiceLines_Total", "Somme de Total", "measure")
            },
            new[]
            {
                (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>
                {
                    ["InvoiceLines_ProductName"] = "Ciment",
                    ["sum_InvoiceLines_Total"] = 30000m
                }
            },
            42);

        using var doc = JsonDocument.Parse(StudioAiPlanSummary.ForReport(
            "Ventes par produit", "Lignes de facture de vente", definition, sample, Array.Empty<string>()));
        var root = doc.RootElement;

        Assert.Equal("Report", root.GetProperty("kind").GetString());
        Assert.Equal("Ventes par produit", root.GetProperty("title").GetString());

        var steps = root.GetProperty("steps").EnumerateArray()
            .ToDictionary(s => s.GetProperty("key").GetString()!, s => s.GetProperty("detail").GetString()!);
        Assert.Equal("Lignes de facture de vente", steps["report_source"]);
        Assert.Contains("Product Name", steps["report_group"]);
        Assert.Contains("Somme de Total", steps["report_measures"]);
        Assert.Contains("42", steps["report_sample"]);

        // L'échantillon est joint : l'utilisateur valide sur des chiffres, pas sur une promesse.
        Assert.Equal(1, root.GetProperty("sample").GetProperty("rows").GetArrayLength());
    }

    // ---- Messages de reformulation ----

    [Fact]
    public void A_failed_report_intent_no_longer_talks_about_creating_a_system()
    {
        Assert.Equal(
            StudioTextToolCallRecovery.ReformulateReportMessage,
            StudioTextToolCallRecovery.ResolveReformulateMessage("studio_plan_report"));

        Assert.Equal(
            StudioTextToolCallRecovery.ReformulateReportMessage,
            StudioTextToolCallRecovery.ResolveReformulateMessage(
                null, """{"preset":"ventes_par_produit","measures":[]"""));

        Assert.Equal(
            StudioTextToolCallRecovery.ReformulateMessage,
            StudioTextToolCallRecovery.ResolveReformulateMessage("studio_plan_system"));
    }

    [Fact]
    public void Plain_prose_quoting_a_code_fence_is_no_longer_taken_for_a_leaked_spec()
    {
        // Cause directe du message d'erreur affiché à tort : un ```json seul suffisait.
        Assert.False(StudioTextToolCallRecovery.LooksLikeStudioSpecText(
            "Voici un exemple de configuration ```json { \"exemple\": 1 } ```"));

        // Une vraie spécification tronquée reste détectée.
        Assert.True(StudioTextToolCallRecovery.LooksLikeStudioSpecText(
            "{\"entity\":{\"displayName\":\"X\"},\"fields\":[{\"label\":\"A\""));
    }

    [Fact]
    public void A_leaked_report_spec_is_recognised()
    {
        Assert.True(StudioTextToolCallRecovery.LooksLikeStudioSpecText(
            "{\"source\":\"InvoiceLines\",\"groupBy\":[\"InvoiceLines_ProductName\"]"));
    }
}
