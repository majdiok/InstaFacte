using System.Text.Json.Nodes;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Application.Features.Studio.Common.SqlReport;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

public sealed class SqlReportPresetCatalogTests
{
    [Fact]
    public void Every_preset_targets_a_table_that_the_access_policy_knows()
    {
        Assert.NotEmpty(SqlReportPresetCatalog.All);
        foreach (var preset in SqlReportPresetCatalog.All)
        {
            var access = SqlReportAccessPolicy.Describe(preset.FactTable);
            Assert.True(access is not null, $"Préréglage « {preset.Key} » : table « {preset.FactTable} » non classée.");
            Assert.Equal(preset.Domain, access!.Domain);
        }
    }

    [Fact]
    public void Preset_keys_are_unique_and_documented()
    {
        var keys = SqlReportPresetCatalog.All.Select(p => p.Key).ToList();
        Assert.Equal(keys.Count, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(SqlReportPresetCatalog.All, p =>
        {
            Assert.False(string.IsNullOrWhiteSpace(p.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(p.Description));
        });
    }

    [Fact]
    public void Find_is_case_insensitive_and_returns_null_for_an_unknown_key()
    {
        Assert.NotNull(SqlReportPresetCatalog.Find("VENTES_PAR_PRODUIT"));
        Assert.Null(SqlReportPresetCatalog.Find("ventes_par_licorne"));
        Assert.Null(SqlReportPresetCatalog.Find(null));
    }

    [Fact]
    public void Materialize_keeps_the_business_filters_and_adds_the_period()
    {
        var preset = SqlReportPresetCatalog.Find("ventes_par_produit")!;
        var definition = SqlReportPresetCatalog.Materialize(
            preset, new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31));

        // Le filtre « factures réalisées » définit l'état : il ne doit jamais sauter.
        Assert.Contains(definition.Filters, f => f.Field == "Invoices_Status" && f.Op == "in");

        var period = Assert.Single(definition.Filters, f => f.Field == "Invoices_IssueDate");
        Assert.Equal("between", period.Op);
        Assert.Equal("2026-01-01", period.Value!.GetValue<string>());
        Assert.Equal("2026-03-31", period.Value2!.GetValue<string>());
    }

    [Fact]
    public void Materialize_without_a_period_adds_no_date_filter()
    {
        var preset = SqlReportPresetCatalog.Find("ventes_par_produit")!;
        var definition = SqlReportPresetCatalog.Materialize(preset);
        Assert.DoesNotContain(definition.Filters, f => f.Field == "Invoices_IssueDate");
    }

    [Fact]
    public void An_open_ended_period_uses_a_single_bound()
    {
        var preset = SqlReportPresetCatalog.Find("ventes_par_produit")!;
        var definition = SqlReportPresetCatalog.Materialize(preset, from: new DateOnly(2026, 6, 1));
        var period = Assert.Single(definition.Filters, f => f.Field == "Invoices_IssueDate");
        Assert.Equal("gte", period.Op);
    }

    [Fact]
    public void A_preset_whose_columns_are_missing_is_not_resolvable()
    {
        var preset = SqlReportPresetCatalog.Find("ventes_par_produit")!;

        var poorSchema = new SqlReportSchemaSnapshot(
            "InvoiceLines",
            new Dictionary<string, IReadOnlyList<SqlColumnInfo>>(StringComparer.OrdinalIgnoreCase)
            {
                ["InvoiceLines"] = new List<SqlColumnInfo> { new("Id", "uniqueidentifier", false, false) }
            },
            Array.Empty<SqlReportJoinEdge>());

        Assert.False(SqlReportPresetCatalog.ResolvesAgainst(preset, poorSchema));
    }

    [Fact]
    public void A_preset_resolves_against_a_matching_schema()
    {
        var preset = SqlReportPresetCatalog.Find("ventes_par_produit")!;
        Assert.True(SqlReportPresetCatalog.ResolvesAgainst(preset, SalesSnapshot()));
    }

    [Fact]
    public void The_sales_by_product_preset_produces_the_expected_sql()
    {
        var preset = SqlReportPresetCatalog.Find("ventes_par_produit")!;
        var definition = SqlReportPresetCatalog.Materialize(
            preset, new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31));

        Assert.True(SqlReportSqlBuilder.TryBuild(SalesSnapshot(), definition, 5000, out var query, out var error), error);
        Assert.Empty(query!.Warnings);

        Assert.Contains("GROUP BY t0.[ProductName]", query.Sql, StringComparison.Ordinal);
        Assert.Contains("SUM(t0.[Total])", query.Sql, StringComparison.Ordinal);
        // Statut ∈ {1,4} + période : 4 paramètres, aucune valeur inlinée.
        Assert.Equal(4, query.Parameters.Count);
        Assert.Contains("IN (@p0, @p1)", query.Sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Monthly_grouping_is_a_code_written_sql_expression()
    {
        var preset = SqlReportPresetCatalog.Find("ventes_par_mois")!;
        var definition = SqlReportPresetCatalog.Materialize(preset);

        Assert.True(SqlReportSqlBuilder.TryBuild(SalesSnapshot(), definition, 5000, out var query, out var error), error);

        Assert.Contains("CONVERT(char(7), t1.[IssueDate], 126)", query!.Sql, StringComparison.Ordinal);
        // La même expression doit apparaître dans le GROUP BY, sinon SQL Server refuse la requête.
        var select = query.Sql[..query.Sql.IndexOf(" GROUP BY ", StringComparison.Ordinal)];
        var group = query.Sql[query.Sql.IndexOf(" GROUP BY ", StringComparison.Ordinal)..];
        Assert.Contains("CONVERT(char(7)", select, StringComparison.Ordinal);
        Assert.Contains("CONVERT(char(7)", group, StringComparison.Ordinal);
        Assert.Contains("Invoices_IssueDate__month", query.Columns.Select(c => c.Key));
    }

    [Fact]
    public void A_granularity_suffix_on_a_non_date_column_does_not_resolve()
    {
        Assert.False(SqlReportSqlBuilder.CanResolve(SalesSnapshot(), "InvoiceLines_ProductName__month"));
        Assert.True(SqlReportSqlBuilder.CanResolve(SalesSnapshot(), "Invoices_IssueDate__quarter"));
    }

    private static SqlReportSchemaSnapshot SalesSnapshot()
    {
        var columns = new Dictionary<string, IReadOnlyList<SqlColumnInfo>>(StringComparer.OrdinalIgnoreCase)
        {
            ["InvoiceLines"] = new List<SqlColumnInfo>
            {
                new("Id", "uniqueidentifier", false, false),
                new("InvoiceId", "uniqueidentifier", false, false, null, new ForeignKeyHint("Invoices", "Id")),
                new("ProductName", "nvarchar", false, false),
                new("Quantity", "decimal", false, true),
                new("VatAmount", "decimal", false, true),
                new("DiscountAmount", "decimal", false, true),
                new("Total", "decimal", false, true)
            },
            ["Invoices"] = new List<SqlColumnInfo>
            {
                new("Id", "uniqueidentifier", false, false),
                new("ClientId", "uniqueidentifier", false, false, null, new ForeignKeyHint("Clients", "Id")),
                new("Status", "int", false, true),
                new("IssueDate", "datetime2", false, false)
            },
            ["Clients"] = new List<SqlColumnInfo>
            {
                new("Id", "uniqueidentifier", false, false),
                new("Name", "nvarchar", false, false)
            }
        };
        var edges = new List<SqlReportJoinEdge>
        {
            new("InvoiceLines", "InvoiceId", "Invoices", "Id"),
            new("Invoices", "ClientId", "Clients", "Id")
        };
        return new SqlReportSchemaSnapshot("InvoiceLines", columns, edges);
    }
}
