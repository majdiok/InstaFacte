using System.Text.Json.Nodes;
using FactuTrust.Infrastructure.Services.AI;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

/// <summary>
/// Lot 3 — cohérence widget ↔ texte : une section « table » sans lignes doit être supprimée pour ne pas
/// afficher une carte « Aucune donnée » contredisant une réponse texte renseignée. KPI/charts non touchés.
/// </summary>
public sealed class DashboardConfigSanitizerTests
{
    private static JsonArray Sections(JsonObject result) => (JsonArray)result["sections"]!;

    [Fact]
    public void Drops_Table_With_Empty_Rows_But_Keeps_KpiCard()
    {
        var sections = JsonNode.Parse(@"[
            {""type"":""kpi_card"",""title"":""CA"",""data"":{""value"":1000}},
            {""type"":""table"",""title"":""Top clients"",""data"":{""columns"":[""Client"",""CA""],""rows"":[]}}
        ]")!;

        var result = DashboardConfigSanitizer.SanitizeSections("Tableau", sections, null);

        var arr = Sections(result);
        Assert.Single(arr);
        Assert.Equal("kpi_card", (string?)arr[0]!["type"]);
    }

    [Fact]
    public void Keeps_Table_With_Rows()
    {
        var sections = JsonNode.Parse(@"[
            {""type"":""table"",""title"":""Top clients"",""data"":{""columns"":[""Client"",""CA""],""rows"":[{""Client"":""Client passager"",""CA"":27387.25}]}}
        ]")!;

        var result = DashboardConfigSanitizer.SanitizeSections("Tableau", sections, null);

        var arr = Sections(result);
        Assert.Single(arr);
        Assert.Equal("table", (string?)arr[0]!["type"]);
    }

    [Fact]
    public void All_Empty_Returns_Empty_Sections_And_Keeps_Title()
    {
        var sections = JsonNode.Parse(@"[
            {""type"":""table"",""title"":""Vide"",""data"":{""columns"":[""A""],""rows"":[]}}
        ]")!;

        var result = DashboardConfigSanitizer.SanitizeSections("Tableau", sections, null);

        Assert.Empty(Sections(result));
        Assert.Equal("Tableau", (string?)result["title"]);
    }

    [Fact]
    public void Does_Not_Touch_Kpi_Or_Chart()
    {
        var sections = JsonNode.Parse(@"[
            {""type"":""kpi_card"",""title"":""CA"",""data"":{""value"":1000}},
            {""type"":""chart"",""title"":""Tendance"",""data"":{""series"":[]}}
        ]")!;

        var result = DashboardConfigSanitizer.SanitizeSections("Tableau", sections, null);

        Assert.Equal(2, Sections(result).Count);
    }

    [Fact]
    public void Drops_Malformed_Table_Without_Throwing()
    {
        // Table sans data/rows : non rendable → supprimée, sans exception.
        var sections = JsonNode.Parse(@"[ {""type"":""table"",""title"":""Cassée""} ]")!;

        var result = DashboardConfigSanitizer.SanitizeSections("Tableau", sections, null);

        Assert.Empty(Sections(result));
    }
}
