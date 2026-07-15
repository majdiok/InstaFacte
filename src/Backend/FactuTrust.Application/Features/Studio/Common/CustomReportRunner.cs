using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FactuTrust.Application.Features.Studio.Common;

/// <summary>
/// Executes a <see cref="ReportDefinition"/> in-memory over rows of primitive values: applies
/// filters, then either projects detail rows or groups + aggregates. Source-agnostic — works for
/// custom records and whitelisted existing sources (the caller supplies primitive rows + field meta).
/// Read-only; volumes are bounded by the caller. Dates stored as ISO strings sort chronologically.
/// </summary>
public static class CustomReportRunner
{
    public static ReportResultDto Run(
        IReadOnlyList<ReportFieldMeta> fields,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        ReportDefinition def)
    {
        var fieldByKey = fields.ToDictionary(f => f.Key, StringComparer.Ordinal);

        var filtered = rows.Where(r => def.Filters.All(f => PassesFilter(r, f, fieldByKey))).ToList();

        return def.Grouping.Count == 0
            ? BuildDetail(filtered, def, fields, fieldByKey)
            : BuildGrouped(filtered, def, fieldByKey);
    }

    // ---- Detail ----

    private static ReportResultDto BuildDetail(
        List<IReadOnlyDictionary<string, object?>> rows,
        ReportDefinition def,
        IReadOnlyList<ReportFieldMeta> fields,
        IReadOnlyDictionary<string, ReportFieldMeta> fieldByKey)
    {
        var keys = def.Fields.Count > 0 ? def.Fields : fields.Select(f => f.Key).ToList();
        var columns = keys
            .Where(fieldByKey.ContainsKey)
            .Select(k => new ReportColumn(k, fieldByKey[k].Label, "dimension"))
            .ToList();

        var outRows = rows
            .Select(r => (IReadOnlyDictionary<string, object?>)columns.ToDictionary(c => c.Key, c => r.GetValueOrDefault(c.Key)))
            .ToList();

        outRows = ApplySort(outRows, def.Sort, fieldByKey);
        return new ReportResultDto(columns, outRows, outRows.Count);
    }

    // ---- Grouped + aggregated ----

    private static ReportResultDto BuildGrouped(
        List<IReadOnlyDictionary<string, object?>> rows,
        ReportDefinition def,
        IReadOnlyDictionary<string, ReportFieldMeta> fieldByKey)
    {
        var groupKeys = def.Grouping.Where(fieldByKey.ContainsKey).ToList();

        var columns = new List<ReportColumn>();
        foreach (var g in groupKeys)
            columns.Add(new ReportColumn(g, fieldByKey[g].Label, "dimension"));
        foreach (var a in def.Aggregations)
            columns.Add(new ReportColumn(AggKey(a), AggLabel(a, fieldByKey), "measure"));

        var groups = new Dictionary<string, List<IReadOnlyDictionary<string, object?>>>(StringComparer.Ordinal);
        var groupValues = new Dictionary<string, object?[]>(StringComparer.Ordinal);

        foreach (var r in rows)
        {
            var vals = groupKeys.Select(g => r.GetValueOrDefault(g)).ToArray();
            var key = string.Join("", vals.Select(v => v?.ToString() ?? " "));
            if (!groups.TryGetValue(key, out var list))
            {
                list = new List<IReadOnlyDictionary<string, object?>>();
                groups[key] = list;
                groupValues[key] = vals;
            }
            list.Add(r);
        }

        var outRows = new List<IReadOnlyDictionary<string, object?>>();
        foreach (var (key, members) in groups)
        {
            var row = new Dictionary<string, object?>();
            var vals = groupValues[key];
            for (var i = 0; i < groupKeys.Count; i++)
                row[groupKeys[i]] = vals[i];
            foreach (var a in def.Aggregations)
                row[AggKey(a)] = Aggregate(members, a);
            outRows.Add(row);
        }

        var sorted = ApplySort(outRows, def.Sort, fieldByKey);
        return new ReportResultDto(columns, sorted, sorted.Count);
    }

    private static object? Aggregate(List<IReadOnlyDictionary<string, object?>> members, ReportAggregation a)
    {
        if (string.Equals(a.Fn, "count", StringComparison.OrdinalIgnoreCase))
            return members.Count;

        var nums = members
            .Select(m => TryGetDecimal(m.GetValueOrDefault(a.Field), out var d) ? (decimal?)d : null)
            .Where(d => d.HasValue)
            .Select(d => d!.Value)
            .ToList();

        if (nums.Count == 0) return null;
        return a.Fn.ToLowerInvariant() switch
        {
            "sum" => nums.Sum(),
            "avg" => Math.Round(nums.Average(), 4),
            "min" => nums.Min(),
            "max" => nums.Max(),
            _ => null
        };
    }

    private static string AggKey(ReportAggregation a) =>
        string.Equals(a.Fn, "count", StringComparison.OrdinalIgnoreCase) ? "count" : $"{a.Fn.ToLowerInvariant()}_{a.Field}";

