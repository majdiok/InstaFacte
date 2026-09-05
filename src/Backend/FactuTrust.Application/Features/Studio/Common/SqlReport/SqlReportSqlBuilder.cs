using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FactuTrust.Application.Features.Studio.Common.SqlReport;

/// <summary>
/// Traduit une <see cref="ReportDefinition"/> en SQL Server paramétré, à partir d'une
/// <see cref="SqlReportSchemaSnapshot"/> du schéma RÉEL.
///
/// Contrat de sûreté — c'est le cœur du dispositif :
/// <list type="bullet">
/// <item>aucun fragment de SQL ne provient de l'appelant : seuls des identifiants VALIDÉS contre le
/// schéma vivant sont insérés, et toujours bracket-quotés ;</item>
/// <item>toute valeur de filtre est un paramètre <c>@pN</c>, jamais une concaténation ;</item>
/// <item>les jointures sont choisies dans les arêtes de clés étrangères réelles du snapshot ;</item>
/// <item>un identifiant inconnu est ÉCARTÉ avec un avertissement, jamais deviné.</item>
/// </list>
/// Pur et sans dépendance : entièrement testable unitairement.
/// </summary>
public static class SqlReportSqlBuilder
{
    public const int MaxJoinedTables = 3;
    public const int MaxProjectedColumns = 50;
    public const int MaxGrouping = 5;
    public const int MaxMeasures = 10;
    public const int MaxFilters = 20;
    public const int MaxInValues = 50;

    private static readonly HashSet<string> AggregateFunctions =
        new(StringComparer.OrdinalIgnoreCase) { "sum", "avg", "count", "min", "max" };

    private static readonly HashSet<string> FilterOperators =
        new(StringComparer.OrdinalIgnoreCase) { "eq", "neq", "gt", "gte", "lt", "lte", "contains", "in", "between" };

    /// <summary>Clé publique d'un champ : <c>Table_Colonne</c>. Stable, elle est persistée dans les états.</summary>
    public static string FieldKey(string table, string column) => $"{table}_{column}";

    /// <summary>
    /// Granularités de date reconnues en suffixe de clé (<c>Invoices_IssueDate__month</c>). L'expression
    /// SQL est écrite ICI, jamais fournie par l'appelant : « par mois » reste une valeur d'un ensemble
    /// fermé, pas un fragment de requête. Les sorties sont des chaînes qui se trient chronologiquement.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, (string Suffix, string Label, string Format)> DateGranularities =
        new Dictionary<string, (string, string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["__day"] = ("__day", "jour", "CONVERT(char(10), {0}, 23)"),
            ["__month"] = ("__month", "mois", "CONVERT(char(7), {0}, 126)"),
            ["__quarter"] = ("__quarter", "trimestre", "CONCAT(DATEPART(year, {0}), '-T', DATEPART(quarter, {0}))"),
            ["__year"] = ("__year", "année", "CONVERT(char(4), {0}, 126)")
        };

    /// <summary>Clé d'une dimension de date agrégée : <c>Invoices_IssueDate__month</c>.</summary>
    public static string DateFieldKey(string table, string column, string granularity) =>
        $"{FieldKey(table, column)}__{granularity.ToLowerInvariant()}";

    /// <summary>Vrai si la clé se résout sur ce schéma. Sert à masquer un préréglage devenu invalide.</summary>
    public static bool CanResolve(SqlReportSchemaSnapshot snapshot, string? key) =>
        TryResolve(snapshot, key, out _);

