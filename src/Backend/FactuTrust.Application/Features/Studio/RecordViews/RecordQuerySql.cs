using System.Data;
using System.Text.Json.Nodes;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Features.Studio.RecordViews;

/// <summary>
/// Spécification d'une requête enregistrements exécutée pour une vue : filtres/tri/recherche déjà
/// validés par <see cref="RecordViewDefinitionValidator"/>, pagination par <see cref="Skip"/> /
/// <see cref="Take"/>, et clés disposant de la colonne calculée indexée <c>jx_&lt;clé&gt;</c>
/// (<see cref="IndexedFieldKeys"/>, résolue côté Infrastructure).
/// </summary>
public sealed record RecordQuerySpec(
    Guid TenantId,
    Guid EntityDefinitionId,
    IReadOnlyList<RecordViewFilter> Filters,
    IReadOnlyList<RecordViewSort> Sort,
    string? Search,
    IReadOnlyList<string> SearchableFieldKeys,
    int Skip,
    int Take,
    IReadOnlySet<string> IndexedFieldKeys);

/// <summary>Fragments SQL sûrs et leurs paramètres (nom, valeur, type) — aucune valeur n'est concaténée au SQL.</summary>
public sealed record RecordQuerySqlResult(
    string WhereSql,
    string OrderBySql,
    IReadOnlyList<(string Name, object? Value, SqlDbType Type)> Parameters);

/// <summary>
/// Constructeur SQL PUR des vues enregistrées (testable sans base) : bâtit <c>WHERE</c> / <c>ORDER BY</c>
/// et la liste des paramètres pour une requête sur <c>CustomRecords</c>. Règles strictes :
/// <list type="bullet">
/// <item><c>TenantId</c>, <c>EntityDefinitionId</c> et <c>IsDeleted = 0</c> toujours dans le <c>WHERE</c> ;</item>
/// <item>chaque clé est revérifiée par <see cref="StudioKey.IsValidShape"/> (sinon <see cref="ArgumentException"/>,
/// appelant en faute) et les chemins JSON ne contiennent que ces clés validées ;</item>
/// <item>chaque VALEUR est un paramètre <c>@pN</c> typé — jamais de littéral utilisateur dans le SQL ;</item>
/// <item>expression par type : textes → <c>JSON_VALUE(DataJson, '$.&lt;clé&gt;')</c> ; numériques →
/// <c>TRY_CONVERT(decimal(18,6), JSON_VALUE(...))</c> ; dates → <c>TRY_CONVERT(datetime2, JSON_VALUE(...), 127)</c> ;
/// booléen <c>eq/neq</c> → <c>JSON_VALUE(...) IN ('true','1')</c> ; <c>in</c> sur MultiSelect →
/// <c>EXISTS (SELECT 1 FROM OPENJSON(DataJson, '$.&lt;clé&gt;') WHERE value = @pN)</c> ;</item>
/// <item>égalité sur clé indexée (≤ 450 caractères) : seek <c>[jx_&lt;clé&gt;] = @pN</c> + revérification exacte par
/// <c>JSON_VALUE</c> (même patron que <c>CustomRecordRepository.BuildFieldPredicate</c>) ;</item>
/// <item>tri par défaut <c>[CreatedAt] DESC, [Id] ASC</c> (déterministe pour la pagination OFFSET/FETCH).</item>
/// </list>
/// </summary>
public static class RecordQuerySql
{
    private const string TableAlias = "r";

    public static RecordQuerySqlResult Build(RecordQuerySpec spec, IReadOnlyDictionary<string, CustomFieldType> fieldTypes)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(fieldTypes);
        // Garde-fou du constructeur : une page ne dépasse jamais 10 000 lignes (bornes métier plus bas :
        // 200 liste / 500 kanban / 1000 calendrier, imposées par le handler Run).
        if (spec.Take is < 1 or > 10_000)
            throw new ArgumentException("Take must be within 1..10000.", nameof(spec));

