using System.Text.Json;
using System.Text.Json.Nodes;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Application.Features.Studio.RecordViews;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Features.Studio.Ai;

/// <summary>
/// Spec d'une vue enregistrée proposée par l'IA (PR 2.4 — outil <c>studio_plan_record_view</c> et
/// <c>entities[].views[]</c> des specs système). Les clés de champ restent TELLES QUE LE MODÈLE LES A
/// ÉCRITES au parsing : elles ne sont résolues qu'à la création, contre le schéma réel
/// (<see cref="StudioAiRecordViewSpec.ResolveAgainstSchema"/>).
/// </summary>
public sealed record ParsedRecordViewSpec(
    /// <summary>Clé de la table cible ; null dans une spec système (entité implicite).</summary>
    string? EntityKey,
    /// <summary>Libellé de la vue (≤ 80 caractères, obligatoire).</summary>
    string DisplayName,
    /// <summary>Mode normalisé : <c>list</c> | <c>kanban</c> | <c>calendar</c>.</summary>
    string Mode,
    /// <summary>Clés de champ demandées (la borne 25 est appliquée à la résolution, avec avertissement).</summary>
    IReadOnlyList<string> Columns,
    /// <summary>Filtres demandés (borne 10 à la résolution) ; op normalisé en minuscules.</summary>
    IReadOnlyList<RecordViewFilter> Filters,
    /// <summary>Tris demandés (borne 3 à la résolution).</summary>
    IReadOnlyList<RecordViewSort> Sort,
    string? GroupByFieldKey,
    string? StartFieldKey,
    string? EndFieldKey,
    string? TitleFieldKey,
    bool IsDefault);

/// <summary>
/// Parse et résout les specs de vues enregistrées émises par le modèle. Tolérant (alias FR/EN de
/// clés et de modes) mais JAMAIS silencieux : toute dégradation à la résolution produit un
/// avertissement en clair (exigence « pas d'échec silencieux » du programme Studio IA).
/// </summary>
public static class StudioAiRecordViewSpec
{
    public const int MaxColumns = RecordViewDefinitionValidator.MaxColumns;   // 25
    public const int MaxFilters = RecordViewDefinitionValidator.MaxFilters;   // 10
    public const int MaxSort = RecordViewDefinitionValidator.MaxSort;         // 3

    /// <summary>Colonnes retenues par défaut quand la spec n'en propose aucune d'exploitable.</summary>
    private const int DefaultColumnCount = 6;

    private const int MaxDisplayNameLength = 80;

    /// <summary>Parse une spec JSON complète (outil <c>studio_plan_record_view</c>).</summary>
    public static bool TryParse(string? specJson, out ParsedRecordViewSpec? spec, out string? error)
    {
        spec = null;
        error = null;
        if (string.IsNullOrWhiteSpace(specJson)) { error = "spec_json est vide."; return false; }

        JsonNode? root;
        try { root = JsonNode.Parse(specJson); }
        catch (JsonException) { error = "spec_json n'est pas un JSON valide."; return false; }

        return TryParseNode(root, out spec, out error);
    }

