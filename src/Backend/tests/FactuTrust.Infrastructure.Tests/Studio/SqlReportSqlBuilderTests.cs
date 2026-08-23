using System.Text.Json.Nodes;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Application.Features.Studio.Common.SqlReport;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

/// <summary>
/// Le constructeur SQL est le point où une sortie de modèle pourrait devenir du SQL. Ces tests
/// vérifient qu'il ne peut PAS : identifiants confrontés au schéma réel, valeurs paramétrées,
/// jointures issues des clés étrangères seulement.
/// </summary>
public sealed class SqlReportSqlBuilderTests
{
    // Étoile réaliste : InvoiceLines (faits) → Invoices → Clients, et InvoiceLines → Products.
    private static SqlReportSchemaSnapshot Snapshot()
    {
        var invoiceLines = new List<SqlColumnInfo>
        {
            new("Id", "uniqueidentifier", false, false),
            new("InvoiceId", "uniqueidentifier", false, false, null, new ForeignKeyHint("Invoices", "Id")),
            new("ProductId", "uniqueidentifier", true, false, null, new ForeignKeyHint("Products", "Id")),
            new("Description", "nvarchar", true, false),
            new("Quantity", "decimal", false, true),
            new("UnitPrice_Amount", "decimal", false, true),
            new("TotalHt_Amount", "decimal", false, true),
            new("RowVersion", "timestamp", false, false)
        };
        var invoices = new List<SqlColumnInfo>
        {
            new("Id", "uniqueidentifier", false, false),
            new("ClientId", "uniqueidentifier", false, false, null, new ForeignKeyHint("Clients", "Id")),
            new("Number", "nvarchar", false, false),
            new("Status", "nvarchar", false, false),
            new("IssueDate", "datetime2", false, false)
        };
        var clients = new List<SqlColumnInfo>
        {
            new("Id", "uniqueidentifier", false, false),
            new("Name", "nvarchar", false, false),
            new("City", "nvarchar", true, false)
        };
        var products = new List<SqlColumnInfo>
        {
            new("Id", "uniqueidentifier", false, false),
            new("Code", "nvarchar", false, false),
            new("Name", "nvarchar", false, false)
        };

        var columns = new Dictionary<string, IReadOnlyList<SqlColumnInfo>>(StringComparer.OrdinalIgnoreCase)
        {
            ["InvoiceLines"] = invoiceLines,
            ["Invoices"] = invoices,
            ["Clients"] = clients,
            ["Products"] = products
        };
        var edges = new List<SqlReportJoinEdge>
        {
            new("InvoiceLines", "InvoiceId", "Invoices", "Id"),
            new("InvoiceLines", "ProductId", "Products", "Id"),
            new("Invoices", "ClientId", "Clients", "Id")
        };
        return new SqlReportSchemaSnapshot("InvoiceLines", columns, edges);
    }

    private static SqlReportQuery Build(ReportDefinition definition, int maxRows = 5000)
    {
        Assert.True(SqlReportSqlBuilder.TryBuild(Snapshot(), definition, maxRows, out var query, out var error), error);
        return query!;
    }

    // ---- Cas nominal ----

    [Fact]
    public void Sales_by_product_groups_joins_and_aggregates_in_sql()
    {
        var query = Build(new ReportDefinition
        {
            Grouping = new[] { "Products_Name" },
            Aggregations = new[]
            {
                new ReportAggregation { Field = "InvoiceLines_Quantity", Fn = "sum" },
                new ReportAggregation { Field = "InvoiceLines_TotalHt_Amount", Fn = "sum" }
            },
            Sort = new[] { new ReportSort { Field = "sum_InvoiceLines_TotalHt_Amount", Dir = "desc" } }
        });

        Assert.Contains("FROM [dbo].[InvoiceLines] AS t0", query.Sql, StringComparison.Ordinal);
        Assert.Contains("LEFT JOIN [dbo].[Products]", query.Sql, StringComparison.Ordinal);
        Assert.Contains("SUM(", query.Sql, StringComparison.Ordinal);
        Assert.Contains("GROUP BY", query.Sql, StringComparison.Ordinal);
        Assert.Contains("ORDER BY [sum_InvoiceLines_TotalHt_Amount] DESC", query.Sql, StringComparison.Ordinal);
        Assert.Contains("OFFSET 0 ROWS FETCH NEXT 5000 ROWS ONLY", query.Sql, StringComparison.Ordinal);
        Assert.Empty(query.Warnings);

        Assert.Equal(3, query.Columns.Count);
        Assert.Equal("dimension", query.Columns[0].Kind);
        Assert.All(query.Columns.Skip(1), c => Assert.Equal("measure", c.Kind));

        // Le comptage d'un état groupé porte sur le nombre de GROUPES, pas de lignes.
        Assert.StartsWith("SELECT COUNT(*) FROM (SELECT", query.CountSql, StringComparison.Ordinal);
    }