        var parameters = new List<(string Name, object? Value, SqlDbType Type)>
        {
            ("@t", spec.TenantId, SqlDbType.UniqueIdentifier),
            ("@e", spec.EntityDefinitionId, SqlDbType.UniqueIdentifier)
        };
        var where = new List<string>
        {
            $"{TableAlias}.[TenantId] = @t",
            $"{TableAlias}.[EntityDefinitionId] = @e",
            $"{TableAlias}.[IsDeleted] = 0"
        };

        foreach (var filter in spec.Filters)
            where.Add(BuildFilterPredicate(filter, spec, fieldTypes, parameters));

        var search = spec.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
            where.Add(BuildSearchPredicate(search, spec, fieldTypes, parameters));

        var orderBy = BuildOrderBy(spec, fieldTypes);

        return new RecordQuerySqlResult(string.Join(" AND ", where), orderBy, parameters);
    }

    // ---- filtres ----

    private static string BuildFilterPredicate(
        RecordViewFilter filter,
        RecordQuerySpec spec,
        IReadOnlyDictionary<string, CustomFieldType> fieldTypes,
        List<(string Name, object? Value, SqlDbType Type)> parameters)
    {
        var key = ValidatedKey(filter.FieldKey);
        var op = (filter.Op ?? string.Empty).Trim().ToLowerInvariant();
        var jsonPath = JsonPath(key);
        var jsonValue = $"JSON_VALUE({TableAlias}.[DataJson], '{jsonPath}')";
        var type = fieldTypes.TryGetValue(key, out var t) ? t : (CustomFieldType?)null;

        switch (op)
        {
            case "is_empty":
                return $"({jsonValue} IS NULL OR {jsonValue} = '')";
            case "is_not_empty":
                return $"({jsonValue} IS NOT NULL AND {jsonValue} <> '')";

            case "eq" or "neq":
            {
                // Booléen : comparaison exacte sur les littéraux stockés ('true'/'1' et 'false'/'0').
                if (type == CustomFieldType.Boolean && TryBool(filter.Value, out var boolValue))
                {
                    return boolValue
                        ? (op == "eq" ? $"{jsonValue} IN ('true','1')" : $"({jsonValue} NOT IN ('true','1') OR {jsonValue} IS NULL)")
                        : (op == "eq" ? $"{jsonValue} IN ('false','0')" : $"({jsonValue} NOT IN ('false','0') OR {jsonValue} IS NULL)");
                }

                var p = AddParameter(parameters, ScalarValue(filter.Value));
                var useIndex = op == "eq" && spec.IndexedFieldKeys.Contains(key)
                    && p.Value is string s && s.Length <= JsonIndexSql.ValueMaxLength;
                var eq = $"{ValueExpression(key, type)} = {p.Name}";
                var predicate = useIndex
                    ? $"({SqlSchemaGuard.Quote(JsonIndexSql.ColumnName(key))} = {p.Name} AND {jsonValue} = {p.Name})"
                    : eq;
                return op == "eq" ? predicate : $"NOT ({predicate})";
            }

            case "contains":
            {
                var p = AddParameter(parameters, "%" + EscapeLike(ScalarText(filter.Value)) + "%");
                // MultiSelect stocke un TABLEAU JSON : JSON_VALUE y renvoie NULL (LIKE muet, faux négatifs).
                // On cherche dans les éléments via OPENJSON — même patron que `in` sur MultiSelect.
                return type == CustomFieldType.MultiSelect
                    ? $"EXISTS (SELECT 1 FROM OPENJSON({TableAlias}.[DataJson], '{jsonPath}') WHERE [value] LIKE {p.Name} ESCAPE '\\')"
                    : $"{jsonValue} LIKE {p.Name} ESCAPE '\\'";
            }

            case "gt" or "gte" or "lt" or "lte":
            {
                var sqlOp = op switch { "gt" => ">", "gte" => ">=", "lt" => "<", _ => "<=" };
                var p = AddParameter(parameters, TypedScalar(filter.Value, type));
                return $"{ValueExpression(key, type)} {sqlOp} {p.Name}";
            }

            case "between":
            {
                var (lo, hi) = Bounds(filter.Value);
                var pLo = AddParameter(parameters, TypedScalar(lo, type));
                var pHi = AddParameter(parameters, TypedScalar(hi, type));
                return $"{ValueExpression(key, type)} BETWEEN {pLo.Name} AND {pHi.Name}";
            }

            case "in":
            {
                var values = filter.Value as JsonArray
                    ?? throw new ArgumentException($"Filter 'in' on '{key}' expects a JSON array value.", nameof(filter));
                var names = new List<string>(values.Count);
                foreach (var item in values)
                    names.Add(AddParameter(parameters, item is null ? null : ScalarValue(item)).Name);
                if (names.Count == 0)
                    return "1 = 0"; // `in []` ne correspond à rien.
                if (type == CustomFieldType.MultiSelect)
                {
                    var clauses = names.Select(n =>
                        $"EXISTS (SELECT 1 FROM OPENJSON({TableAlias}.[DataJson], '{jsonPath}') WHERE [value] = {n})");
                    return "(" + string.Join(" OR ", clauses) + ")";
                }
                return $"{jsonValue} IN (" + string.Join(", ", names) + ")";
            }

            default:
                throw new ArgumentException($"Unknown view filter operator: '{filter.Op}'.", nameof(filter));
        }
    }

    private static string BuildSearchPredicate(
        string search,
        RecordQuerySpec spec,
        IReadOnlyDictionary<string, CustomFieldType> fieldTypes,
        List<(string Name, object? Value, SqlDbType Type)> parameters)
    {
        var p = AddParameter(parameters, "%" + EscapeLike(search) + "%");
        var keys = spec.SearchableFieldKeys.Where(k => !string.IsNullOrWhiteSpace(k)).Take(6).ToList();

        if (keys.Count == 0)
            return $"{TableAlias}.[DataJson] LIKE {p.Name} ESCAPE '\\'";

        var clauses = keys.Select(k =>
        {
            var key = ValidatedKey(k);
            // Numérique/date : on convertit la saisie et on compare sur l'expression typée ; sinon LIKE.
            var type = fieldTypes.TryGetValue(key, out var t) ? t : (CustomFieldType?)null;
            return type is CustomFieldType.Number or CustomFieldType.Decimal or CustomFieldType.Money
                or CustomFieldType.Percentage or CustomFieldType.Rating or CustomFieldType.Date or CustomFieldType.DateTime
                ? $"CONVERT(nvarchar(64), {ValueExpression(key, type)}) LIKE {p.Name} ESCAPE '\\'"
                : $"JSON_VALUE({TableAlias}.[DataJson], '{JsonPath(key)}') LIKE {p.Name} ESCAPE '\\'";
        });
        return "(" + string.Join(" OR ", clauses) + ")";
    }

    // ---- tri ----

    private static string BuildOrderBy(RecordQuerySpec spec, IReadOnlyDictionary<string, CustomFieldType> fieldTypes)
    {
        var parts = new List<string>(spec.Sort.Count + 2);
        foreach (var sort in spec.Sort)
        {
            var direction = sort.Descending ? "DESC" : "ASC";
            if (sort.FieldKey == "createdAt")
            {
                parts.Add($"{TableAlias}.[CreatedAt] {direction}");
                continue;
            }
            if (sort.FieldKey == "updatedAt")
            {
                parts.Add($"{TableAlias}.[UpdatedAt] {direction}");
                continue;
            }
            var key = ValidatedKey(sort.FieldKey);
            var type = fieldTypes.TryGetValue(key, out var t) ? t : (CustomFieldType?)null;
            parts.Add($"{ValueExpression(key, type)} {direction}");
        }
        // Ordre par défaut + tie-breaker stable : la pagination OFFSET/FETCH reste déterministe.
        if (parts.Count == 0)
            parts.Add($"{TableAlias}.[CreatedAt] DESC");
        parts.Add($"{TableAlias}.[Id] ASC");
        return string.Join(", ", parts);
    }

    // ---- expressions et valeurs ----

    /// <summary>Expression SQL de comparaison pour une clé, selon son type (JSON_VALUE / TRY_CONVERT).</summary>
    private static string ValueExpression(string key, CustomFieldType? type)
    {
        var jsonValue = $"JSON_VALUE({TableAlias}.[DataJson], '{JsonPath(key)}')";
        return type switch
        {
            CustomFieldType.Number or CustomFieldType.Decimal or CustomFieldType.Money
                or CustomFieldType.Percentage or CustomFieldType.Rating or CustomFieldType.AutoNumber
                => $"TRY_CONVERT(decimal(18,6), {jsonValue})",
            CustomFieldType.Date or CustomFieldType.DateTime
                => $"TRY_CONVERT(datetime2, {jsonValue}, 127)",
            _ => jsonValue
        };
    }

    /// <summary>Valeur scalaire d'un filtre : string, decimal (numériques), DateTime (dates), ou null.</summary>
    private static object? ScalarValue(JsonNode? value) => value switch
    {
        null => null,
        JsonValue v when v.TryGetValue<string>(out var s) => s,
        JsonValue v when v.TryGetValue<bool>(out var b) => b ? "true" : "false",
        JsonValue v when v.TryGetValue<decimal>(out var d) => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
        JsonValue v when v.TryGetValue<double>(out var dbl) => dbl.ToString(System.Globalization.CultureInfo.InvariantCulture),
        _ => value.ToJsonString()
    };

    /// <summary>Texte brut pour les opérateurs LIKE.</summary>
    private static string ScalarText(JsonNode? value) => ScalarValue(value) as string ?? string.Empty;

    /// <summary>Valeur typée pour gt/gte/lt/lte/between : decimal ou DateTime selon le champ, string sinon.</summary>
    private static object? TypedScalar(JsonNode? value, CustomFieldType? type)
    {
        if (value is null) return null;
        var raw = ScalarValue(value);
        if (raw is null) return null;
        switch (type)
        {
            case CustomFieldType.Number or CustomFieldType.Decimal or CustomFieldType.Money
                or CustomFieldType.Percentage or CustomFieldType.Rating or CustomFieldType.AutoNumber:
                return raw is string s && decimal.TryParse(s, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : raw;
            case CustomFieldType.Date or CustomFieldType.DateTime:
                return raw is string ds && DateTime.TryParse(ds, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var dt) ? dt : raw;
            default:
                return raw;
        }
    }

    private static (JsonNode? Lo, JsonNode? Hi) Bounds(JsonNode? value)
    {
        if (value is not JsonArray { Count: 2 } array)
            throw new ArgumentException("Filter 'between' expects a JSON array of exactly 2 bounds.", nameof(value));
        return (array[0], array[1]);
    }

    private static bool TryBool(JsonNode? value, out bool result)
    {
        result = false;
        if (value is not JsonValue v) return false;
        if (v.TryGetValue<bool>(out var b)) { result = b; return true; }
        if (v.TryGetValue<string>(out var s) && bool.TryParse(s, out var parsed)) { result = parsed; return true; }
        return false;
    }

    private static string EscapeLike(string term) =>
        term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");

    /// <summary>Revérifie la clé (jamais de confiance en l'appelant pour du SQL) et renvoie la clé validée.</summary>
    private static string ValidatedKey(string? key)
    {
        if (!StudioKey.IsValidShape(key))
            throw new ArgumentException($"Invalid Studio key shape for SQL: '{key}'.", nameof(key));
        return key!;
    }

    private static string JsonPath(string key) => "$." + key;

    private static (string Name, object? Value, SqlDbType Type) AddParameter(
        List<(string Name, object? Value, SqlDbType Type)> parameters, object? value)
    {
        var type = value switch
        {
            null => SqlDbType.NVarChar,
            decimal => SqlDbType.Decimal,
            DateTime => SqlDbType.DateTime2,
            _ => SqlDbType.NVarChar
        };
        var p = ($"@p{parameters.Count - 2}", value, type); // @t/@e occupent les deux premières positions
        parameters.Add(p);
        return p;
    }
}