    /// <summary>Parse un nœud <c>views[]</c> d'une spec système (réutilisé par StudioAiSystemSpec).</summary>
    public static bool TryParseNode(JsonNode? node, out ParsedRecordViewSpec? spec, out string? error)
    {
        spec = null;
        error = null;
        if (node is not JsonObject obj)
        {
            error = "Une vue doit être un objet JSON.";
            return false;
        }

        var displayName = Str(obj["name"]) ?? Str(obj["displayName"]) ?? Str(obj["nom"]);
        if (string.IsNullOrWhiteSpace(displayName))
        {
            error = "name est obligatoire (libellé de la vue).";
            return false;
        }
        displayName = displayName!.Trim();
        if (displayName.Length > MaxDisplayNameLength) displayName = displayName[..MaxDisplayNameLength];

        var rawMode = Str(obj["mode"]);
        var mode = NormalizeMode(rawMode);
        if (mode is null)
        {
            error = $"Mode de vue inconnu : « {rawMode} » (attendu : list, kanban ou calendar).";
            return false;
        }

        // La clé de table est normalisée comme les refs de spec (le modèle écrit souvent le libellé) ;
        // son existence est revérifiée contre le schéma réel à l'aperçu et à l'exécution.
        var entityRaw = Str(obj["entity"]) ?? Str(obj["entityKey"]) ?? Str(obj["table"]);
        string? entityKey = null;
        if (!string.IsNullOrWhiteSpace(entityRaw))
        {
            var slug = StudioAiAppSpec.SlugKey(entityRaw);
            entityKey = string.IsNullOrEmpty(slug) ? entityRaw!.Trim() : slug;
        }

        var columns = ParseColumns(obj["columns"] ?? obj["colonnes"]);
        var filters = ParseFilters(obj["filters"] ?? obj["filtres"]);
        var sort = ParseSort(obj["sort"] ?? obj["tri"]);

        var groupBy = Str(obj["groupBy"]) ?? Str(obj["groupe"]);
        var start = Str(obj["start"]) ?? Str(obj["debut"]);
        var end = Str(obj["end"]) ?? Str(obj["fin"]);
        var title = Str(obj["title"]) ?? Str(obj["titre"]);
        var isDefault = Bool(obj["isDefault"]) ?? Bool(obj["default"]) ?? Bool(obj["parDefaut"]) ?? false;

        spec = new ParsedRecordViewSpec(
            entityKey, displayName, mode, columns, filters, sort,
            groupBy?.Trim(), start?.Trim(), end?.Trim(), title?.Trim(), isDefault);
        return true;
    }

    /// <summary>
    /// Confronte la spec au schéma RÉEL de la table et produit la définition à enregistrer :
    /// colonnes/filtres/tris inconnus retirés (avertissement), kanban sans champ Select ou calendrier
    /// sans champ date dégradés en Liste (avertissement), validation finale par
    /// <see cref="RecordViewDefinitionValidator"/> (repli Liste + avertissement si encore invalide).
    /// </summary>
    public static (CustomRecordViewMode Mode, RecordViewDefinition Definition, IReadOnlyList<string> Warnings)
        ResolveAgainstSchema(ParsedRecordViewSpec spec, IReadOnlyList<CustomFieldDto> fields)
    {
        var warnings = new List<string>();
        var active = fields.Where(f => f.IsActive).ToList();
        // Projection en entités domaine : la validation finale réutilise le validateur des vues
        // (mêmes règles à l'enregistrement humain et à la proposition IA).
        var definitions = active
            .Select(f => CustomFieldDefinition.Create(
                Guid.Empty, Guid.Empty, f.Key, f.Label, f.FieldType, f.IsRequired, f.IsUnique, f.SortOrder,
                null, StudioFieldJson.SerializeOptions(f.Options), null, null))
            .ToList();
        var byKey = definitions.ToDictionary(f => f.Key, StringComparer.Ordinal);

        // ---- Colonnes ----
        var columns = new List<string>();
        var seenColumns = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in spec.Columns)
        {
            var key = ResolveFieldKey(raw, active);
            if (key is null)
            {
                warnings.Add($"Colonne « {raw} » inconnue, ignorée.");
                continue;
            }
            if (seenColumns.Add(key))
                columns.Add(key);
        }
        if (spec.Columns.Count > 0 && columns.Count > MaxColumns)
        {
            warnings.Add($"Au-delà de {MaxColumns} colonnes, les suivantes ont été ignorées.");
            columns = columns.Take(MaxColumns).ToList();
        }
        if (columns.Count == 0)
        {
            // Choix par défaut (spec muette) ou dégradation (toutes inconnues — déjà averti) :
            // les 6 premiers champs actifs de la table.
            columns = active.Take(DefaultColumnCount).Select(f => f.Key).ToList();
        }

