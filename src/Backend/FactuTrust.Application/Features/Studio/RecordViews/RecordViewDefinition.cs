using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Features.Studio.RecordViews;

/// <summary>Une colonne d'une vue enregistrée : champ actif de l'entité (ou <c>createdAt</c> / <c>updatedAt</c>).</summary>
public sealed record RecordViewColumn(string FieldKey, int? Width = null, bool Hidden = false);

/// <summary>
/// Un filtre d'une vue enregistrée. <c>Op</c> ∈ <c>eq | neq | contains | gt | gte | lt | lte | in |
/// is_empty | is_not_empty | between</c>. La compatibilité opérateur / type de champ est validée par
/// <see cref="RecordViewDefinitionValidator"/> et revérifiée à l'exécution. <c>Value</c> est un JSON
/// libre : scalaire pour eq/neq/contains/gt/gte/lt/lte, tableau pour <c>in</c>, tableau de 2 bornes pour
/// <c>between</c>, ignoré pour <c>is_empty</c> / <c>is_not_empty</c>.
/// </summary>
public sealed record RecordViewFilter(string FieldKey, string Op, JsonNode? Value);

/// <summary>Un tri d'une vue enregistrée (champ actif, <c>createdAt</c> ou <c>updatedAt</c>).</summary>
public sealed record RecordViewSort(string FieldKey, bool Descending = false);

/// <summary>Options du mode Kanban : regroupement par un champ <c>Select</c> (obligatoire).</summary>
public sealed record RecordViewKanban(
    string GroupByFieldKey,
    string? TitleFieldKey,
    IReadOnlyList<string>? CardFieldKeys,
    IReadOnlyList<string>? ColumnOrder,
    bool ShowEmptyGroup = true);

/// <summary>Options du mode Calendrier : borne début (et fin optionnelle) sur des champs <c>Date</c> / <c>DateTime</c>.</summary>
public sealed record RecordViewCalendar(string StartFieldKey, string? EndFieldKey, string? TitleFieldKey, string? ColorFieldKey);

/// <summary>
/// Définition typée d'une vue enregistrée, stockée sérialisée (camelCase) dans
/// <c>CustomRecordViewDefinition.DefinitionJson</c>. Les bornes (25 colonnes, 10 filtres, 3 tris,
/// pageSize 1..200) sont imposées par <see cref="RecordViewDefinitionValidator"/>.
/// </summary>
public sealed record RecordViewDefinition(
    IReadOnlyList<RecordViewColumn> Columns,
    IReadOnlyList<RecordViewFilter> Filters,
    IReadOnlyList<RecordViewSort> Sort,
    RecordViewKanban? Kanban,
    RecordViewCalendar? Calendar,
    bool SearchEnabled = true,
    int PageSize = 25);

/// <summary>
/// (Dé)sérialisation camelCase de <see cref="RecordViewDefinition"/>. Le parse est permissif
/// (<c>null</c> sur JSON corrompu) : c'est le validateur qui refuse une définition illisible.
/// </summary>
public static class RecordViewDefinitionJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static RecordViewDefinition? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<RecordViewDefinition>(json, Options); }
        catch (JsonException) { return null; }
    }

    public static string Serialize(RecordViewDefinition definition) => JsonSerializer.Serialize(definition, Options);
}

/// <summary>
/// Validation d'une <see cref="RecordViewDefinition"/> contre les champs actifs d'une entité.
/// Pure (aucune dépendance) : mêmes règles à l'enregistrement de la vue et à son exécution.
/// </summary>
public static class RecordViewDefinitionValidator
{
    public const int MaxColumns = 25, MaxFilters = 10, MaxSort = 3;

    public static readonly string[] Operators =
        { "eq", "neq", "contains", "gt", "gte", "lt", "lte", "in", "is_empty", "is_not_empty", "between" };

    /// <summary>Clés persistées (hors <c>DataJson</c>) autorisées en colonne / tri / recherche.</summary>
    public static readonly IReadOnlySet<string> PersistedKeys = new HashSet<string>(StringComparer.Ordinal)
        { "createdAt", "updatedAt" };

    public static bool IsPersistedKey(string fieldKey) => PersistedKeys.Contains(fieldKey);

