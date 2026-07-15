using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Infrastructure.Services.Studio;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Studio;

public sealed class SqlColumnValueFormatterTests
{
    private readonly SqlColumnValueFormatter _formatter = new(new StubSqlSchemaProvider());

    [Fact]
    public void FormatDisplay_formats_datetime_in_fr()
    {
        var col = new SqlQueryColumnDto("CreatedAt", "Créé le", "dimension", "datetime", null, "datetime2", false);
        var result = _formatter.FormatDisplay(new DateTime(2026, 3, 22, 14, 30, 0), col, null);
        Assert.Contains("2026", result);
    }

    [Fact]
    public void FormatDisplay_maps_status_via_options()
    {
        var col = new SqlQueryColumnDto(
            "Status", "Statut", "dimension", "status",
            new ViewColumnFormatOptions { StatusMap = new Dictionary<string, string> { ["3"] = "Validé" } },
            "int", false);

        Assert.Equal("Validé", _formatter.FormatDisplay(3, col, null));
    }

    [Fact]
    public void FormatDisplay_fk_fallback_truncates_uuid()
    {
        var col = new SqlQueryColumnDto("WarehouseId", "Entrepôt", "dimension", "fk", null, "uniqueidentifier", false);
        var id = Guid.Parse("9be35db4-1234-5678-9abc-def012345678");
        var result = _formatter.FormatDisplay(id, col, null);
        Assert.StartsWith("9be35db4", result);
        Assert.Contains("…", result);
    }

    [Theory]
    [InlineData("datetime2", "datetime")]
    [InlineData("date", "date")]
    [InlineData("bit", "boolean")]
    [InlineData("money", "money")]
    public void SuggestFormat_detects_sql_types(string dataType, string expected)
    {
        var col = new SqlColumnInfo("Amount", dataType, false, true);
        Assert.Equal(expected, SqlColumnValueFormatter.SuggestFormat(col));
    }

    [Fact]
    public async Task ResolveDisplayValues_batches_one_call_per_fk_column_with_distinct_ids()
    {
        var stub = new StubSqlSchemaProvider();
        stub.Labels["g1"] = "Entrepôt A";
        stub.Labels["g2"] = "Entrepôt B";
        var formatter = new SqlColumnValueFormatter(stub);

        var col = new SqlQueryColumnDto(
            "WarehouseId", "Entrepôt", "dimension", "fk",
            new ViewColumnFormatOptions { LookupTable = "Warehouses", LookupDisplayColumn = "Name" },
            "uniqueidentifier", false);
        var rows = new IReadOnlyDictionary<string, object?>[]
        {
            new Dictionary<string, object?> { ["WarehouseId"] = "g1" },
            new Dictionary<string, object?> { ["WarehouseId"] = "g2" },
            new Dictionary<string, object?> { ["WarehouseId"] = "g1" } // duplicate id must not add a call
        };

        var resolved = await formatter.ResolveDisplayValuesAsync(Guid.NewGuid(), new[] { col }, rows);

        Assert.Equal(1, stub.BatchCalls);            // ONE batched query for the whole page, not per row
        Assert.Equal(2, stub.LastIdCount);           // distinct ids only
        Assert.Equal("Entrepôt A", resolved["WarehouseId:g1"]);
        Assert.Equal("Entrepôt B", resolved["WarehouseId:g2"]);
    }

    [Fact]
    public async Task ResolveDisplayValues_does_not_query_for_non_fk_columns()
    {
        var stub = new StubSqlSchemaProvider();
        var formatter = new SqlColumnValueFormatter(stub);
        var col = new SqlQueryColumnDto("Name", "Nom", "dimension", "text", null, "nvarchar", false);
        var rows = new IReadOnlyDictionary<string, object?>[] { new Dictionary<string, object?> { ["Name"] = "X" } };

        await formatter.ResolveDisplayValuesAsync(Guid.NewGuid(), new[] { col }, rows);

        Assert.Equal(0, stub.BatchCalls);
    }

    private sealed class StubSqlSchemaProvider : ISqlSchemaProvider
    {
        public int BatchCalls { get; private set; }
        public int LastIdCount { get; private set; }
        public Dictionary<string, string> Labels { get; } = new(StringComparer.Ordinal);

        public Task<IReadOnlyList<SqlTableInfo>> ListTablesAsync(Guid tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SqlTableInfo>>(Array.Empty<SqlTableInfo>());

        public Task<IReadOnlyList<SqlColumnInfo>?> ListColumnsAsync(Guid tenantId, string table, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SqlColumnInfo>?>(null);

        public Task<SqlQueryResultDto?> QueryAsync(Guid tenantId, string table, IReadOnlyList<string> columns, string? search, int page, int pageSize, CancellationToken cancellationToken = default)
            => Task.FromResult<SqlQueryResultDto?>(null);

        public Task<string?> LookupDisplayValueAsync(Guid tenantId, string table, string displayColumn, string idValue, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task<IReadOnlyDictionary<string, string>> LookupDisplayValuesAsync(
            Guid tenantId, string table, string displayColumn, IReadOnlyCollection<string> idValues, CancellationToken cancellationToken = default)
        {
            BatchCalls++;
            LastIdCount = idValues.Count;
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var id in idValues)
                if (Labels.TryGetValue(id, out var label)) map[id] = label;
            return Task.FromResult<IReadOnlyDictionary<string, string>>(map);
        }
    }
}