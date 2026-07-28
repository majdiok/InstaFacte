using System.Text.Json;
using System.Text.Json.Nodes;
using FactuTrust.Application.Features.Studio.Ai;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Features.Studio.Ai;

public sealed record ParsedSystemEntity(
    string Ref,
    string EntityDisplayName,
    string EntityDisplayNamePlural,
    string? Icon,
    string? Description,
    IReadOnlyList<ParsedSystemField> Fields,
    ParsedFormSpec? Form,
    ParsedAppReport? Report);

public sealed record ParsedSystemField(
    string Key,
    string Label,
    CustomFieldType FieldType,
    bool Required,
    bool Unique,
    IReadOnlyList<SelectOptionDto>? Options,
    Dictionary<string, JsonNode?>? Config,
    string? RelationToRef);

public sealed record ParsedFormSpec(
    IReadOnlyList<ParsedFormSection> Sections);

/// <summary>Width: <c>half</c> ou null (= full). LabelOverride: libellé d'affichage optionnel.</summary>
public sealed record ParsedFormFieldRef(
    string Key,
    string? Width,
    string? LabelOverride);

public sealed record ParsedFormSection(
    string? Title,
    IReadOnlyList<ParsedFormFieldRef> Fields);

public sealed record ParsedSeedBatch(
    string EntityRef,
    IReadOnlyList<Dictionary<string, JsonNode?>> Records);

public sealed record ParsedSystemSpec(
    string SystemDisplayName,
    string? SystemIcon,
    string? SystemDescription,
    IReadOnlyList<string>? OnboardingSteps,
    IReadOnlyList<ParsedSystemEntity> Entities,
    IReadOnlyList<ParsedSeedBatch> Seed);

/// <summary>
/// Parses multi-table system specs for <c>studio_generate_system</c>.
/// </summary>
public static class StudioAiSystemSpec
{
    public const int MaxEntities = 8;
    public const int MaxSeedRecords = 200;