    public static Result<RecordViewDefinition> Validate(
        RecordViewDefinition definition, CustomRecordViewMode mode, IReadOnlyList<CustomFieldDefinition> fields)
    {
        var activeFields = fields.Where(f => f.IsActive).ToList();
        var byKey = activeFields.ToDictionary(f => f.Key, StringComparer.Ordinal);

        if (definition.Columns.Count > MaxColumns)
            return Result.Failure<RecordViewDefinition>(
                Error.Validation("columns", $"Une vue accepte au plus {MaxColumns} colonnes."));
        if (definition.Filters.Count > MaxFilters)
            return Result.Failure<RecordViewDefinition>(
                Error.Validation("filters", $"Une vue accepte au plus {MaxFilters} filtres."));
        if (definition.Sort.Count > MaxSort)
            return Result.Failure<RecordViewDefinition>(
                Error.Validation("sort", $"Une vue accepte au plus {MaxSort} critères de tri."));
        if (definition.PageSize is < 1 or > 200)
            return Result.Failure<RecordViewDefinition>(
                Error.Validation("pageSize", "La taille de page doit être comprise entre 1 et 200."));

        foreach (var column in definition.Columns)
        {
            var keyError = RequireKnownKey(column.FieldKey, byKey, "columns", out _);
            if (keyError is not null) return Result.Failure<RecordViewDefinition>(keyError);
        }

        foreach (var sort in definition.Sort)
        {
            var keyError = RequireKnownKey(sort.FieldKey, byKey, "sort", out var field);
            if (keyError is not null) return Result.Failure<RecordViewDefinition>(keyError);
            if (field is not null && CustomRecordValidator.IsComputed(field.FieldType))
                return Result.Failure<RecordViewDefinition>(
                    Error.Validation("sort", $"Champ calculé non triable : « {sort.FieldKey} »."));
        }

        var filterError = ValidateFilters(definition.Filters, byKey);
        if (filterError is not null) return Result.Failure<RecordViewDefinition>(filterError);

        var modeError = mode switch
        {
            CustomRecordViewMode.Kanban => ValidateKanban(definition.Kanban, byKey),
            CustomRecordViewMode.Calendar => ValidateCalendar(definition.Calendar, byKey),
            _ => null
        };
        return modeError is null ? Result.Success(definition) : Result.Failure<RecordViewDefinition>(modeError);
    }

    /// <summary>
    /// Revalide des filtres additionnels (exécution : <c>extraFilters</c>) contre les mêmes champs actifs.
    /// </summary>
    public static Error? ValidateFilters(
        IReadOnlyList<RecordViewFilter> filters, IReadOnlyDictionary<string, CustomFieldDefinition> fieldsByKey)
    {
        foreach (var filter in filters)
        {
            var keyError = RequireKnownKey(filter.FieldKey, fieldsByKey, "filters", out var field);
            if (keyError is not null) return keyError;
            // Les colonnes createdAt/updatedAt sont triables mais pas filtrables (pas dans DataJson).
            if (field is null)
                return Error.Validation("filters", $"Champ non filtrable : « {filter.FieldKey} ».");
            if (CustomRecordValidator.IsComputed(field.FieldType))
                return Error.Validation("filters", $"Champ calculé non filtrable : « {filter.FieldKey} ».");

            var op = (filter.Op ?? string.Empty).Trim().ToLowerInvariant();
            if (!Operators.Contains(op))
                return Error.Validation("filters", $"Opérateur inconnu : « {filter.Op} ».");

            var typeError = ValidateOperatorForType(filter, op, field.FieldType);
            if (typeError is not null) return typeError;
        }
        return null;
    }

    private static Error? ValidateOperatorForType(RecordViewFilter filter, string op, CustomFieldType type)
    {
        var compatible = op switch
        {
            "eq" or "neq" or "is_empty" or "is_not_empty" => true,
            "contains" => type is CustomFieldType.Text or CustomFieldType.MultilineText
                or CustomFieldType.Select or CustomFieldType.MultiSelect,
            "gt" or "gte" or "lt" or "lte" or "between" => type is CustomFieldType.Number or CustomFieldType.Decimal
                or CustomFieldType.Money or CustomFieldType.Percentage or CustomFieldType.Rating
                or CustomFieldType.Date or CustomFieldType.DateTime or CustomFieldType.AutoNumber,
            "in" => type is CustomFieldType.Select or CustomFieldType.MultiSelect
                or CustomFieldType.RelationCustom or CustomFieldType.RelationExisting,
            _ => false
        };
        return compatible
            ? null
            : Error.Validation("filters", $"Opérateur « {op} » incompatible avec le type {type} du champ « {filter.FieldKey} ».");
    }

