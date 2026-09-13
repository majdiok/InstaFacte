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
    ParsedAppReport? Report,
    /// <summary>Clé d'une table EXISTANTE à réutiliser telle quelle (champs/formulaire/état ignorés).</summary>
    string? ExistingKey = null);

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

/// <summary>
/// Relation plusieurs-à-plusieurs déclarée dans <c>relations[]</c> d'une spec système (PR 2.2), OU
/// promue depuis un champ d'entité de type <c>many_to_many</c>/<c>n_n</c> porteur d'un
/// <c>relationTo</c> valide. <see cref="Kind"/> vaut toujours <c>"many_to_many"</c> après
/// normalisation (seule nature acceptée pour l'instant).
/// </summary>
public sealed record ParsedSystemRelation(
    string Kind,
    /// <summary>Ref d'entité (slug) de la table source — doit exister parmi les refs de la spec.</summary>
    string FromRef,
    /// <summary>Ref d'entité (slug) de la table cible — doit exister et différer de <see cref="FromRef"/>.</summary>
    string ToRef,
    /// <summary>Libellé de la relation (≤ 80 caractères), affiché sur le champ de jonction correspondant.</summary>
    string? Label,
    /// <summary>Libellé de la table de jonction ; la clé est dérivée par le backend si absente.</summary>
    string? JunctionName);

public sealed record ParsedSystemSpec(
    string SystemDisplayName,
    string? SystemIcon,
    string? SystemDescription,
    IReadOnlyList<string>? OnboardingSteps,
    IReadOnlyList<ParsedSystemEntity> Entities,
    IReadOnlyList<ParsedSeedBatch> Seed,
    /// <summary>Avertissements de parsing (réutilisations ignorées, champs écartés) — additif, R6.</summary>
    IReadOnlyList<string>? Warnings = null,
    /// <summary>Relations plusieurs-à-plusieurs déclarées (explicites ou promues) — additif, PR 2.2.</summary>
    IReadOnlyList<ParsedSystemRelation>? Relations = null)
{
    public IReadOnlyList<ParsedSystemRelation> Relations { get; init; } = Relations ?? Array.Empty<ParsedSystemRelation>();
}

/// <summary>
/// Parses multi-table system specs for <c>studio_generate_system</c>.
/// </summary>
public static class StudioAiSystemSpec
{
    /// <summary>Borne de tables NOUVELLES par système (les tables réutilisées ne comptent pas).</summary>
    public const int MaxEntities = 8;
    /// <summary>Borne de tables existantes réutilisées par système (<c>existingKey</c>).</summary>
    public const int MaxExistingRefs = 8;
    public const int MaxSeedRecords = 200;
    /// <summary>Borne de relations plusieurs-à-plusieurs (explicites + promues) par système.</summary>
    public const int MaxRelations = 6;

