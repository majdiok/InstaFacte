using System.Text.Json.Nodes;
using FactuTrust.Application.Features.Studio.Common;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

public sealed class CustomReportRunnerTests
{
    private static IReadOnlyList<ReportFieldMeta> Schema() => new[]
    {
        new ReportFieldMeta("region", "Région", false),
        new ReportFieldMeta("amount", "Montant", true),
        new ReportFieldMeta("qty", "Quantité", true)
    };

    private static IReadOnlyDictionary<string, object?> Row(string region, decimal amount, int qty) =>
        new Dictionary<string, object?> { ["region"] = region, ["amount"] = amount, ["qty"] = qty };

    private static List<IReadOnlyDictionary<string, object?>> Data() => new()
    {
        Row("Nord", 100m, 2),
        Row("Sud", 50m, 1),
        Row("Nord", 200m, 3),
        Row("Sud", 25m, 5)
    };

    [Fact]
    public void Detail_report_projects_selected_fields()
    {
        var def = new ReportDefinition { Fields = new[] { "region", "amount" } };
        var result = CustomReportRunner.Run(Schema(), Data(), def);
        Assert.Equal(2, result.Columns.Count);
        Assert.Equal(4, result.TotalRows);
        Assert.All(result.Rows, r => Assert.True(r.ContainsKey("region") && r.ContainsKey("amount")));
    }

    [Fact]
    public void Grouped_sum_aggregates_per_group()
    {
        var def = new ReportDefinition
        {
            Grouping = new[] { "region" },
            Aggregations = new[] { new ReportAggregation { Field = "amount", Fn = "sum" } },
            Sort = new[] { new ReportSort { Field = "region", Dir = "asc" } }
        };
        var result = CustomReportRunner.Run(Schema(), Data(), def);
        Assert.Equal(2, result.TotalRows);
        var nord = result.Rows.First(r => (r["region"] as string) == "Nord");
        Assert.Equal(300m, Convert.ToDecimal(nord["sum_amount"]));
        var sud = result.Rows.First(r => (r["region"] as string) == "Sud");
        Assert.Equal(75m, Convert.ToDecimal(sud["sum_amount"]));
    }

    [Fact]
    public void Count_aggregation_counts_group_members()
    {
        var def = new ReportDefinition
        {
            Grouping = new[] { "region" },
            Aggregations = new[] { new ReportAggregation { Fn = "count" } }
        };
        var result = CustomReportRunner.Run(Schema(), Data(), def);
        Assert.All(result.Rows, r => Assert.Equal(2, Convert.ToInt32(r["count"])));
    }

    [Fact]
    public void Filter_numeric_gte_keeps_matching_rows()
    {
        var def = new ReportDefinition
        {
            Fields = new[] { "amount" },
            Filters = new[] { new ReportFilter { Field = "amount", Op = "gte", Value = JsonValue.Create(100) } }
        };
        var result = CustomReportRunner.Run(Schema(), Data(), def);
        Assert.Equal(2, result.TotalRows);
    }

    [Fact]
    public void Filter_contains_is_case_insensitive()
    {
        var def = new ReportDefinition
        {
            Fields = new[] { "region" },
            Filters = new[] { new ReportFilter { Field = "region", Op = "contains", Value = JsonValue.Create("nor") } }
        };
        var result = CustomReportRunner.Run(Schema(), Data(), def);
        Assert.Equal(2, result.TotalRows);
    }

    [Fact]
    public void Avg_and_minmax_aggregations()
    {
        var def = new ReportDefinition
        {
            Grouping = new[] { "region" },
            Aggregations = new[]
            {
                new ReportAggregation { Field = "amount", Fn = "avg" },
                new ReportAggregation { Field = "amount", Fn = "min" },
                new ReportAggregation { Field = "amount", Fn = "max" }
            }
        };
        var result = CustomReportRunner.Run(Schema(), Data(), def);
        var nord = result.Rows.First(r => (r["region"] as string) == "Nord");
        Assert.Equal(150m, Convert.ToDecimal(nord["avg_amount"]));
        Assert.Equal(100m, Convert.ToDecimal(nord["min_amount"]));
        Assert.Equal(200m, Convert.ToDecimal(nord["max_amount"]));
    }

    [Fact]
    public void Detail_sort_desc_orders_rows()
    {
        var def = new ReportDefinition
        {
            Fields = new[] { "amount" },
            Sort = new[] { new ReportSort { Field = "amount", Dir = "desc" } }
        };
        var result = CustomReportRunner.Run(Schema(), Data(), def);
        Assert.Equal(200m, Convert.ToDecimal(result.Rows[0]["amount"]));
        Assert.Equal(25m, Convert.ToDecimal(result.Rows[^1]["amount"]));
    }

    [Fact]
    public void Between_filter_on_string_dates_is_inclusive()
    {
        var fields = new[] { new ReportFieldMeta("date", "Date", false), new ReportFieldMeta("v", "V", true) };
        var rows = new List<IReadOnlyDictionary<string, object?>>
        {
            new Dictionary<string, object?> { ["date"] = "2026-01-15", ["v"] = 1m },
            new Dictionary<string, object?> { ["date"] = "2026-03-10", ["v"] = 2m },
            new Dictionary<string, object?> { ["date"] = "2026-06-01", ["v"] = 3m }
        };
        var def = new ReportDefinition
        {
            Fields = new[] { "date" },
            Filters = new[] { new ReportFilter { Field = "date", Op = "between", Value = JsonValue.Create("2026-02-01"), Value2 = JsonValue.Create("2026-04-01") } }
        };
        var result = CustomReportRunner.Run(fields, rows, def);
        Assert.Equal(1, result.TotalRows);
    }
}