    /// <summary>
    /// Champs exposés par un snapshot : colonnes de la table de faits d'abord, puis celles des tables
    /// joignables. Alimente le sélecteur du concepteur ET le catalogue vu par l'IA.
    /// </summary>
    public static IReadOnlyList<ReportFieldMeta> BuildFieldMeta(SqlReportSchemaSnapshot snapshot)
    {
        var meta = new List<ReportFieldMeta>();
        foreach (var table in OrderedTables(snapshot))
        {
            if (!snapshot.ColumnsByTable.TryGetValue(table, out var columns))
                continue;

            var isFact = string.Equals(table, snapshot.FactTable, StringComparison.OrdinalIgnoreCase);
            foreach (var column in columns)
            {
                if (SqlSchemaGuard.IsDeniedColumn(column.Name))
                    continue;
                var label = isFact ? Humanize(column.Name) : $"{Humanize(table)} · {Humanize(column.Name)}";
                meta.Add(new ReportFieldMeta(FieldKey(table, column.Name), label, column.Numeric));

                // Une date ouvre ses regroupements calendaires : « ventes par mois » est la demande la
                // plus courante et ne doit pas exiger une colonne dérivée dans la base.
                if (!IsDateColumn(column))
                    continue;
                foreach (var (suffix, entry) in DateGranularities)
                    meta.Add(new ReportFieldMeta(FieldKey(table, column.Name) + suffix, $"{label} ({entry.Label})", false));
            }
        }
        return meta;
    }