    private static Error? ValidateKanban(
        RecordViewKanban? kanban, IReadOnlyDictionary<string, CustomFieldDefinition> fieldsByKey)
    {
        if (kanban is null)
            return Error.Validation("kanban", "Une vue Kanban exige la section « kanban » de la définition.");

        if (!fieldsByKey.TryGetValue(kanban.GroupByFieldKey ?? string.Empty, out var groupBy))
            return Error.Validation("kanban", $"Champ de regroupement inconnu : « {kanban.GroupByFieldKey} ».");
        if (groupBy.FieldType != CustomFieldType.Select)
            return Error.Validation("kanban", $"Le regroupement Kanban exige un champ Select : « {kanban.GroupByFieldKey} » est de type {groupBy.FieldType}.");
        if (StudioFieldJson.ParseOptions(groupBy.OptionsJson) is not { Count: > 0 })
            return Error.Validation("kanban", $"Le champ « {kanban.GroupByFieldKey} » n'a aucune option à regrouper.");

        if (kanban.CardFieldKeys is { Count: > 6 })
            return Error.Validation("kanban", "Une carte Kanban affiche au plus 6 champs.");

        var referenceKeys = new[] { kanban.TitleFieldKey }.Concat(kanban.CardFieldKeys ?? Array.Empty<string>());
        foreach (var key in referenceKeys.Where(k => !string.IsNullOrWhiteSpace(k)))
        {
            if (!fieldsByKey.ContainsKey(key!) && !IsPersistedKey(key!))
                return Error.Validation("kanban", $"Champ inconnu : « {key} ».");
        }
        return null;
    }

    private static Error? ValidateCalendar(
        RecordViewCalendar? calendar, IReadOnlyDictionary<string, CustomFieldDefinition> fieldsByKey)
    {
        if (calendar is null)
            return Error.Validation("calendar", "Une vue Calendrier exige la section « calendar » de la définition.");

        if (!fieldsByKey.TryGetValue(calendar.StartFieldKey ?? string.Empty, out var start))
            return Error.Validation("calendar", $"Champ de début inconnu : « {calendar.StartFieldKey} ».");
        if (start.FieldType is not (CustomFieldType.Date or CustomFieldType.DateTime))
            return Error.Validation("calendar", $"Le début d'une vue Calendrier exige un champ Date/DateTime : « {calendar.StartFieldKey} » est de type {start.FieldType}.");

        if (!string.IsNullOrWhiteSpace(calendar.EndFieldKey))
        {
            if (!fieldsByKey.TryGetValue(calendar.EndFieldKey, out var end))
                return Error.Validation("calendar", $"Champ de fin inconnu : « {calendar.EndFieldKey} ».");
            if (end.FieldType is not (CustomFieldType.Date or CustomFieldType.DateTime))
                return Error.Validation("calendar", $"La fin d'une vue Calendrier exige un champ Date/DateTime : « {calendar.EndFieldKey} » est de type {end.FieldType}.");
        }

        foreach (var key in new[] { calendar.TitleFieldKey, calendar.ColorFieldKey }.Where(k => !string.IsNullOrWhiteSpace(k)))
        {
            if (!fieldsByKey.ContainsKey(key!) && !IsPersistedKey(key!))
                return Error.Validation("calendar", $"Champ inconnu : « {key} ».");
        }
        return null;
    }

    private static Error? RequireKnownKey(
        string? fieldKey, IReadOnlyDictionary<string, CustomFieldDefinition> fieldsByKey,
        string errorField, out CustomFieldDefinition? field)
    {
        field = null;
        if (string.IsNullOrWhiteSpace(fieldKey))
            return Error.Validation(errorField, "Chaque référence exige une clé de champ.");
        if (fieldsByKey.TryGetValue(fieldKey, out var found))
        {
            field = found;
            return null;
        }
        if (IsPersistedKey(fieldKey))
            return null;
        return Error.Validation(errorField, $"Champ inconnu ou inactif : « {fieldKey} ».");
    }
}