    private static string AggLabel(ReportAggregation a, IReadOnlyDictionary<string, ReportFieldMeta> fieldByKey)
    {
        if (string.Equals(a.Fn, "count", StringComparison.OrdinalIgnoreCase))
            return "Nombre";
        var fnLabel = a.Fn.ToLowerInvariant() switch
        {
            "sum" => "Somme",
            "avg" => "Moyenne",
            "min" => "Min",
            "max" => "Max",
            _ => a.Fn
        };
        var fieldLabel = fieldByKey.TryGetValue(a.Field, out var f) ? f.Label : a.Field;
        return $"{fnLabel} de {fieldLabel}";
    }

    // ---- Filters ----

    private static bool PassesFilter(
        IReadOnlyDictionary<string, object?> row,
        ReportFilter filter,
        IReadOnlyDictionary<string, ReportFieldMeta> fieldByKey)
    {
        if (!fieldByKey.TryGetValue(filter.Field, out var field))
            return true; // unknown filter field is ignored (defensive)

        var cell = row.GetValueOrDefault(filter.Field);
        var numeric = field.Numeric;
        var value = JsonToPrimitive(filter.Value);

        switch (filter.Op.ToLowerInvariant())
        {
            case "eq": return Compare(cell, value, numeric) == 0;
            case "neq": return Compare(cell, value, numeric) != 0;
            case "gt": return Compare(cell, value, numeric) > 0;
            case "gte": return Compare(cell, value, numeric) >= 0;
            case "lt": return Compare(cell, value, numeric) < 0;
            case "lte": return Compare(cell, value, numeric) <= 0;
            case "contains":
                return (ToStr(cell) ?? string.Empty).Contains(ToStr(value) ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            case "in":
                return filter.Value is JsonArray arr && arr.Any(v => Compare(cell, JsonToPrimitive(v), numeric) == 0);
            case "between":
                return Compare(cell, value, numeric) >= 0 && Compare(cell, JsonToPrimitive(filter.Value2), numeric) <= 0;
            default:
                return true;
        }
    }

    private static int Compare(object? cell, object? value, bool numeric)
    {
        if (numeric && TryGetDecimal(cell, out var a) && TryGetDecimal(value, out var b))
            return a.CompareTo(b);
        return string.Compare(ToStr(cell) ?? string.Empty, ToStr(value) ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    // ---- Sort ----

    private static List<IReadOnlyDictionary<string, object?>> ApplySort(
        List<IReadOnlyDictionary<string, object?>> rows,
        IReadOnlyList<ReportSort> sort,
        IReadOnlyDictionary<string, ReportFieldMeta> fieldByKey)
    {
        if (sort.Count == 0) return rows;
        IOrderedEnumerable<IReadOnlyDictionary<string, object?>>? ordered = null;
        foreach (var s in sort)
        {
            var key = s.Field;
            var numeric = fieldByKey.TryGetValue(key, out var f) && f.Numeric;
            Comparison<IReadOnlyDictionary<string, object?>> cmp = (x, y) => Compare(x.GetValueOrDefault(key), y.GetValueOrDefault(key), numeric);
            var comparer = Comparer<IReadOnlyDictionary<string, object?>>.Create(
                s.Dir.Equals("desc", StringComparison.OrdinalIgnoreCase) ? (x, y) => cmp(y, x) : cmp);
            ordered = ordered is null ? rows.OrderBy(r => r, comparer) : ordered.ThenBy(r => r, comparer);
        }
        return ordered!.ToList();
    }

    // ---- Value helpers ----

    public static object? JsonToPrimitive(JsonNode? node)
    {
        if (node is null) return null;
        if (node is JsonArray a) return string.Join(", ", a.Select(JsonToPrimitive));
        if (node is not JsonValue v) return null;
        if (v.TryGetValue<JsonElement>(out var el))
        {
            return el.ValueKind switch
            {
                JsonValueKind.Number => el.TryGetDecimal(out var d) ? d : (object?)el.GetRawText(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String => el.GetString(),
                _ => null
            };
        }
        if (v.TryGetValue<decimal>(out var dec)) return dec;
        if (v.TryGetValue<bool>(out var b2)) return b2;
        if (v.TryGetValue<string>(out var s)) return s;
        return v.ToString();
    }

    private static string? ToStr(object? value) => value switch
    {
        null => null,
        decimal d => d.ToString(CultureInfo.InvariantCulture),
        double db => db.ToString(CultureInfo.InvariantCulture),
        int i => i.ToString(CultureInfo.InvariantCulture),
        long l => l.ToString(CultureInfo.InvariantCulture),
        bool b => b ? "true" : "false",
        DateTime dt => dt.ToString("o", CultureInfo.InvariantCulture),
        _ => value.ToString()
    };

    private static bool TryGetDecimal(object? value, out decimal result)
    {
        switch (value)
        {
            case decimal d: result = d; return true;
            case double db: result = (decimal)db; return true;
            case int i: result = i; return true;
            case long l: result = l; return true;
            case string s when decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var ds): result = ds; return true;
            default: result = 0m; return false;
        }
    }
}