    public static bool TryBuild(
        SqlReportSchemaSnapshot snapshot,
        ReportDefinition definition,
        int maxRows,
        out SqlReportQuery? query,
        out string? error)
    {
        query = null;
        error = null;
        definition ??= new ReportDefinition();

        var warnings = new List<string>();
        var aliases = new TableAliases(snapshot.FactTable);

        // ---- Dimensions (regroupement) ou colonnes projetées (détail) ----
        var grouped = definition.Grouping.Count > 0;
        var requestedDimensions = grouped
            ? definition.Grouping.Take(MaxGrouping).ToList()
            : definition.Fields.Take(MaxProjectedColumns).ToList();

        var dimensions = new List<ResolvedField>();
        foreach (var key in requestedDimensions)
        {
            if (!TryResolve(snapshot, key, out var resolved))
            {
                warnings.Add($"Champ « {key} » absent du schéma : ignoré.");
                continue;
            }
            if (dimensions.Any(d => string.Equals(d.Key, resolved!.Key, StringComparison.Ordinal)))
                continue;
            dimensions.Add(resolved!);
        }

        // Regroupement demandé mais AUCUNE clé résolue : grouper devient impossible (un GROUP BY
        // sans expression n'est pas du SQL valide) et retomber sur un total global rendrait un état
        // FAUX. On refuse en nommant les clés fautives — c'est la seule information qui permette de
        // reformuler.
        if (grouped && dimensions.Count == 0)
        {
            error = $"Aucun des regroupements demandés n'existe sur « {snapshot.FactTable} » : "
                  + string.Join(", ", requestedDimensions) + ".";
            return false;
        }

        // Détail sans colonne demandée : on projette les colonnes de la table de faits (plafonnées),
        // comportement attendu d'un « montre-moi cette table ».
        if (!grouped && dimensions.Count == 0)
        {
            dimensions = snapshot.ColumnsByTable.TryGetValue(snapshot.FactTable, out var factColumns)
                ? factColumns
                    .Where(c => !SqlSchemaGuard.IsDeniedColumn(c.Name))
                    .Take(12)
                    .Select(c => new ResolvedField(snapshot.FactTable, c, FieldKey(snapshot.FactTable, c.Name)))
                    .ToList()
                : new List<ResolvedField>();
        }

        // ---- Mesures ----
        var measures = new List<ResolvedMeasure>();
        if (grouped)
        {
            foreach (var aggregation in definition.Aggregations.Take(MaxMeasures))
            {
                if (!AggregateFunctions.Contains(aggregation.Fn))
                {
                    warnings.Add($"Calcul « {aggregation.Fn} » non reconnu : ignoré.");
                    continue;
                }

                if (string.Equals(aggregation.Fn, "count", StringComparison.OrdinalIgnoreCase))
                {
                    if (measures.Any(m => m.Key == "count")) continue;
                    measures.Add(new ResolvedMeasure("count", null, "count", "Nombre"));
                    continue;
                }

                if (!TryResolve(snapshot, aggregation.Field, out var field))
                {
                    warnings.Add($"Champ « {aggregation.Field} » absent du schéma : calcul ignoré.");
                    continue;
                }
                if (!field!.Column.Numeric)
                {
                    warnings.Add($"« {field.Key} » n'est pas numérique : calcul ignoré.");
                    continue;
                }

                var key = $"{aggregation.Fn.ToLowerInvariant()}_{field.Key}";
                if (measures.Any(m => string.Equals(m.Key, key, StringComparison.Ordinal))) continue;
                measures.Add(new ResolvedMeasure(
                    aggregation.Fn.ToLowerInvariant(), field, key, MeasureLabel(aggregation.Fn, field)));
            }

            if (measures.Count == 0)
                measures.Add(new ResolvedMeasure("count", null, "count", "Nombre"));
        }

        if (dimensions.Count == 0 && measures.Count == 0)
        {
            error = "Aucune colonne exploitable sur cette source.";
            return false;
        }

        // ---- Filtres ----
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
        var whereParts = new List<string>();
        foreach (var filter in definition.Filters.Take(MaxFilters))
        {
            if (!FilterOperators.Contains(filter.Op ?? string.Empty))
            {
                warnings.Add($"Opérateur « {filter.Op} » non reconnu : filtre ignoré.");
                continue;
            }
            if (!TryResolve(snapshot, filter.Field, out var field))
            {
                warnings.Add($"Champ « {filter.Field} » absent du schéma : filtre ignoré.");
                continue;
            }

            var clause = BuildFilterClause(field!, filter, aliases, parameters, warnings);
            if (clause is not null)
                whereParts.Add(clause);
        }

        // ---- Jointures : uniquement pour les tables réellement référencées ----
        var referenced = dimensions.Select(d => d.Table)
            .Concat(measures.Where(m => m.Field is not null).Select(m => m.Field!.Table))
            .Concat(ReferencedFilterTables(snapshot, definition))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(t => !string.Equals(t, snapshot.FactTable, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (referenced.Count > MaxJoinedTables)
        {
            error = $"Trop de tables liées ({referenced.Count}) : {MaxJoinedTables} au maximum.";
            return false;
        }

        if (!TryBuildJoins(snapshot, referenced, aliases, out var joinSql, out var joinError))
        {
            error = joinError;
            return false;
        }

        // ---- Assemblage ----
        var columns = new List<ReportColumn>();
        var selectParts = new List<string>();

        foreach (var dimension in dimensions)
        {
            selectParts.Add($"{Expression(dimension, aliases)} AS {SqlSchemaGuard.Quote(dimension.Key)}");
            columns.Add(new ReportColumn(dimension.Key, LabelFor(snapshot, dimension), "dimension"));
        }

        foreach (var measure in measures)
        {
            var expression = measure.Fn == "count"
                ? "COUNT(*)"
                : $"{measure.Fn.ToUpperInvariant()}({aliases.Qualify(measure.Field!.Table, measure.Field.Column.Name)})";
            selectParts.Add($"{expression} AS {SqlSchemaGuard.Quote(measure.Key)}");
            columns.Add(new ReportColumn(measure.Key, measure.Label, "measure"));
        }

        // Invariant : un SELECT sans expression serait invalide. Inatteignable grâce aux gardes
        // ci-dessus, répété ici pour que l'assemblage ne dépende d'aucune hypothèse.
        if (selectParts.Count == 0)
        {
            error = "Aucune colonne exploitable sur cette source.";
            return false;
        }

        var where = whereParts.Count > 0 ? " WHERE " + string.Join(" AND ", whereParts) : string.Empty;
        var from = $" FROM [dbo].{SqlSchemaGuard.Quote(snapshot.FactTable)} AS {aliases.Of(snapshot.FactTable)}{joinSql}";
        // La condition porte sur les dimensions RÉSOLUES, jamais sur la seule demande : « GROUP BY »
        // suivi de rien produirait « GROUP BY  ORDER BY … », que SQL Server rejette.
        var groupBy = grouped && dimensions.Count > 0
            ? " GROUP BY " + string.Join(", ", dimensions.Select(d => Expression(d, aliases)))
            : string.Empty;

        var orderBy = BuildOrderBy(definition, columns, warnings);

        var safeRows = maxRows < 1 ? 1 : maxRows;
        var sql = new StringBuilder()
            .Append("SELECT ").Append(string.Join(", ", selectParts))
            .Append(from).Append(where).Append(groupBy).Append(orderBy)
            .Append(" OFFSET 0 ROWS FETCH NEXT ").Append(safeRows.ToString(CultureInfo.InvariantCulture))
            .Append(" ROWS ONLY")
            .ToString();

        // Comptage EXACT : sur les lignes de détail, ou sur le nombre de groupes.
        var countSql = groupBy.Length > 0
            ? $"SELECT COUNT(*) FROM (SELECT {string.Join(", ", dimensions.Select(d => $"{Expression(d, aliases)} AS {SqlSchemaGuard.Quote(d.Key)}"))}{from}{where}{groupBy}) AS g"
            : $"SELECT COUNT(*){from}{where}";

        query = new SqlReportQuery(sql, countSql, parameters, columns, warnings);
        return true;
    }

    // ---- Résolution des champs -------------------------------------------------------------

    private sealed record ResolvedField(string Table, SqlColumnInfo Column, string Key, string? DateGranularity = null);

    private sealed record ResolvedMeasure(string Fn, ResolvedField? Field, string Key, string Label);

    /// <summary>
    /// Résout une clé <c>Table_Colonne</c> — ou une colonne nue, alors cherchée sur la table de faits.
    /// Le préfixe n'est reconnu comme table que s'il est présent dans le snapshot : une colonne
    /// contenant un souligné (<c>UnitPrice_Amount</c>) reste donc correctement interprétée.
    /// </summary>
    private static bool TryResolve(SqlReportSchemaSnapshot snapshot, string? key, out ResolvedField? resolved)
    {
        resolved = null;
        if (string.IsNullOrWhiteSpace(key))
            return false;

        var trimmed = key!.Trim();

        // Suffixe de granularité de date : on le détache AVANT de chercher la colonne. Aucune colonne
        // réelle ne se termine par « __month » & co., la levée d'ambiguïté est donc sûre.
        string? granularity = null;
        foreach (var (suffix, entry) in DateGranularities)
        {
            if (!trimmed.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                continue;
            granularity = entry.Suffix;
            trimmed = trimmed[..^suffix.Length];
            break;
        }

        if (granularity is not null)
        {
            if (!TryResolveBase(snapshot, trimmed, out var dateField))
                return false;
            if (!IsDateColumn(dateField!.Column))
                return false;
            resolved = dateField with { Key = dateField.Key + granularity, DateGranularity = granularity };
            return true;
        }

        return TryResolveBase(snapshot, trimmed, out resolved);
    }

    private static bool IsDateColumn(SqlColumnInfo column) =>
        column.DataType.ToLowerInvariant() is "date" or "datetime" or "datetime2" or "smalldatetime" or "datetimeoffset";

    private static bool TryResolveBase(SqlReportSchemaSnapshot snapshot, string trimmed, out ResolvedField? resolved)
    {
        resolved = null;
        var separator = trimmed.IndexOf('_');
        if (separator > 0)
        {
            var candidateTable = trimmed[..separator];
            var candidateColumn = trimmed[(separator + 1)..];
            if (snapshot.ColumnsByTable.ContainsKey(candidateTable)
                && TryResolveIn(snapshot, candidateTable, candidateColumn, out resolved))
                return true;
        }

        return TryResolveIn(snapshot, snapshot.FactTable, trimmed, out resolved);
    }

    private static bool TryResolveIn(
        SqlReportSchemaSnapshot snapshot, string table, string column, out ResolvedField? resolved)
    {
        resolved = null;
        if (!snapshot.ColumnsByTable.TryGetValue(table, out var columns))
            return false;

        var match = columns.FirstOrDefault(c => string.Equals(c.Name, column, StringComparison.OrdinalIgnoreCase));
        if (match is null || SqlSchemaGuard.IsDeniedColumn(match.Name) || !SqlSchemaGuard.IsValidIdentifier(match.Name))
            return false;

        // La table réelle est celle du snapshot (casse canonique), pas celle écrite par l'appelant.
        var canonicalTable = snapshot.ColumnsByTable.Keys
            .First(t => string.Equals(t, table, StringComparison.OrdinalIgnoreCase));
        resolved = new ResolvedField(canonicalTable, match, FieldKey(canonicalTable, match.Name));
        return true;
    }

    private static IEnumerable<string> ReferencedFilterTables(SqlReportSchemaSnapshot snapshot, ReportDefinition definition)
    {
        foreach (var filter in definition.Filters.Take(MaxFilters))
        {
            if (TryResolve(snapshot, filter.Field, out var resolved))
                yield return resolved!.Table;
        }
    }

    // ---- Jointures -------------------------------------------------------------------------

    private static bool TryBuildJoins(
        SqlReportSchemaSnapshot snapshot,
        IReadOnlyList<string> targets,
        TableAliases aliases,
        out string joinSql,
        out string? error)
    {
        joinSql = string.Empty;
        error = null;
        if (targets.Count == 0)
            return true;

        var joined = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { snapshot.FactTable };
        var builder = new StringBuilder();

        foreach (var target in targets)
        {
            if (joined.Contains(target))
                continue;

            // Chemin de clés étrangères RÉELLES, 2 sauts au maximum. Les tables intermédiaires sont
            // jointes au passage : demander « le client » depuis une ligne de facture traverse la facture.
            var path = FindForeignKeyPath(snapshot.Edges, joined, target, maxDepth: 2);
            if (path is null)
            {
                error = $"Aucun lien (clé étrangère) entre « {snapshot.FactTable} » et « {target} ». " +
                        "Choisissez une source qui porte ce lien.";
                return false;
            }

            foreach (var edge in path)
            {
                if (!joined.Add(edge.ToTable))
                    continue;
                builder.Append(" LEFT JOIN [dbo].").Append(SqlSchemaGuard.Quote(edge.ToTable))
                    .Append(" AS ").Append(aliases.Of(edge.ToTable))
                    .Append(" ON ").Append(aliases.Qualify(edge.FromTable, edge.FromColumn))
                    .Append(" = ").Append(aliases.Qualify(edge.ToTable, edge.ToColumn));
            }
        }

        // Le plafond porte sur les tables RÉELLEMENT jointes, intermédiaires comprises.
        if (joined.Count - 1 > MaxJoinedTables)
        {
            error = $"Trop de tables liées ({joined.Count - 1}) : {MaxJoinedTables} au maximum.";
            return false;
        }

        joinSql = builder.ToString();
        return true;
    }

    /// <summary>Parcours en largeur borné sur les arêtes de clés étrangères. Aucune jointure devinée.</summary>
    private static List<SqlReportJoinEdge>? FindForeignKeyPath(
        IReadOnlyList<SqlReportJoinEdge> edges,
        IReadOnlyCollection<string> reachable,
        string target,
        int maxDepth)
    {
        var queue = new Queue<List<SqlReportJoinEdge>>();
        var visited = new HashSet<string>(reachable, StringComparer.OrdinalIgnoreCase);

        foreach (var start in reachable)
        {
            foreach (var edge in edges.Where(e => string.Equals(e.FromTable, start, StringComparison.OrdinalIgnoreCase)))
                queue.Enqueue(new List<SqlReportJoinEdge> { edge });
        }

        while (queue.Count > 0)
        {
            var path = queue.Dequeue();
            var last = path[^1];

            if (string.Equals(last.ToTable, target, StringComparison.OrdinalIgnoreCase))
                return path;

            if (path.Count >= maxDepth || !visited.Add(last.ToTable))
                continue;

            foreach (var edge in edges.Where(e => string.Equals(e.FromTable, last.ToTable, StringComparison.OrdinalIgnoreCase)))
                queue.Enqueue(new List<SqlReportJoinEdge>(path) { edge });
        }

        return null;
    }

    // ---- Filtres ---------------------------------------------------------------------------

    private static string? BuildFilterClause(
        ResolvedField field,
        ReportFilter filter,
        TableAliases aliases,
        Dictionary<string, object?> parameters,
        List<string> warnings)
    {
        var column = Expression(field, aliases);
        var op = (filter.Op ?? "eq").ToLowerInvariant();

        if (op == "in")
        {
            if (filter.Value is not JsonArray array || array.Count == 0)
            {
                warnings.Add($"Filtre « {field.Key} » : liste de valeurs vide, ignoré.");
                return null;
            }
            var names = new List<string>();
            foreach (var item in array.Take(MaxInValues))
                names.Add(AddParameter(parameters, Coerce(field.Column, item)));
            return $"{column} IN ({string.Join(", ", names)})";
        }

        if (op == "between")
        {
            if (filter.Value is null || filter.Value2 is null)
            {
                warnings.Add($"Filtre « {field.Key} » : bornes incomplètes, ignoré.");
                return null;
            }
            var low = AddParameter(parameters, Coerce(field.Column, filter.Value));
            var high = AddParameter(parameters, Coerce(field.Column, filter.Value2));
            return $"{column} >= {low} AND {column} <= {high}";
        }

        if (op == "contains")
        {
            var text = ValueToString(filter.Value);
            if (string.IsNullOrEmpty(text))
            {
                warnings.Add($"Filtre « {field.Key} » : valeur vide, ignoré.");
                return null;
            }
            var name = AddParameter(parameters, "%" + text + "%");
            return $"{column} LIKE {name}";
        }

        if (filter.Value is null)
        {
            return op switch
            {
                "eq" => $"{column} IS NULL",
                "neq" => $"{column} IS NOT NULL",
                _ => null
            };
        }

        var comparison = op switch
        {
            "eq" => "=",
            "neq" => "<>",
            "gt" => ">",
            "gte" => ">=",
            "lt" => "<",
            "lte" => "<=",
            _ => null
        };
        if (comparison is null)
            return null;

        var parameterName = AddParameter(parameters, Coerce(field.Column, filter.Value));
        return $"{column} {comparison} {parameterName}";
    }

    private static string AddParameter(Dictionary<string, object?> parameters, object? value)
    {
        var name = "@p" + parameters.Count.ToString(CultureInfo.InvariantCulture);
        parameters[name] = value;
        return name;
    }

    /// <summary>Aligne la valeur du filtre sur le type SQL de la colonne (nombre, date, booléen, texte).</summary>
    private static object? Coerce(SqlColumnInfo column, JsonNode? node)
    {
        var raw = ValueToString(node);
        if (raw is null)
            return null;

        if (column.Numeric)
            return decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var number)
                ? number
                : (object?)raw;

        var dataType = column.DataType.ToLowerInvariant();
        if (dataType is "date" or "datetime" or "datetime2" or "smalldatetime" or "datetimeoffset")
            return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                ? date
                : (object?)raw;

        if (dataType == "bit")
            return raw.ToLowerInvariant() switch
            {
                "true" or "1" or "oui" or "yes" => true,
                "false" or "0" or "non" or "no" => false,
                _ => (object?)raw
            };

        return raw;
    }

    private static string? ValueToString(JsonNode? node)
    {
        if (node is not JsonValue value)
            return null;
        if (value.TryGetValue<string>(out var text))
            return text;
        if (value.TryGetValue<JsonElement>(out var element))
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null
            };
        return value.ToString();
    }

    // ---- Tri -------------------------------------------------------------------------------

    private static string BuildOrderBy(ReportDefinition definition, IReadOnlyList<ReportColumn> columns, List<string> warnings)
    {
        var available = columns.Select(c => c.Key).ToHashSet(StringComparer.Ordinal);
        var parts = new List<string>();
        // Un même champ trié deux fois est refusé par SQL Server (« A column has been specified more
        // than once in the order by list »). On déduplique comme le font déjà dimensions et mesures.
        var sorted = new HashSet<string>(StringComparer.Ordinal);

        foreach (var sort in definition.Sort)
        {
            if (!available.Contains(sort.Field))
            {
                warnings.Add($"Tri sur « {sort.Field} » impossible : colonne absente du résultat, ignoré.");
                continue;
            }
            if (!sorted.Add(sort.Field))
                continue;
            var direction = string.Equals(sort.Dir, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
            parts.Add($"{SqlSchemaGuard.Quote(sort.Field)} {direction}");
        }

        if (parts.Count == 0)
        {
            // OFFSET/FETCH impose un ORDER BY : à défaut, la mesure décroissante (le classement
            // attendu d'un état), sinon la première dimension.
            var measure = columns.FirstOrDefault(c => c.Kind == "measure");
            var fallback = measure ?? columns[0];
            parts.Add($"{SqlSchemaGuard.Quote(fallback.Key)} {(measure is not null ? "DESC" : "ASC")}");
        }

        return " ORDER BY " + string.Join(", ", parts);
    }

    // ---- Libellés et alias -----------------------------------------------------------------

    private static IEnumerable<string> OrderedTables(SqlReportSchemaSnapshot snapshot) =>
        new[] { snapshot.FactTable }
            .Concat(snapshot.ColumnsByTable.Keys
                .Where(t => !string.Equals(t, snapshot.FactTable, StringComparison.OrdinalIgnoreCase))
                .OrderBy(t => t, StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// Expression SQL d'une dimension : la colonne qualifiée, ou son agrégation de date quand une
    /// granularité est demandée. Le gabarit vient du dictionnaire interne, jamais de l'appelant.
    /// </summary>
    private static string Expression(ResolvedField field, TableAliases aliases)
    {
        var qualified = aliases.Qualify(field.Table, field.Column.Name);
        if (field.DateGranularity is null || !DateGranularities.TryGetValue(field.DateGranularity, out var entry))
            return qualified;
        return string.Format(CultureInfo.InvariantCulture, entry.Format, qualified);
    }

    private static string LabelFor(SqlReportSchemaSnapshot snapshot, ResolvedField field)
    {
        var baseLabel = string.Equals(field.Table, snapshot.FactTable, StringComparison.OrdinalIgnoreCase)
            ? Humanize(field.Column.Name)
            : $"{Humanize(field.Table)} · {Humanize(field.Column.Name)}";

        return field.DateGranularity is not null && DateGranularities.TryGetValue(field.DateGranularity, out var entry)
            ? $"{baseLabel} ({entry.Label})"
            : baseLabel;
    }

    private static string MeasureLabel(string fn, ResolvedField field)
    {
        var prefix = fn.ToLowerInvariant() switch
        {
            "sum" => "Somme",
            "avg" => "Moyenne",
            "min" => "Min",
            "max" => "Max",
            _ => fn
        };
        return $"{prefix} de {Humanize(field.Column.Name)}";
    }

    /// <summary>« TotalHt_Amount » → « Total Ht · Amount ». Suffisant pour un en-tête lisible.</summary>
    internal static string Humanize(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return identifier;

        var segments = identifier.Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(SplitPascalCase);
        return string.Join(" · ", segments);
    }

    private static string SplitPascalCase(string value)
    {
        var builder = new StringBuilder(value.Length + 8);
        for (var i = 0; i < value.Length; i++)
        {
            if (i > 0 && char.IsUpper(value[i]) && !char.IsUpper(value[i - 1]))
                builder.Append(' ');
            builder.Append(value[i]);
        }
        return builder.ToString();
    }

    /// <summary>Alias de table générés par le code (t0, t1, …) : jamais issus d'une entrée externe.</summary>
    private sealed class TableAliases
    {
        private readonly Dictionary<string, string> _byTable = new(StringComparer.OrdinalIgnoreCase);

        public TableAliases(string factTable) => _byTable[factTable] = "t0";

        public string Of(string table)
        {
            if (_byTable.TryGetValue(table, out var alias))
                return alias;
            alias = "t" + _byTable.Count.ToString(CultureInfo.InvariantCulture);
            _byTable[table] = alias;
            return alias;
        }

        public string Qualify(string table, string column) => $"{Of(table)}.{SqlSchemaGuard.Quote(column)}";
    }
}