        // ---- Filtres (chaque filtre revérifié par le validateur, valeur mal formée incluse) ----
        var filters = new List<RecordViewFilter>();
        foreach (var filter in spec.Filters)
        {
            if (filters.Count >= MaxFilters)
            {
                warnings.Add($"Au-delà de {MaxFilters} filtres, les suivants ont été ignorés.");
                break;
            }
            var key = ResolveFieldKey(filter.FieldKey, active);
            if (key is null)
            {
                warnings.Add($"Filtre sur « {filter.FieldKey} » ignoré : champ inconnu.");
                continue;
            }
            if (!byKey.TryGetValue(key, out var field))
            {
                // createdAt / updatedAt : colonnables et triables mais pas filtrables (hors DataJson).
                warnings.Add($"Filtre sur « {key} » ignoré : champ non filtrable.");
                continue;
            }
            var op = (filter.Op ?? string.Empty).Trim().ToLowerInvariant();
            if (!RecordViewDefinitionValidator.Operators.Contains(op))
            {
                warnings.Add($"Filtre sur « {key} » ignoré : opérateur « {filter.Op} » inconnu.");
                continue;
            }
            if (!RecordViewDefinitionValidator.IsOperatorCompatible(op, field.FieldType))
            {
                warnings.Add($"Filtre sur « {key} » ignoré : opérateur « {op} » incompatible avec le type {field.FieldType}.");
                continue;
            }
            var candidate = new RecordViewFilter(key, op, filter.Value);
            if (RecordViewDefinitionValidator.ValidateFilters(new[] { candidate }, byKey) is { } filterError)
            {
                warnings.Add($"Filtre sur « {key} » ignoré : {filterError.Description}");
                continue;
            }
            filters.Add(candidate);
        }

        // ---- Tri ----
        var sort = new List<RecordViewSort>();
        foreach (var s in spec.Sort)
        {
            if (sort.Count >= MaxSort)
            {
                warnings.Add($"Au-delà de {MaxSort} critères de tri, les suivants ont été ignorés.");
                break;
            }
            var key = ResolveFieldKey(s.FieldKey, active);
            if (key is null)
            {
                warnings.Add($"Tri sur « {s.FieldKey} » ignoré : champ inconnu.");
                continue;
            }
            if (byKey.TryGetValue(key, out var sortField) && CustomRecordValidator.IsComputed(sortField.FieldType))
            {
                warnings.Add($"Tri sur « {key} » ignoré : champ calculé non triable.");
                continue;
            }
            sort.Add(new RecordViewSort(key, s.Descending));
        }

        // ---- Mode (kanban / calendrier dégradés en liste si le schéma ne les supporte pas) ----
        var mode = ModeFromKey(spec.Mode);
        RecordViewKanban? kanban = null;
        RecordViewCalendar? calendar = null;
        switch (mode)
        {
            case CustomRecordViewMode.Kanban:
            {
                var groupBy = spec.GroupByFieldKey is null ? null : ResolveFieldKey(spec.GroupByFieldKey, active);
                var groupField = groupBy is not null && byKey.TryGetValue(groupBy, out var g) ? g : null;
                var hasOptions = groupField is not null
                    && StudioFieldJson.ParseOptions(groupField.OptionsJson) is { Count: > 0 };
                if (groupField is null || groupField.FieldType != CustomFieldType.Select || !hasOptions)
                {
                    warnings.Add(spec.GroupByFieldKey is null
                        ? "Kanban impossible : aucun champ de regroupement indiqué ; vue Liste proposée."
                        : $"Kanban impossible : « {spec.GroupByFieldKey} » n'est pas une liste de choix ; vue Liste proposée.");
                    mode = CustomRecordViewMode.List;
                }
                else
                {
                    var titleKey = ResolveOptionalKey(spec.TitleFieldKey, active, warnings, "Titre de carte");
                    kanban = new RecordViewKanban(groupBy!, titleKey,
                        columns.Take(4).ToList(), ColumnOrder: null, ShowEmptyGroup: true);
                }
                break;
            }
            case CustomRecordViewMode.Calendar:
            {
                var start = spec.StartFieldKey is null ? null : ResolveFieldKey(spec.StartFieldKey, active);
                var startField = start is not null && byKey.TryGetValue(start, out var s) ? s : null;
                if (startField is null || startField.FieldType is not (CustomFieldType.Date or CustomFieldType.DateTime))
                {
                    warnings.Add("Calendrier impossible : aucun champ date valide ; vue Liste proposée.");
                    mode = CustomRecordViewMode.List;
                }
                else
                {
                    string? endKey = null;
                    if (!string.IsNullOrWhiteSpace(spec.EndFieldKey))
                    {
                        var end = ResolveFieldKey(spec.EndFieldKey, active);
                        var endField = end is not null && byKey.TryGetValue(end, out var e) ? e : null;
                        if (endField is null || endField.FieldType is not (CustomFieldType.Date or CustomFieldType.DateTime))
                            warnings.Add($"Champ de fin « {spec.EndFieldKey} » ignoré : un calendrier exige un champ date.");
                        else
                            endKey = end;
                    }
                    var titleKey = ResolveOptionalKey(spec.TitleFieldKey, active, warnings, "Titre d'événement");
                    calendar = new RecordViewCalendar(start!, endKey, titleKey, ColorFieldKey: null);
                }
                break;
            }
        }

