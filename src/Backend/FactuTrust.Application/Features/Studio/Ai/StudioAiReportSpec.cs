using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Application.Features.Studio.Common.SqlReport;

namespace FactuTrust.Application.Features.Studio.Ai;

/// <summary>
/// Spécification d'un ÉTAT demandée par l'assistant. Deux formes, la première étant préférée :
/// un <c>preset</c> (état métier prêt à l'emploi, tenable en UN seul tour d'outil sur CPU), ou une
/// <c>source</c> + regroupement/mesures libres, confrontés au schéma réel.
/// </summary>
public sealed record ParsedReportSpec(
    string Title,
    string? PresetKey,
    string? Source,
    IReadOnlyList<string> Grouping,
    IReadOnlyList<ReportAggregation> Measures,
    IReadOnlyList<string> Columns,
    IReadOnlyList<ReportFilter> Filters,
    IReadOnlyList<ReportSort> Sort,
    DateOnly? From,
    DateOnly? To,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Analyse le <c>spec_json</c> d'un état. Pur et défensif : dégrade au lieu d'échouer, et ne valide
/// AUCUN identifiant lui-même — c'est <see cref="SqlReportSqlBuilder"/>, face au schéma vivant, qui
/// écarte ce qui n'existe pas. Le modèle ne peut donc rien inventer qui atteigne la base.
/// </summary>
public static class StudioAiReportSpec
{
    public const int MaxGrouping = 5;
    public const int MaxMeasures = 10;
    public const int MaxColumns = 30;
    public const int MaxFilters = 20;

    private static readonly HashSet<string> KnownFunctions =
        new(StringComparer.OrdinalIgnoreCase) { "sum", "avg", "count", "min", "max" };

    /// <summary>Opérateurs acceptés, avec les alias français que le modèle produit spontanément.</summary>
    private static readonly IReadOnlyDictionary<string, string> OperatorAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["="] = "eq", ["=="] = "eq", ["eq"] = "eq", ["egal"] = "eq", ["égal"] = "eq",
            ["!="] = "neq", ["<>"] = "neq", ["neq"] = "neq", ["different"] = "neq", ["différent"] = "neq",
            [">"] = "gt", ["gt"] = "gt", ["superieur"] = "gt", ["supérieur"] = "gt",
            [">="] = "gte", ["gte"] = "gte",
            ["<"] = "lt", ["lt"] = "lt", ["inferieur"] = "lt", ["inférieur"] = "lt",
            ["<="] = "lte", ["lte"] = "lte",
            ["contains"] = "contains", ["contient"] = "contains", ["like"] = "contains",
            ["in"] = "in", ["dans"] = "in", ["parmi"] = "in",
            ["between"] = "between", ["entre"] = "between"
        };

    public static bool TryParse(string? specJson, out ParsedReportSpec? spec, out string? error)
    {
        spec = null;
        error = null;
        if (string.IsNullOrWhiteSpace(specJson)) { error = "spec_json est vide."; return false; }

        JsonNode? root;
        try { root = JsonNode.Parse(specJson); }
        catch (JsonException) { error = "spec_json n'est pas un JSON valide."; return false; }

        if (root is not JsonObject) { error = "spec_json doit être un objet JSON."; return false; }

        var warnings = new List<string>();

        var presetKey = Str(root!["preset"]) ?? Str(root["presetKey"]) ?? Str(root["modele"]);
        var source = Str(root["source"]) ?? Str(root["table"]) ?? Str(root["dataSource"]);

        if (presetKey is not null && SqlReportPresetCatalog.Find(presetKey) is null)
        {
            warnings.Add($"État prêt à l'emploi « {presetKey} » inconnu : ignoré.");
            presetKey = null;
        }

        if (presetKey is null && string.IsNullOrWhiteSpace(source))
        {
            error = "Indiquez soit `preset` (état prêt à l'emploi), soit `source` (table à analyser).";
            return false;
        }

        if (source is not null && !SqlSchemaGuard.IsValidIdentifier(source))
        {
            error = $"Nom de source invalide : « {source} ».";
            return false;
        }

        var title = Str(root["title"]) ?? Str(root["displayName"]) ?? Str(root["name"])
            ?? (presetKey is not null ? SqlReportPresetCatalog.Find(presetKey)!.DisplayName : source!);

        var (from, to) = ParsePeriod(root, warnings);

        spec = new ParsedReportSpec(
            title.Trim(),
            presetKey,
            source?.Trim(),
            ParseKeys(root["groupBy"] ?? root["grouping"] ?? root["regrouper"], MaxGrouping),
            ParseMeasures(root["measures"] ?? root["aggregations"] ?? root["mesures"], warnings),
            ParseKeys(root["columns"] ?? root["fields"] ?? root["colonnes"], MaxColumns),
            ParseFilters(root["filters"] ?? root["filtres"], warnings),
            ParseSort(root["sort"] ?? root["tri"]),
            from,
            to,
            warnings);
        return true;
    }

    /// <summary>
    /// Traduit la spécification en <see cref="ReportDefinition"/> exécutable. Pour un préréglage, les
    /// filtres métier de base sont conservés (ils DÉFINISSENT l'état) ; le modèle ne peut qu'ajouter.
    /// </summary>
    public static (string FactTable, ReportDefinition Definition) Materialize(ParsedReportSpec spec)
    {
        if (spec.PresetKey is not null && SqlReportPresetCatalog.Find(spec.PresetKey) is { } preset)
        {
            return (preset.FactTable, SqlReportPresetCatalog.Materialize(
                preset, spec.From, spec.To, spec.Filters,
                spec.Grouping.Count > 0 ? spec.Grouping : null));
        }

        var filters = new List<ReportFilter>(spec.Filters);
        var definition = new ReportDefinition
        {
            Grouping = spec.Grouping,
            Aggregations = spec.Grouping.Count > 0 ? spec.Measures : Array.Empty<ReportAggregation>(),
            Fields = spec.Grouping.Count > 0 ? Array.Empty<string>() : spec.Columns,
            Filters = filters,
            Sort = spec.Sort
        };
        return (spec.Source!, definition);
    }

    // ---- Analyse ---------------------------------------------------------------------------

    private static IReadOnlyList<string> ParseKeys(JsonNode? node, int max)
    {
        if (node is not JsonArray array)
            return Array.Empty<string>();

        var keys = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in array)
        {
            if (keys.Count >= max) break;
            var value = Str(item) ?? Str(item?["field"]) ?? Str(item?["key"]) ?? Str(item?["name"]);
            if (string.IsNullOrWhiteSpace(value)) continue;
            var trimmed = value!.Trim();
            if (seen.Add(trimmed)) keys.Add(trimmed);
        }
        return keys;
    }

    private static IReadOnlyList<ReportAggregation> ParseMeasures(JsonNode? node, List<string> warnings)
    {
        if (node is not JsonArray array)
            return Array.Empty<ReportAggregation>();

        var measures = new List<ReportAggregation>();
        foreach (var item in array)
        {
            if (measures.Count >= MaxMeasures) break;

            var fn = Str(item?["fn"]) ?? Str(item?["function"]) ?? Str(item?["agg"]) ?? "sum";
            if (!KnownFunctions.Contains(fn))
            {
                warnings.Add($"Calcul « {fn} » non reconnu : ignoré.");
                continue;
            }

            var field = Str(item?["field"]) ?? Str(item?["column"]) ?? Str(item);
            if (string.Equals(fn, "count", StringComparison.OrdinalIgnoreCase))
            {
                measures.Add(new ReportAggregation { Fn = "count" });
                continue;
            }
            if (string.IsNullOrWhiteSpace(field))
            {
                warnings.Add($"Calcul « {fn} » sans champ : ignoré.");
                continue;
            }
            measures.Add(new ReportAggregation { Field = field!.Trim(), Fn = fn.ToLowerInvariant() });
        }
        return measures;
    }

    private static IReadOnlyList<ReportFilter> ParseFilters(JsonNode? node, List<string> warnings)
    {
        if (node is not JsonArray array)
            return Array.Empty<ReportFilter>();

        var filters = new List<ReportFilter>();
        foreach (var item in array)
        {
            if (filters.Count >= MaxFilters) break;
            if (item is not JsonObject obj) continue;

            var field = Str(obj["field"]) ?? Str(obj["column"]) ?? Str(obj["champ"]);
            if (string.IsNullOrWhiteSpace(field)) continue;

            var rawOp = Str(obj["op"]) ?? Str(obj["operator"]) ?? Str(obj["operateur"]) ?? "eq";
            if (!OperatorAliases.TryGetValue(rawOp, out var op))
            {
                warnings.Add($"Opérateur « {rawOp} » non reconnu : filtre sur « {field} » ignoré.");
                continue;
            }

            filters.Add(new ReportFilter
            {
                Field = field!.Trim(),
                Op = op,
                Value = (obj["value"] ?? obj["valeur"])?.DeepClone(),
                Value2 = (obj["value2"] ?? obj["valeur2"])?.DeepClone()
            });
        }
        return filters;
    }

    private static IReadOnlyList<ReportSort> ParseSort(JsonNode? node)
    {
        if (node is not JsonArray array)
            return Array.Empty<ReportSort>();

        var sorts = new List<ReportSort>();
        foreach (var item in array)
        {
            var field = Str(item?["field"]) ?? Str(item?["column"]) ?? Str(item);
            if (string.IsNullOrWhiteSpace(field)) continue;
            var dir = Str(item?["dir"]) ?? Str(item?["direction"]) ?? "asc";
            sorts.Add(new ReportSort
            {
                Field = field!.Trim(),
                Dir = dir.StartsWith("desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc"
            });
        }
        return sorts;
    }

    private static (DateOnly? From, DateOnly? To) ParsePeriod(JsonNode root, List<string> warnings)
    {
        var period = root["period"] ?? root["periode"] ?? root["période"];
        var fromRaw = Str(root["from"]) ?? Str(root["dateFrom"]) ?? Str(period?["from"]) ?? Str(period?["debut"]);
        var toRaw = Str(root["to"]) ?? Str(root["dateTo"]) ?? Str(period?["to"]) ?? Str(period?["fin"]);

        DateOnly? from = null, to = null;
        if (fromRaw is not null && !TryDate(fromRaw, out from))
            warnings.Add($"Date de début « {fromRaw} » illisible : période ignorée.");
        if (toRaw is not null && !TryDate(toRaw, out to))
            warnings.Add($"Date de fin « {toRaw} » illisible : période ignorée.");

        // Bornes inversées : on les remet dans l'ordre plutôt que de rendre un état vide.
        if (from is not null && to is not null && from > to)
            (from, to) = (to, from);

        return (from, to);
    }

    private static bool TryDate(string raw, out DateOnly? value)
    {
        value = null;
        if (DateOnly.TryParseExact(raw.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
        {
            value = exact;
            return true;
        }
        if (DateTime.TryParse(raw.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var loose))
        {
            value = DateOnly.FromDateTime(loose);
            return true;
        }
        return false;
    }

    private static string? Str(JsonNode? node)
    {
        if (node is not JsonValue value) return null;
        if (value.TryGetValue<string>(out var text)) return string.IsNullOrWhiteSpace(text) ? null : text;
        if (value.TryGetValue<JsonElement>(out var element))
            return element.ValueKind == JsonValueKind.String ? element.GetString() : element.GetRawText();
        return value.ToString();
    }
}