    private static readonly HashSet<string> RelationTypeAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        "relation", "link", "lookup", "reference", "relationcustom", "foreign"
    };

    public static bool TryParse(string? specJson, out ParsedSystemSpec? spec, out string? error)
    {
        spec = null;
        error = null;
        if (string.IsNullOrWhiteSpace(specJson)) { error = "spec_json est vide."; return false; }

        JsonNode? root;
        try { root = JsonNode.Parse(specJson); }
        catch (JsonException) { error = "spec_json n'est pas un JSON valide."; return false; }

        var systemNode = root?["system"];
        var displayName = Str(systemNode?["displayName"]) ?? Str(root?["displayName"]) ?? Str(root?["name"]);
        if (string.IsNullOrWhiteSpace(displayName)) { error = "system.displayName est obligatoire."; return false; }

        var systemIcon = Str(systemNode?["icon"]);
        var systemDescription = Str(systemNode?["description"]);
        var onboarding = ParseOnboarding(systemNode?["onboarding"] ?? root?["onboarding"]);

        var entitiesArr = root?["entities"]?.AsArray();
        if (entitiesArr is null || entitiesArr.Count == 0) { error = "Au moins une entité est requise."; return false; }
        if (entitiesArr.Count > MaxEntities) { error = $"Maximum {MaxEntities} entités par système."; return false; }

        var entities = new List<ParsedSystemEntity>();
        var refs = new HashSet<string>(StringComparer.Ordinal);

        foreach (var en in entitiesArr)
        {
            if (entities.Count >= MaxEntities) break;
            var entity = ParseEntity(en, refs, out var entityError);
            if (entity is null) { error = entityError; return false; }
            entities.Add(entity);
        }

        var seed = ParseSeed(root?["seed"], refs, out var seedError);
        if (seedError is not null) { error = seedError; return false; }

        // Tolerant relations: a custom relation whose target isn't a spec ref (e.g. the model pointed at an
        // ERP/unknown table for "connecté à l'ERP") degrades to a plain Text field instead of failing the
        // WHOLE system. Existing-source relations (clients/products) were already finalized in ParseEntity.
        var resolved = entities.Select(e => e with
        {
            Fields = e.Fields.Select(f =>
                f.FieldType == CustomFieldType.RelationCustom && (f.RelationToRef is null || !refs.Contains(f.RelationToRef))
                    ? f with { FieldType = CustomFieldType.Text, Options = null, Config = null, RelationToRef = null }
                    : f).ToList()
        }).ToList();

        spec = new ParsedSystemSpec(displayName!.Trim(), systemIcon, systemDescription, onboarding, resolved, seed);
        return true;
    }

    /// <summary>
    /// Resolves a model-supplied <c>relationTo</c> to a whitelisted ERP existing source key (clients/products),
    /// tolerating common label variants (Produits, client, …). Returns null when it is not an ERP source.
    /// </summary>
    private static string? ResolveErpSource(string? relationTo)
    {
        if (string.IsNullOrWhiteSpace(relationTo)) return null;
        var slug = StudioAiAppSpec.SlugKey(relationTo);
        slug = slug switch
        {
            "client" or "clients" => "clients",
            "produit" or "produits" or "product" or "products" or "article" or "articles" => "products",
            _ => slug
        };
        return ExistingRelationSources.IsRelationTarget(slug) ? slug : null;
    }

    private static ParsedSystemEntity? ParseEntity(JsonNode? en, HashSet<string> refs, out string? error)
    {
        error = null;
        var refKey = Str(en?["ref"]) ?? Str(en?["key"]);
        var displayName = Str(en?["displayName"]) ?? Str(en?["name"]);
        if (string.IsNullOrWhiteSpace(displayName)) { error = "Chaque entité doit avoir displayName."; return null; }

        refKey = string.IsNullOrWhiteSpace(refKey)
            ? StudioAiAppSpec.SlugKey(displayName!)
            : StudioAiAppSpec.SlugKey(refKey);
        if (string.IsNullOrEmpty(refKey) || !StudioKey.IsValidShape(refKey))
            refKey = "table";
        var baseRef = refKey;
        var i = 1;
        while (refs.Contains(refKey)) refKey = $"{baseRef}_{++i}";
        refs.Add(refKey);

        var plural = Str(en?["displayNamePlural"]) ?? displayName!.Trim();
        var icon = Str(en?["icon"]);
        var description = Str(en?["description"]);

        var fieldsArr = en?["fields"]?.AsArray();
        if (fieldsArr is null || fieldsArr.Count == 0) { error = $"Entité « {displayName} » : au moins un champ requis."; return null; }

        var fields = new List<ParsedSystemField>();
        var usedKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var fn in fieldsArr)
        {
            if (fields.Count >= StudioAiAppSpec.MaxFields) break;
            var label = Str(fn?["label"]) ?? Str(fn?["name"]);
            if (string.IsNullOrWhiteSpace(label)) continue;

            var rawType = Str(fn?["type"]) ?? "text";
            var relationTo = Str(fn?["relationTo"]) ?? Str(fn?["relationToRef"]) ?? Str(fn?["targetRef"]);
            var key = UniqueFieldKey(label!, usedKeys);
            var required = Bool(fn?["required"]) ?? Bool(fn?["isRequired"]) ?? false;
            var unique = Bool(fn?["unique"]) ?? Bool(fn?["isUnique"]) ?? false;

            // Parse options up-front so a `select` that carries a stray `relationTo` stays a select.
            var parsedOptions = ParseOptions(fn?["options"]);
            var mappedType = StudioAiAppSpec.MapType(rawType);

            // A field is a real relation ONLY when its type is explicitly relational, OR it carries a
            // relationTo AND maps to plain Text (no concrete scalar/select type) AND has no options. This
            // stops the small model's spurious `relationTo` on select/date/money fields from turning them
            // into (broken) relations and discarding their options.
            var isRelation = RelationTypeAliases.Contains(rawType)
                || (!string.IsNullOrWhiteSpace(relationTo)
                    && mappedType == CustomFieldType.Text
                    && (parsedOptions is null || parsedOptions.Count == 0));

            // A relation pointing at a known ERP source (clients/products) becomes a real RelationExisting
            // link (honours "connecté à l'ERP"); otherwise it's a tentative custom relation to another spec
            // table — resolved tolerantly after all refs are known. Nothing here ever fails the spec.
            var erpSource = isRelation ? ResolveErpSource(relationTo) : null;
            var fieldType = !isRelation ? mappedType
                : erpSource is not null ? CustomFieldType.RelationExisting
                : CustomFieldType.RelationCustom;

            IReadOnlyList<SelectOptionDto>? options = null;
            Dictionary<string, JsonNode?>? config = null;
            if (fieldType is CustomFieldType.Select or CustomFieldType.MultiSelect)
            {
                options = parsedOptions;
                if (options is null || options.Count == 0)
                    fieldType = CustomFieldType.Text;
            }
            else if (fieldType is not (CustomFieldType.RelationCustom or CustomFieldType.RelationExisting))
            {
                config = ParseFieldConfig(fn, fieldType);
            }

            // A custom relation with no target → degrade to plain text rather than failing the whole system.
            if (fieldType == CustomFieldType.RelationCustom && string.IsNullOrWhiteSpace(relationTo))
                fieldType = CustomFieldType.Text;

            var relationRef = fieldType switch
            {
                CustomFieldType.RelationExisting => erpSource,
                CustomFieldType.RelationCustom => StudioAiAppSpec.SlugKey(relationTo!),
                _ => null
            };

            fields.Add(new ParsedSystemField(key, label!, fieldType, required, unique, options, config, relationRef));
        }

        if (fields.Count == 0) { error = $"Entité « {displayName} » : aucun champ valide."; return null; }

        var form = ParseForm(en?["form"], fields);
        var report = ParseEntityReport(en?["report"], fields);
        return new ParsedSystemEntity(refKey, displayName!.Trim(), plural.Trim(), icon, description, fields, form, report);
    }

    private static ParsedAppReport? ParseEntityReport(JsonNode? node, IReadOnlyList<ParsedSystemField> fields)
    {
        if (node is not JsonObject) return null;
        var wrapper = new JsonObject
        {
            ["entity"] = new JsonObject { ["displayName"] = "T" },
            ["fields"] = new JsonArray(fields.Select(f => new JsonObject { ["label"] = f.Label, ["type"] = "text" }).ToArray()),
            ["report"] = node.DeepClone()
        };
        return StudioAiAppSpec.TryParse(wrapper.ToJsonString(), out var appSpec, out _) ? appSpec?.Report : null;
    }

    private static ParsedFormSpec? ParseForm(JsonNode? node, IReadOnlyList<ParsedSystemField> fields)
    {
        if (node is not JsonObject) return null;
        var byKey = fields.ToDictionary(f => f.Key, StringComparer.Ordinal);
        var sections = new List<ParsedFormSection>();
        foreach (var sec in node["sections"]?.AsArray() ?? new JsonArray())
        {
            var refs = new List<ParsedFormFieldRef>();
            foreach (var fk in sec?["fields"]?.AsArray() ?? new JsonArray())
            {
                // Tolérant : entrée chaîne ("cle") OU objet ({"field","width","label"}). Un attribut de
                // mise en forme invalide dégrade (width → full, label → null), jamais d'échec du spec.
                string? raw;
                string? width = null;
                string? labelOverride = null;
                if (fk is JsonObject fo)
                {
                    raw = Str(fo["field"]) ?? Str(fo["key"]) ?? Str(fo["name"]);
                    width = NormalizeFormWidth(Str(fo["width"]));
                    labelOverride = Str(fo["label"]) ?? Str(fo["labelOverride"]);
                }
                else
                {
                    raw = Str(fk);
                }
                if (string.IsNullOrWhiteSpace(raw)) continue;
                var resolved = ResolveFieldKey(raw, fields);
                if (resolved is not null && byKey.ContainsKey(resolved))
                    refs.Add(new ParsedFormFieldRef(resolved, width,
                        string.IsNullOrWhiteSpace(labelOverride) ? null : labelOverride!.Trim()));
            }
            if (refs.Count > 0)
                sections.Add(new ParsedFormSection(Str(sec?["title"]), refs));
        }
        return sections.Count > 0 ? new ParsedFormSpec(sections) : null;
    }

    private static string? NormalizeFormWidth(string? raw)
    {
        var w = raw?.Trim().ToLowerInvariant();
        return w is "half" or "demi" or "moitie" or "moitié" or "1/2" or "50%" ? "half" : null;
    }

    private static IReadOnlyList<ParsedSeedBatch> ParseSeed(JsonNode? node, HashSet<string> refs, out string? error)
    {
        error = null;
        var result = new List<ParsedSeedBatch>();
        if (node is not JsonArray arr) return result;

        var total = 0;
        foreach (var batch in arr)
        {
            var entityRef = Str(batch?["entityRef"]) ?? Str(batch?["entity"]);
            if (string.IsNullOrWhiteSpace(entityRef)) { error = "seed.entityRef est obligatoire."; return result; }
            entityRef = StudioAiAppSpec.SlugKey(entityRef);
            if (!refs.Contains(entityRef)) { error = $"seed : entité « {entityRef} » inconnue."; return result; }

            var recordsArr = batch?["records"]?.AsArray();
            if (recordsArr is null || recordsArr.Count == 0) continue;

            var records = new List<Dictionary<string, JsonNode?>>();
            foreach (var rec in recordsArr)
            {
                if (total >= MaxSeedRecords) break;
                if (rec is not JsonObject obj) continue;
                var dict = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
                foreach (var prop in obj)
                    dict[prop.Key] = prop.Value?.DeepClone();
                records.Add(dict);
                total++;
            }
            if (records.Count > 0)
                result.Add(new ParsedSeedBatch(entityRef, records));
        }
        return result;
    }

    private static IReadOnlyList<string>? ParseOnboarding(JsonNode? node)
    {
        if (node is not JsonArray arr) return null;
        var steps = arr.Select(Str).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()).ToList();
        return steps.Count > 0 ? steps : null;
    }

    private static string? ResolveFieldKey(string raw, IReadOnlyList<ParsedSystemField> fields)
    {
        if (fields.Any(f => f.Key == raw)) return raw;
        var slug = StudioAiAppSpec.SlugKey(raw);
        return fields.FirstOrDefault(f => f.Key == slug)?.Key
            ?? fields.FirstOrDefault(f => StudioAiAppSpec.SlugKey(f.Label) == slug)?.Key;
    }

    private static string UniqueFieldKey(string label, HashSet<string> used)
    {
        var baseKey = StudioAiAppSpec.SlugKey(label);
        if (string.IsNullOrEmpty(baseKey) || !StudioKey.IsValidShape(baseKey) || StudioKey.IsReservedFieldKey(baseKey))
            baseKey = "champ";
        var key = baseKey;
        var i = 1;
        while (used.Contains(key)) key = $"{baseKey}_{++i}";
        used.Add(key);
        return key;
    }

    private static IReadOnlyList<SelectOptionDto>? ParseOptions(JsonNode? node)
    {
        if (node is not JsonArray arr) return null;
        var result = new List<SelectOptionDto>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in arr)
        {
            string? value;
            string? label;
            if (item is JsonObject obj)
            {
                value = Str(obj["value"]) ?? Str(obj["label"]);
                label = Str(obj["label"]) ?? value;
            }
            else
            {
                value = Str(item);
                label = value;
            }
            if (string.IsNullOrWhiteSpace(value)) continue;
            var slug = StudioAiAppSpec.SlugKey(value);
            if (string.IsNullOrEmpty(slug)) slug = value!.Trim();
            if (!seen.Add(slug)) continue;
            result.Add(new SelectOptionDto(slug, (label ?? value)!.Trim()));
        }
        return result.Count > 0 ? result : null;
    }

    private static Dictionary<string, JsonNode?>? ParseFieldConfig(JsonNode? fn, CustomFieldType type)
    {
        var cfg = fn?["config"] as JsonObject;
        switch (type)
        {
            case CustomFieldType.Money:
            {
                var currency = Str(cfg?["currency"]) ?? Str(fn?["currency"]) ?? "TND";
                return new() { ["currency"] = JsonValue.Create(currency.Trim().ToUpperInvariant()) };
            }
            case CustomFieldType.Rating:
            {
                var max = Int(cfg?["max"]) ?? Int(fn?["max"]) ?? 5;
                return new() { ["max"] = JsonValue.Create(Math.Clamp(max, 1, 10)) };
            }
            default:
                return null;
        }
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

    private static int? Int(JsonNode? n)
    {
        if (n is not JsonValue v) return null;
        if (v.TryGetValue<int>(out var i)) return i;
        return int.TryParse(Str(n), out var p) ? p : null;
    }
}