    [Fact]
    public void A_two_hop_dimension_joins_the_intermediate_table()
    {
        var query = Build(new ReportDefinition
        {
            Grouping = new[] { "Clients_Name" },
            Aggregations = new[] { new ReportAggregation { Field = "InvoiceLines_TotalHt_Amount", Fn = "sum" } }
        });

        // Le client n'est accessible qu'en traversant la facture : les deux jointures sont émises.
        Assert.Contains("LEFT JOIN [dbo].[Invoices]", query.Sql, StringComparison.Ordinal);
        Assert.Contains("LEFT JOIN [dbo].[Clients]", query.Sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Detail_without_requested_columns_projects_the_fact_table()
    {
        var query = Build(new ReportDefinition());

        Assert.DoesNotContain("GROUP BY", query.Sql, StringComparison.Ordinal);
        Assert.All(query.Columns, c => Assert.Equal("dimension", c.Kind));
        Assert.DoesNotContain(query.Columns, c => c.Key.Contains("RowVersion", StringComparison.Ordinal));
        Assert.Equal($"SELECT COUNT(*) FROM [dbo].[InvoiceLines] AS t0", query.CountSql);
    }

    // ---- Sûreté ----

    [Theory]
    [InlineData("Quantity]; DROP TABLE Invoices--")]
    [InlineData("1 OR 1=1")]
    [InlineData("Products_Name; DELETE FROM Clients")]
    [InlineData("UnknownColumn")]
    public void An_identifier_that_is_not_in_the_schema_is_dropped_not_injected(string field)
    {
        Assert.True(SqlReportSqlBuilder.TryBuild(
            Snapshot(),
            new ReportDefinition { Fields = new[] { field, "InvoiceLines_Quantity" } },
            100,
            out var query,
            out var error), error);

        Assert.DoesNotContain("DROP", query!.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE", query.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("1=1", query.Sql, StringComparison.Ordinal);
        Assert.Contains(query.Warnings, w => w.Contains("ignoré", StringComparison.OrdinalIgnoreCase));
        Assert.Single(query.Columns);
    }

    [Fact]
    public void A_filter_value_is_always_a_parameter_never_inlined()
    {
        var query = Build(new ReportDefinition
        {
            Fields = new[] { "InvoiceLines_Quantity" },
            Filters = new[]
            {
                new ReportFilter
                {
                    Field = "Invoices_Number",
                    Op = "eq",
                    Value = JsonValue.Create("F-001'; DROP TABLE Clients--")
                }
            }
        });

        Assert.DoesNotContain("DROP TABLE", query.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@p0", query.Sql, StringComparison.Ordinal);
        Assert.Equal("F-001'; DROP TABLE Clients--", Assert.Single(query.Parameters).Value);
    }

    [Fact]
    public void A_denied_column_is_neither_exposed_nor_resolvable()
    {
        var meta = SqlReportSqlBuilder.BuildFieldMeta(Snapshot());
        Assert.DoesNotContain(meta, m => m.Key.Contains("RowVersion", StringComparison.Ordinal));

        Assert.True(SqlReportSqlBuilder.TryBuild(
            Snapshot(),
            new ReportDefinition { Fields = new[] { "InvoiceLines_RowVersion", "InvoiceLines_Quantity" } },
            100, out var query, out _));
        Assert.DoesNotContain("RowVersion", query!.Sql, StringComparison.Ordinal);
    }

    [Fact]
    public void A_table_without_a_foreign_key_path_is_refused_rather_than_cross_joined()
    {
        var snapshot = Snapshot();
        var orphan = new Dictionary<string, IReadOnlyList<SqlColumnInfo>>(snapshot.ColumnsByTable, StringComparer.OrdinalIgnoreCase)
        {
            ["Employees"] = new List<SqlColumnInfo> { new("Id", "uniqueidentifier", false, false), new("LastName", "nvarchar", false, false) }
        };
        var isolated = new SqlReportSchemaSnapshot(snapshot.FactTable, orphan, snapshot.Edges);

        Assert.False(SqlReportSqlBuilder.TryBuild(
            isolated,
            new ReportDefinition { Fields = new[] { "Employees_LastName" } },
            100, out var query, out var error));
        Assert.Null(query);
        Assert.Contains("clé étrangère", error, StringComparison.OrdinalIgnoreCase);
    }

    // ---- Mesures ----

    [Fact]
    public void An_unknown_aggregate_is_dropped_and_count_takes_over()
    {
        var query = Build(new ReportDefinition
        {
            Grouping = new[] { "Products_Name" },
            Aggregations = new[] { new ReportAggregation { Field = "InvoiceLines_Quantity", Fn = "median" } }
        });

        Assert.Contains(query.Warnings, w => w.Contains("median", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("MEDIAN", query.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("COUNT(*)", query.Sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Summing_a_non_numeric_column_is_refused()
    {
        var query = Build(new ReportDefinition
        {
            Grouping = new[] { "Products_Name" },
            Aggregations = new[] { new ReportAggregation { Field = "Invoices_Number", Fn = "sum" } }
        });

        Assert.Contains(query.Warnings, w => w.Contains("numérique", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("SUM(", query.Sql, StringComparison.Ordinal);
    }

    // ---- Filtres ----

    [Fact]
    public void Between_on_a_date_column_produces_two_typed_parameters()
    {
        var query = Build(new ReportDefinition
        {
            Grouping = new[] { "Products_Name" },
            Aggregations = new[] { new ReportAggregation { Fn = "count" } },
            Filters = new[]
            {
                new ReportFilter
                {
                    Field = "Invoices_IssueDate",
                    Op = "between",
                    Value = JsonValue.Create("2026-01-01"),
                    Value2 = JsonValue.Create("2026-03-31")
                }
            }
        });

        Assert.Equal(2, query.Parameters.Count);
        Assert.All(query.Parameters.Values, v => Assert.IsType<DateTime>(v));
        Assert.Contains(">= @p0", query.Sql, StringComparison.Ordinal);
        Assert.Contains("<= @p1", query.Sql, StringComparison.Ordinal);
    }

    [Fact]
    public void In_filter_expands_to_one_parameter_per_value()
    {
        var query = Build(new ReportDefinition
        {
            Fields = new[] { "InvoiceLines_Quantity" },
            Filters = new[]
            {
                new ReportFilter
                {
                    Field = "Invoices_Status",
                    Op = "in",
                    Value = new JsonArray(JsonValue.Create("Paid"), JsonValue.Create("Validated"))
                }
            }
        });

        Assert.Equal(2, query.Parameters.Count);
        Assert.Contains("IN (@p0, @p1)", query.Sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Contains_filter_uses_a_parameterized_like()
    {
        var query = Build(new ReportDefinition
        {
            Fields = new[] { "InvoiceLines_Quantity" },
            Filters = new[] { new ReportFilter { Field = "Clients_Name", Op = "contains", Value = JsonValue.Create("sarl") } }
        });

        Assert.Contains("LIKE @p0", query.Sql, StringComparison.Ordinal);
        Assert.Equal("%sarl%", Assert.Single(query.Parameters).Value);
    }

    [Fact]
    public void A_numeric_filter_value_is_coerced_to_a_number()
    {
        var query = Build(new ReportDefinition
        {
            Fields = new[] { "InvoiceLines_Quantity" },
            Filters = new[] { new ReportFilter { Field = "InvoiceLines_Quantity", Op = "gte", Value = JsonValue.Create(10) } }
        });

        Assert.Equal(10m, Assert.Single(query.Parameters).Value);
    }

    // ---- Tri ----

    [Fact]
    public void A_sort_on_a_column_absent_from_the_result_is_dropped_and_a_default_applies()
    {
        var query = Build(new ReportDefinition
        {
            Fields = new[] { "InvoiceLines_Quantity" },
            Sort = new[] { new ReportSort { Field = "Clients_City", Dir = "asc" } }
        });

        Assert.Contains(query.Warnings, w => w.Contains("Tri", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("[Clients_City]", query.Sql, StringComparison.Ordinal);
        // OFFSET/FETCH impose un ORDER BY : un tri de repli est toujours émis.
        Assert.Contains("ORDER BY", query.Sql, StringComparison.Ordinal);
    }

    // ---- Champs exposés ----

    [Fact]
    public void Field_meta_prefixes_joined_tables_and_flags_numeric_columns()
    {
        var meta = SqlReportSqlBuilder.BuildFieldMeta(Snapshot());

        var quantity = meta.Single(m => m.Key == "InvoiceLines_Quantity");
        Assert.True(quantity.Numeric);
        Assert.Equal("Quantity", quantity.Label);

        var clientName = meta.Single(m => m.Key == "Clients_Name");
        Assert.False(clientName.Numeric);
        Assert.Contains("·", clientName.Label, StringComparison.Ordinal);
    }

    [Fact]
    public void A_column_name_containing_an_underscore_still_resolves_on_the_fact_table()
    {
        var query = Build(new ReportDefinition { Fields = new[] { "InvoiceLines_UnitPrice_Amount" } });
        Assert.Equal("InvoiceLines_UnitPrice_Amount", Assert.Single(query.Columns).Key);
        Assert.Contains("t0.[UnitPrice_Amount]", query.Sql, StringComparison.Ordinal);
    }
}
