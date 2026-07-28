using FactuTrust.Application.Features.AI;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Application.Features.AI.Tools;
using FactuTrust.Application.Features.Studio.Ai;
using FactuTrust.Application.Features.Studio.Common;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

/// <summary>
/// Fenêtres (vues lecture seule) proposées par l'assistant. L'enjeu est la sécurité : l'IA ne doit
/// jamais pouvoir viser une table interdite ni inventer une colonne — elle emprunte exactement le
/// même chemin gardé que le concepteur humain.
/// </summary>
public sealed class StudioAiViewSpecTests
{
    private static readonly IReadOnlyList<SqlColumnInfo> InvoiceColumns = new[]
    {
        new SqlColumnInfo("Id", "uniqueidentifier", false, false, "uuid"),
        new SqlColumnInfo("IssueDate", "datetime2", false, false, "date"),
        new SqlColumnInfo("ClientName", "nvarchar", true, false, "text"),
        new SqlColumnInfo("TotalAmount", "decimal", false, true, "money")
    };

    [Fact]
    public void Parses_a_window_over_an_existing_table()
    {
        const string json = """
        { "title": "Factures récentes", "table": "Invoices",
          "columns": [ "IssueDate", { "name": "TotalAmount", "label": "Total TTC", "format": "money" } ],
          "search": true }
        """;

        Assert.True(StudioAiViewSpec.TryParse(json, out var spec, out var error), error);
        Assert.Equal("Factures récentes", spec!.Title);
        Assert.Equal("Invoices", spec.Table);
        Assert.Equal(2, spec.Columns.Count);
        Assert.Equal("Total TTC", spec.Columns[1].Label);
        Assert.Equal("money", spec.Columns[1].Format);
        Assert.True(spec.Search);
    }

    [Theory]
    [InlineData("Invoices; DROP TABLE Users")]
    [InlineData("[Invoices]")]
    [InlineData("dbo.Invoices")]
    public void Table_name_that_is_not_a_plain_identifier_is_refused(string table)
    {
        var json = $$"""{ "title": "T", "table": "{{table}}" }""";

        Assert.False(StudioAiViewSpec.TryParse(json, out var spec, out var error));
        Assert.Null(spec);
        Assert.NotNull(error);
    }

    [Fact]
    public void Denied_table_is_refused_at_parse_time()
    {
        var denied = SqlSchemaGuard.DeniedTables.First();
        var json = $$"""{ "title": "T", "table": "{{denied}}" }""";

        Assert.False(StudioAiViewSpec.TryParse(json, out _, out var error));
        Assert.Contains("pas accessible", error);
    }

    [Fact]
    public void Unknown_columns_are_dropped_against_the_real_schema()
    {
        Assert.True(StudioAiViewSpec.TryParse("""
        { "title": "Factures", "table": "Invoices", "columns": [ "IssueDate", "ColonneInventee", "TotalAmount" ] }
        """, out var spec, out var error), error);

        var (definition, warnings) = StudioAiViewSpec.ResolveAgainstSchema(spec!, InvoiceColumns);

        Assert.Equal(new[] { "IssueDate", "TotalAmount" }, definition.Columns.Select(c => c.Name));
        Assert.Contains(warnings, w => w.Contains("ColonneInventee"));
    }

    [Fact]
    public void Format_falls_back_to_the_one_suggested_by_introspection()
    {
        Assert.True(StudioAiViewSpec.TryParse("""
        { "title": "Factures", "table": "Invoices", "columns": [ "IssueDate", { "name": "TotalAmount", "format": "wibble" } ] }
        """, out var spec, out var error), error);

        var (definition, _) = StudioAiViewSpec.ResolveAgainstSchema(spec!, InvoiceColumns);

        Assert.Equal("date", definition.Columns[0].Format);   // suggéré par le schéma
        Assert.Equal("money", definition.Columns[1].Format);  // format inconnu écarté, repli sur le schéma
    }

    [Fact]
    public void Spec_without_columns_shows_the_whole_table()
    {
        Assert.True(StudioAiViewSpec.TryParse("""
        { "title": "Factures", "table": "Invoices" }
        """, out var spec, out var error), error);

        var (definition, _) = StudioAiViewSpec.ResolveAgainstSchema(spec!, InvoiceColumns);

        Assert.Equal(InvoiceColumns.Count, definition.Columns.Count);
    }

    [Fact]
    public void Columns_are_capped_and_deduplicated()
    {
        var many = string.Join(",", Enumerable.Range(0, StudioAiViewSpec.MaxColumns + 5).Select(i => $"\"Col{i}\""));
        var json = $$"""{ "title": "T", "table": "Invoices", "columns": [ {{many}}, "Col0" ] }""";

        Assert.True(StudioAiViewSpec.TryParse(json, out var spec, out var error), error);
        Assert.Equal(StudioAiViewSpec.MaxColumns, spec!.Columns.Count);
        Assert.Equal(spec.Columns.Count, spec.Columns.Select(c => c.Name).Distinct().Count());
    }

    [Fact]
    public void View_tools_are_only_exposed_with_both_flags_on()
    {
        var previewOnly = Names(studioPlanPreview: true, viewTools: false);
        var withViews = Names(studioPlanPreview: true, viewTools: true);
        var viewsWithoutPreview = Names(studioPlanPreview: false, viewTools: true);

        Assert.DoesNotContain("studio_plan_view", previewOnly);
        Assert.Contains("studio_plan_view", withViews);
        Assert.Contains("studio_list_sql_tables", withViews);
        // Sans aperçu, une fenêtre serait créée sans validation : le couple de drapeaux est obligatoire.
        Assert.DoesNotContain("studio_plan_view", viewsWithoutPreview);
    }

    [Fact]
    public void View_tools_have_a_french_label()
    {
        Assert.True(AiToolFrenchLabels.Labels.ContainsKey("studio_plan_view"));
        Assert.True(AiToolFrenchLabels.Labels.ContainsKey("studio_list_sql_tables"));
    }

    private static IReadOnlyList<string> Names(bool studioPlanPreview, bool viewTools) =>
        AiToolRegistry.GetDefinitionsForMode(AssistantMode.StudioBuilder, enableMutationTools: true,
                agentScope: AssistantAgentScope.None, studioPlanPreview: studioPlanPreview,
                studioModifyTools: false, studioViewTools: viewTools)
            .Select(t => t.Name).ToList();
}
