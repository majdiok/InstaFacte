using System.Globalization;
using FactuTrust.Application.Features.Studio.Common;

namespace FactuTrust.Infrastructure.Services.Studio;

public sealed class SqlColumnValueFormatter : ISqlColumnValueFormatter
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");
    private readonly ISqlSchemaProvider _schemaProvider;

    public SqlColumnValueFormatter(ISqlSchemaProvider schemaProvider) => _schemaProvider = schemaProvider;

    public static string? SuggestFormat(SqlColumnInfo col)
    {
        if (col.ForeignKey is not null || (col.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase) && col.Name != "Id"))
            return "fk";

        var dt = col.DataType.ToLowerInvariant();
        if (dt is "datetime" or "datetime2" or "smalldatetime") return "datetime";
        if (dt is "date") return "date";
        if (dt is "bit") return "boolean";
        if (dt is "decimal" or "money" or "smallmoney" or "numeric" or "float" or "real")
            return col.Numeric ? "money" : "number";
        if (col.Name.Contains("Status", StringComparison.OrdinalIgnoreCase)
            || col.Name.Equals("State", StringComparison.OrdinalIgnoreCase))
            return "status";
        if (dt is "uniqueidentifier") return "uuid";

        return null;
    }

    public async Task<IReadOnlyDictionary<string, string>> ResolveDisplayValuesAsync(
        Guid tenantId,
        IReadOnlyList<SqlQueryColumnDto> columns,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var col in columns.Where(c => string.Equals(c.Format, "fk", StringComparison.OrdinalIgnoreCase)))
        {
            var opts = col.FormatOptions;
            if (opts?.LookupTable is null || opts.LookupDisplayColumn is null) continue;

            // Collect every distinct id for this FK column across the page, then resolve them all in ONE query.
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                if (!row.TryGetValue(col.Key, out var val) || val is null) continue;
                var idStr = val.ToString()?.Trim();
                if (!string.IsNullOrEmpty(idStr)) ids.Add(idStr!);
            }
            if (ids.Count == 0) continue;

            IReadOnlyDictionary<string, string> labels;
            try
            {
                labels = await _schemaProvider.LookupDisplayValuesAsync(
                    tenantId, opts.LookupTable, opts.LookupDisplayColumn, ids, cancellationToken);
            }
            catch
            {
                continue; // best-effort: a failed FK resolution must never break the view
            }

            foreach (var (id, label) in labels)
                result[$"{col.Key}:{id}"] = label;
        }

        return result;
    }

    public string FormatDisplay(object? value, SqlQueryColumnDto column, IReadOnlyDictionary<string, string>? resolved)
    {
        if (value is null or DBNull) return string.Empty;

        var cacheKey = $"{column.Key}:{value}";
        if (resolved is not null && resolved.TryGetValue(cacheKey, out var fkLabel))
            return fkLabel;

        return column.Format?.ToLowerInvariant() switch
        {
            "date" => FormatDate(value, includeTime: false),
            "datetime" => FormatDate(value, includeTime: true),
            "number" => FormatNumber(value, money: false),
            "money" => FormatNumber(value, money: true),
            "boolean" => FormatBoolean(value),
            "uuid" => FormatUuid(value),
            "status" => FormatStatus(value, column.FormatOptions),
            "fk" => FormatFkFallback(value),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string FormatDate(object value, bool includeTime)
    {
        if (value is DateTime dt)
            return includeTime ? dt.ToString("g", Fr) : dt.ToString("d", Fr);
        if (DateTime.TryParse(value.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
            return includeTime ? parsed.ToString("g", Fr) : parsed.ToString("d", Fr);
        return value.ToString() ?? string.Empty;
    }

    private static string FormatNumber(object value, bool money)
    {
        if (decimal.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
            return money ? d.ToString("C", Fr) : d.ToString("N2", Fr);
        return value.ToString() ?? string.Empty;
    }

    private static string FormatBoolean(object value) => value switch
    {
        bool b => b ? "Oui" : "Non",
        byte or short or int or long => Convert.ToInt64(value) != 0 ? "Oui" : "Non",
        _ => value.ToString() ?? string.Empty
    };

    private static string FormatUuid(object value)
    {
        var s = value.ToString() ?? string.Empty;
        return s.Length > 12 ? s[..8] + "…" : s;
    }

    private static string FormatStatus(object value, ViewColumnFormatOptions? opts)
    {
        var key = value.ToString() ?? string.Empty;
        if (opts?.StatusMap is not null && opts.StatusMap.TryGetValue(key, out var label))
            return label;
        return key;
    }

    private static string FormatFkFallback(object value)
    {
        var s = value.ToString() ?? string.Empty;
        if (Guid.TryParse(s, out _) && s.Length > 12) return s[..8] + "…";
        return s;
    }
}