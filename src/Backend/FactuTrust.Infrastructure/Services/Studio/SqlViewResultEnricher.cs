using FactuTrust.Application.Features.Studio.Common;

namespace FactuTrust.Infrastructure.Services.Studio;

public sealed class SqlViewResultEnricher : ISqlViewResultEnricher
{
    private readonly ISqlColumnValueFormatter _formatter;

    public SqlViewResultEnricher(ISqlColumnValueFormatter formatter) => _formatter = formatter;

    public async Task<SqlQueryResultDto> EnrichAsync(
        Guid tenantId,
        string sourceTable,
        IReadOnlyList<ViewColumn> viewColumns,
        IReadOnlyList<SqlColumnInfo> liveColumns,
        SqlQueryResultDto raw,
        CancellationToken cancellationToken = default)
    {
        var liveByName = liveColumns.ToDictionary(c => c.Name, StringComparer.Ordinal);
        var viewByName = viewColumns.ToDictionary(c => c.Name, StringComparer.Ordinal);

        var enrichedColumns = raw.Columns.Select(col =>
        {
            viewByName.TryGetValue(col.Key, out var vc);
            liveByName.TryGetValue(col.Key, out var live);

            var label = !string.IsNullOrWhiteSpace(vc?.Label) ? vc!.Label!.Trim() : col.Label;
            var format = vc?.Format ?? col.Format ?? live?.SuggestedFormat;
            var formatOptions = vc?.FormatOptions ?? BuildDefaultFormatOptions(live, format);

            return new SqlQueryColumnDto(
                col.Key,
                label,
                col.Kind,
                format,
                formatOptions,
                live?.DataType ?? col.DataType,
                live?.Numeric ?? col.Numeric);
        }).ToList();

        var resolved = await _formatter.ResolveDisplayValuesAsync(tenantId, enrichedColumns, raw.Rows, cancellationToken);

        var displayValues = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in raw.Rows)
        {
            foreach (var col in enrichedColumns)
            {
                if (!row.TryGetValue(col.Key, out var val) || val is null) continue;
                var cacheKey = $"{col.Key}:{val}";
                if (resolved.TryGetValue(cacheKey, out var resolvedLabel))
                {
                    displayValues[cacheKey] = resolvedLabel;
                    continue;
                }

                var formatted = _formatter.FormatDisplay(val, col, resolved);
                var rawStr = val.ToString() ?? string.Empty;
                if (!string.Equals(formatted, rawStr, StringComparison.Ordinal))
                    displayValues[cacheKey] = formatted;
            }
        }

        return new SqlQueryResultDto(enrichedColumns, raw.Rows, raw.TotalRows, displayValues);
    }

    private static ViewColumnFormatOptions? BuildDefaultFormatOptions(SqlColumnInfo? live, string? format)
    {
        if (live?.ForeignKey is null) return null;
        if (!string.Equals(format, "fk", StringComparison.OrdinalIgnoreCase)
            && !live.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase))
            return null;

        return new ViewColumnFormatOptions
        {
            LookupTable = live.ForeignKey.ReferencedTable,
            LookupDisplayColumn = GuessDisplayColumn(live.ForeignKey.ReferencedTable)
        };
    }

    private static string GuessDisplayColumn(string table) => table switch
    {
        "Warehouses" or "Products" or "Clients" => "Name",
        "AspNetUsers" => "UserName",
        _ => "Name",
    };
}