    private static readonly HashSet<string> RelationTypeAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        "relation", "link", "lookup", "reference", "relationcustom", "foreign"
    };

    /// <summary>Alias reconnus pour <c>relations[].kind</c> — tous normalisés en <c>"many_to_many"</c>.</summary>
    private static readonly HashSet<string> ManyToManyAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        "many_to_many", "manytomany", "many-to-many", "n_n", "nn", "n-n", "plusieurs_a_plusieurs", "m2m"
    };

    /// <summary>
    /// Alias reconnus pour <c>fields[].type</c> qui PROMEUT un champ en relation N‑N plutôt que de le
    /// créer : jeu volontairement restreint (le champ « relation » ordinaire reste RelationCustom).
    /// </summary>
    private static readonly HashSet<string> ManyToManyFieldTypeAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        "many_to_many", "manytomany", "n_n", "nn"
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

        var entities = new List<ParsedSystemEntity>();
        var refs = new HashSet<string>(StringComparer.Ordinal);
        var warnings = new List<string>();
        var promotedRelations = new List<ParsedSystemRelation>();
        var newCount = 0;
        var reusedCount = 0;

        foreach (var en in entitiesArr)
        {
            var entity = ParseEntity(en, refs, warnings, promotedRelations, out var entityError);
            if (entity is null) { error = entityError; return false; }

            // La borne MaxEntities ne vise que les tables NOUVELLES : une table réutilisée n'est ni
            // créée ni comptée dans le quota. Au-delà de MaxExistingRefs, la réutilisation est
            // ignorée avec avertissement (la spec reste exécutable), jamais un rejet franc.
            if (entity.ExistingKey is not null)
            {
                if (reusedCount >= MaxExistingRefs)
                {
                    refs.Remove(entity.Ref);
                    warnings.Add($"Entité « {entity.EntityDisplayName} » ignorée : au plus {MaxExistingRefs} tables existantes réutilisées par système.");
                    continue;
                }
                reusedCount++;
            }
            else
            {
                if (newCount >= MaxEntities) { error = $"Maximum {MaxEntities} entités par système."; return false; }
                newCount++;
            }
            entities.Add(entity);
        }

        var seed = ParseSeed(root?["seed"], refs, out var seedError);
        if (seedError is not null) { error = seedError; return false; }

        // Relations plusieurs-à-plusieurs (PR 2.2) : explicites (relations[]) + promues depuis un champ
        // type "many_to_many"/"n_n" — dédoublonnées par paire, bornées à MaxRelations, jamais un rejet
        // franc (une relation invalide dégrade en avertissement, comme le reste du parseur).
        var relations = ParseRelations(root?["relations"], refs, promotedRelations, warnings);

        // D5 : les workflows ne sont pas encore pris en charge par la génération de système — avertir
        // plutôt qu'échouer ou ignorer silencieusement (l'utilisateur doit savoir qu'il faut redemander).
        if (root?["workflows"] is JsonArray workflowsArr && workflowsArr.Count > 0)
            warnings.Add("Les workflows sont proposés séparément : demandez-les après la création du système.");

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

        spec = new ParsedSystemSpec(displayName!.Trim(), systemIcon, systemDescription, onboarding, resolved, seed, warnings, relations);
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

    private static ParsedSystemEntity? ParseEntity(
        JsonNode? en, HashSet<string> refs, List<string> warnings,
        List<ParsedSystemRelation> promotedRelations, out string? error)
    {
        error = null;
        var refKey = Str(en?["ref"]) ?? Str(en?["key"]);
        var displayName = Str(en?["displayName"]) ?? Str(en?["name"]);

        // Réutilisation d'une table existante : « existingKey » (alias existing/useExisting/reuse).
        // Chaîne → clé explicite ; booléen true → la clé est celle de l'entité (ref/key/displayName).
        // L'existence et l'état actif de la table sont revérifiés à l'exécution, jamais ici.
        var existingNode = en?["existingKey"] ?? en?["existing"] ?? en?["useExisting"] ?? en?["reuse"];
        var reuseFlag = Bool(existingNode);
        if (reuseFlag == false)
            existingNode = null; // « reuse: false » = pas de réutilisation
        string? existingKey = null;
        if (existingNode is not null && reuseFlag != true)
        {
            var rawExisting = Str(existingNode);
            var candidate = rawExisting is null ? null : StudioAiAppSpec.SlugKey(rawExisting);
            if (string.IsNullOrEmpty(candidate) || !StudioKey.IsValidShape(candidate))
            {
                error = $"Entité « {displayName ?? refKey ?? "?"} » : « existingKey » n'est pas une clé de table valide.";
                return null;
            }
            existingKey = candidate;
        }

        if (existingKey is not null || existingNode is not null)
        {
            // La ref interne (relations/seed) retombe sur la clé réutilisée, puis sur le libellé.
            // Le repli d'existingKey se fait sur la ref AVANT déduplication (la clé réutilisée reste
            // celle demandée, jamais une forme suffixée « _2 »).
            var baseRef = SlugRef(refKey, existingKey ?? displayName ?? "table");
            existingKey ??= baseRef;
            displayName ??= existingKey;
            refKey = UniqueRef(baseRef, refs);

            // La table n'est NI créée NI modifiée : champs/formulaire/état éventuels sont ignorés.
            if (en?["fields"] is not null || en?["form"] is not null || en?["report"] is not null)
                warnings.Add($"Entité « {displayName} » : champs, formulaire et état ignorés — la table existante « {existingKey} » est réutilisée telle quelle.");

            return new ParsedSystemEntity(refKey, displayName.Trim(), displayName.Trim(), null, null,
                Array.Empty<ParsedSystemField>(), null, null, existingKey);
        }

        if (string.IsNullOrWhiteSpace(displayName)) { error = "Chaque entité doit avoir displayName."; return null; }

        refKey = UniqueRef(SlugRef(refKey, displayName!), refs);

        var plural = Str(en?["displayNamePlural"]) ?? displayName!.Trim();
        var icon = Str(en?["icon"]);
        var description = Str(en?["description"]);

        var fieldsArr = en?["fields"]?.AsArray();
        if (fieldsArr is null || fieldsArr.Count == 0) { error = $"Entité « {displayName} » : au moins un champ requis."; return null; }

        // Rejet franc (comme les entités et le seed) : tronquer silencieusement induirait l'utilisateur
        // en erreur dans l'aperçu éditable (« 41 champs proposés, 40 créés »).
        if (fieldsArr.Count > StudioAiAppSpec.MaxFields) { error = $"Entité « {displayName} » : au plus {StudioAiAppSpec.MaxFields} champs."; return null; }

        var fields = new List<ParsedSystemField>();
        var usedKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var fn in fieldsArr)
        {
            var label = Str(fn?["label"]) ?? Str(fn?["name"]);
            if (string.IsNullOrWhiteSpace(label)) continue;

            var rawType = Str(fn?["type"]) ?? "text";
            var relationTo = Str(fn?["relationTo"]) ?? Str(fn?["relationToRef"]) ?? Str(fn?["targetRef"]);

            // Promotion de champ (PR 2.2) : un champ typé many_to_many/n_n avec un relationTo n'est PAS
            // créé — il devient une relation N‑N (résolue et dédoublonnée plus tard par ParseRelations).
            // Sans relationTo, ce n'est qu'un type inconnu ordinaire (dégradé en Text ci-dessous).
            if (ManyToManyFieldTypeAliases.Contains(rawType.Trim()) && !string.IsNullOrWhiteSpace(relationTo))
            {
                promotedRelations.Add(new ParsedSystemRelation("many_to_many", refKey!, relationTo!, label, null));
                continue;
            }

            // Clé explicite (éditeur d'aperçu « Personnaliser ») prioritaire sur la dérivation du
            // libellé : renommer un libellé ne doit jamais casser les références formulaire/rapport.
            // Une clé explicite invalide, réservée ou dupliquée rejette la spec (rejet franc).
            var explicitKey = Str(fn?["key"]);
            string key;
            if (!string.IsNullOrWhiteSpace(explicitKey))
            {
                key = StudioKey.Slugify(explicitKey!);
                if (StudioKey.IsReservedFieldKey(key) || !StudioKey.IsValidShape(key))
                { error = $"Clé de champ « {key} » invalide ou réservée."; return null; }
                if (!usedKeys.Add(key)) { error = $"Clé de champ « {key} » dupliquée."; return null; }
            }
            else
            {
                key = UniqueFieldKey(label!, usedKeys);
            }
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

    /// <summary>
    /// Fusionne les relations explicites (<c>relations[]</c>) et les relations promues depuis un champ
    /// (<paramref name="promotedRelations"/>) : refs résolues par <c>SlugKey</c> puis vérifiées dans
    /// <paramref name="refs"/>, dédoublonnage par paire (ordre indifférent), borne <see cref="MaxRelations"/>
    /// — chaque cas invalide dégrade en avertissement, jamais un rejet franc de la spec entière.
    /// </summary>
    private static IReadOnlyList<ParsedSystemRelation> ParseRelations(
        JsonNode? node, HashSet<string> refs, List<ParsedSystemRelation> promotedRelations, List<string> warnings)
    {
        // « from »/« to » sont garantis non nuls ici (filtrés à l'ajout, ci-dessus et à la promotion).
        var candidates = new List<(string From, string To, string? Label, string? Junction)>();
        if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                if (item is not JsonObject obj)
                {
                    warnings.Add("Relation ignorée : élément invalide.");
                    continue;
                }
                var kindRaw = Str(obj["kind"]) ?? Str(obj["type"]) ?? "many_to_many";
                if (!ManyToManyAliases.Contains(kindRaw.Trim()))
                {
                    warnings.Add($"Relation ignorée : type « {kindRaw} » non pris en charge (seul many_to_many est accepté).");
                    continue;
                }
                var fromRaw = Str(obj["from"]) ?? Str(obj["source"]) ?? Str(obj["a"]);
                var toRaw = Str(obj["to"]) ?? Str(obj["target"]) ?? Str(obj["b"]);
                if (fromRaw is null || toRaw is null)
                {
                    warnings.Add("Relation ignorée : « from »/« to » sont obligatoires.");
                    continue;
                }
                var label = Str(obj["label"]) ?? Str(obj["libelle"]);
                var junction = Str(obj["junctionName"]) ?? Str(obj["junction"]) ?? Str(obj["table"]);
                candidates.Add((fromRaw, toRaw, label, junction));
            }
        }
        foreach (var promoted in promotedRelations)
            candidates.Add((promoted.FromRef, promoted.ToRef, promoted.Label, promoted.JunctionName));

        var result = new List<ParsedSystemRelation>();
        var seenPairs = new HashSet<(string, string)>();
        foreach (var c in candidates)
        {
            if (result.Count >= MaxRelations)
            {
                warnings.Add($"Au plus {MaxRelations} relations plusieurs-à-plusieurs ; les suivantes sont ignorées.");
                break;
            }

            var from = StudioAiAppSpec.SlugKey(c.From);
            if (string.IsNullOrEmpty(from) || !refs.Contains(from))
            {
                warnings.Add($"Relation ignorée : table « {c.From} » inconnue.");
                continue;
            }
            var to = StudioAiAppSpec.SlugKey(c.To);
            if (string.IsNullOrEmpty(to) || !refs.Contains(to))
            {
                warnings.Add($"Relation ignorée : table « {c.To} » inconnue.");
                continue;
            }
            if (string.Equals(from, to, StringComparison.Ordinal))
            {
                warnings.Add("Relation ignorée : une table ne peut pas être liée à elle-même.");
                continue;
            }

            var pair = string.CompareOrdinal(from, to) <= 0 ? (from, to) : (to, from);
            if (!seenPairs.Add(pair))
            {
                warnings.Add($"Relation en double ignorée : {from} ↔ {to}.");
                continue;
            }

            var label = string.IsNullOrWhiteSpace(c.Label) ? null : TruncateLabel(c.Label!.Trim(), 80);
            var junctionName = string.IsNullOrWhiteSpace(c.Junction) ? null : c.Junction!.Trim();
            result.Add(new ParsedSystemRelation("many_to_many", from, to, label, junctionName));
        }
        return result;
    }

    private static string TruncateLabel(string value, int max) => value.Length > max ? value[..max] : value;

    /// <summary>Slug accent-insensible de la ref explicite (sinon du repli) ; « table » si invalide.</summary>
    private static string SlugRef(string? refKey, string fallback)
    {
        var slug = StudioAiAppSpec.SlugKey(string.IsNullOrWhiteSpace(refKey) ? fallback : refKey);
        return string.IsNullOrEmpty(slug) || !StudioKey.IsValidShape(slug) ? "table" : slug;
    }

    /// <summary>Ref unique dans la spec : suffixe « _2 », « _3 »… en cas de collision, jamais d'écrasement.</summary>
    private static string UniqueRef(string baseRef, HashSet<string> refs)
    {
        var refKey = baseRef;
        var i = 1;
        while (refs.Contains(refKey)) refKey = $"{baseRef}_{++i}";
        refs.Add(refKey);
        return refKey;
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
            // Miroir de StudioAiAppSpec.ParseConfig : un code-barres perdait son format en spec système.
            case CustomFieldType.Barcode:
            {
                var format = (Str(cfg?["format"]) ?? Str(fn?["format"]) ?? "code128").Trim().ToLowerInvariant();
                if (format is not ("code128" or "ean13")) format = "code128";
                return new() { ["format"] = JsonValue.Create(format) };
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