        var definition = new RecordViewDefinition(
            columns.Select(c => new RecordViewColumn(c)).ToList(),
            filters, sort, kanban, calendar, SearchEnabled: true, PageSize: 25);

        // Filet final : le MÊME validateur qu'à l'enregistrement manuel. Tout refus dégrade en une
        // liste simple sur les colonnes par défaut (jamais d'échec pour une proposition d'IA).
        if (RecordViewDefinitionValidator.Validate(definition, mode, definitions).IsFailure)
        {
            warnings.Add("Définition trop complexe après résolution ; vue Liste simplifiée proposée.");
            mode = CustomRecordViewMode.List;
            definition = new RecordViewDefinition(
                active.Take(DefaultColumnCount).Select(f => new RecordViewColumn(f.Key)).ToList(),
                Array.Empty<RecordViewFilter>(), Array.Empty<RecordViewSort>(), null, null,
                SearchEnabled: true, PageSize: 25);
        }

        return (mode, definition, warnings);
    }

    /// <summary>
    /// Clé machine d'une vue proposée : <c>vue_&lt;slug du libellé&gt;</c> tronquée à 64 caractères,
    /// suffixée <c>_2</c>…<c>_99</c> en cas de collision avec les vues existantes.
    /// </summary>
    public static string SlugKey(string displayName, IReadOnlySet<string> usedKeys)
    {
        var slug = StudioAiAppSpec.SlugKey(StudioAiAppSpec.RemoveDiacritics(displayName));
        var baseKey = string.IsNullOrEmpty(slug) ? "vue" : $"vue_{slug}";
        if (baseKey.Length > StudioKey.MaxLength) baseKey = baseKey[..StudioKey.MaxLength].TrimEnd('_');

        var key = baseKey;
        for (var i = 2; usedKeys.Contains(key) && i <= 99; i++)
        {
            var suffix = $"_{i}";
            key = baseKey.Length + suffix.Length > StudioKey.MaxLength
                ? baseKey[..(StudioKey.MaxLength - suffix.Length)].TrimEnd('_') + suffix
                : baseKey + suffix;
        }
        return key;
    }

    /// <summary>Clé textuelle d'un mode résolu (pour reconstruire une spec ajustée après résolution).</summary>
    public static string ModeKey(CustomRecordViewMode mode) => mode switch
    {
        CustomRecordViewMode.Kanban => "kanban",
        CustomRecordViewMode.Calendar => "calendar",
        _ => "list"
    };

    /// <summary>Libellé d'aperçu d'un mode de spec (« Liste » / « Kanban » / « Calendrier »).</summary>
    public static string ModeLabel(string mode) => mode switch
    {
        "kanban" => "Kanban",
        "calendar" => "Calendrier",
        _ => "Liste"
    };

    private static CustomRecordViewMode ModeFromKey(string mode) => mode switch
    {
        "kanban" => CustomRecordViewMode.Kanban,
        "calendar" => CustomRecordViewMode.Calendar,
        _ => CustomRecordViewMode.List
    };

    /// <summary>null = inconnu (erreur) ; absent du JSON = liste par défaut.</summary>
    private static string? NormalizeMode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "list";
        return raw.Trim().ToLowerInvariant() switch
        {
            "liste" or "list" or "table" => "list",
            "kanban" or "tableau" or "board" => "kanban",
            "calendrier" or "calendar" or "planning" or "agenda" => "calendar",
            _ => null
        };
    }

    /// <summary>
    /// Résolution tolérante d'une clé écrite par le modèle : clé exacte, puis insensible à la casse,
    /// puis slug de la saisie contre clés et libellés. Les clés persistées
    /// (<c>createdAt</c>/<c>updatedAt</c>) sont reconnues. null = inconnue.
    /// </summary>
    private static string? ResolveFieldKey(string raw, IReadOnlyList<CustomFieldDto> fields)
    {
        var trimmed = raw.Trim();
        if (fields.Any(f => f.Key == trimmed)) return trimmed;
        if (RecordViewDefinitionValidator.IsPersistedKey(trimmed)) return trimmed;
        var insensitive = fields.FirstOrDefault(f => string.Equals(f.Key, trimmed, StringComparison.OrdinalIgnoreCase));
        if (insensitive is not null) return insensitive.Key;
        var slug = StudioAiAppSpec.SlugKey(trimmed);
        if (slug.Length > 0)
        {
            var bySlug = fields.FirstOrDefault(f => f.Key == slug);
            if (bySlug is not null) return bySlug.Key;
            var byLabel = fields.FirstOrDefault(f => StudioAiAppSpec.SlugKey(f.Label) == slug);
            if (byLabel is not null) return byLabel.Key;
        }
        return null;
    }

    /// <summary>Résout une clé optionnelle (titre de carte/événement) ; inconnue ⇒ null + avertissement.</summary>
    private static string? ResolveOptionalKey(
        string? raw, IReadOnlyList<CustomFieldDto> fields, List<string> warnings, string role)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var key = ResolveFieldKey(raw, fields);
        if (key is null)
            warnings.Add($"{role} « {raw} » inconnu, ignoré.");
        return key;
    }

    private static IReadOnlyList<string> ParseColumns(JsonNode? node)
    {
        var columns = new List<string>();
        if (node is not JsonArray arr) return columns;
        foreach (var item in arr)
        {
            var raw = item is JsonObject obj
                ? Str(obj["field"]) ?? Str(obj["fieldKey"]) ?? Str(obj["key"]) ?? Str(obj["name"])
                : Str(item);
            if (string.IsNullOrWhiteSpace(raw)) continue;
            columns.Add(raw!.Trim());
        }
        return columns;
    }

    private static IReadOnlyList<RecordViewFilter> ParseFilters(JsonNode? node)
    {
        var filters = new List<RecordViewFilter>();
        if (node is not JsonArray arr) return filters;
        foreach (var item in arr)
        {
            if (item is not JsonObject obj) continue;
            var field = Str(obj["field"]) ?? Str(obj["fieldKey"]) ?? Str(obj["key"]);
            var op = Str(obj["op"]);
            if (string.IsNullOrWhiteSpace(field) || string.IsNullOrWhiteSpace(op)) continue;
            filters.Add(new RecordViewFilter(field!.Trim(), op!.Trim().ToLowerInvariant(), obj["value"]?.DeepClone()));
        }
        return filters;
    }

    private static IReadOnlyList<RecordViewSort> ParseSort(JsonNode? node)
    {
        var sort = new List<RecordViewSort>();
        if (node is not JsonArray arr) return sort;
        foreach (var item in arr)
        {
            if (item is JsonObject obj)
            {
                var field = Str(obj["field"]) ?? Str(obj["fieldKey"]) ?? Str(obj["key"]);
                if (string.IsNullOrWhiteSpace(field)) continue;
                var descending = Bool(obj["desc"]) ?? Bool(obj["descending"]) ?? false;
                sort.Add(new RecordViewSort(field!.Trim(), descending));
            }
            else
            {
                var raw = Str(item);
                if (!string.IsNullOrWhiteSpace(raw))
                    sort.Add(new RecordViewSort(raw!.Trim(), false));
            }
        }
        return sort;
    }

    private static string? Str(JsonNode? n)
    {
        if (n is not JsonValue v) return null;
        if (v.TryGetValue<string>(out var s)) return string.IsNullOrWhiteSpace(s) ? null : s;
        return v.ToString();
    }

    private static bool? Bool(JsonNode? n)
    {
        if (n is not JsonValue v) return null;
        if (v.TryGetValue<bool>(out var b)) return b;
        var s = Str(n)?.Trim().ToLowerInvariant();
        return s switch { "true" or "oui" or "yes" or "1" => true, "false" or "non" or "no" or "0" => false, _ => (bool?)null };
    }
}
