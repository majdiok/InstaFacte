using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Features.Studio.Ai;

/// <summary>
/// Forme CANONIQUE d'une spec Studio IA : clés dans un ordre fixe, noms de types canoniques
/// (<c>text</c>, <c>select</c>, <c>date</c>, …), options en objets <c>{value,label}</c>, clé de
/// champ toujours explicite (<c>"key"</c>). C'est la seule forme persistée après édition et la
/// seule exposée à l'éditeur d'aperçu : la canonicalisation EST le filtre d'écriture massive
/// (aucune clé inconnue conservée). Les alias d'entrée (<c>ref</c>/<c>key</c>, <c>label</c>/<c>name</c>,
/// types français « texte », « liste », …) restent acceptés par les parseurs mais ne sont jamais émis.
/// Invariant : <c>CanonicalFor(kind, CanonicalFor(kind, x))</c> est stable à l'octet près.
/// </summary>
public static class StudioAiSpecCanonical
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true, // 2 espaces par défaut, sans espaces finaux
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping // « é » reste lisible dans l'éditeur
    };

    /// <summary>Écriture ordonnée (ordre d'insertion) + indentation 2 espaces, sans espaces finaux.</summary>
    public static string Serialize(JsonNode node) => node.ToJsonString(SerializerOptions);

    /// <summary>Parse une spec selon la nature du plan puis la resérialise en forme canonique ; null si invalide.</summary>
    public static string? CanonicalFor(StudioAiPlanKind kind, string specJson, out string? error)
    {
        error = null;
        switch (kind)
        {
            case StudioAiPlanKind.CreateSystem:
                if (!StudioAiSystemSpec.TryParse(specJson, out var system, out error) || system is null) return null;
                return CanonicalSystem(system);
            case StudioAiPlanKind.CreateApp:
                if (!StudioAiAppSpec.TryParse(specJson, out var app, out error) || app is null) return null;
                return CanonicalApp(app);
            case StudioAiPlanKind.Amendment:
                if (!StudioAiAmendmentSpec.TryParse(specJson, out var amendment, out error) || amendment is null) return null;
                return CanonicalAmendment(amendment);
            case StudioAiPlanKind.View:
                if (!StudioAiViewSpec.TryParse(specJson, out var view, out error) || view is null) return null;
                return CanonicalView(view);
            case StudioAiPlanKind.Report:
                if (!StudioAiReportSpec.TryParse(specJson, out var report, out error) || report is null) return null;
                return CanonicalReport(report);
            default:
                error = $"Nature de plan « {kind} » non prise en charge.";
                return null;
        }
    }

    // ---- Système multi-tables -------------------------------------------------------------

    /// <summary>Ordre fixe des clés : system{displayName,icon,description,onboarding(,menu P2)}, entities[], seed[].</summary>
    public static string CanonicalSystem(ParsedSystemSpec spec)
    {
        var system = new JsonObject { ["displayName"] = spec.SystemDisplayName };
        if (spec.SystemIcon is not null) system["icon"] = spec.SystemIcon;
        if (spec.SystemDescription is not null) system["description"] = spec.SystemDescription;
        if (spec.OnboardingSteps is { Count: > 0 })
            system["onboarding"] = StringArray(spec.OnboardingSteps);

        var entities = new JsonArray();
        foreach (var entity in spec.Entities)
        {
            // Table réutilisée : la clé existante suffit — aucun champ/formulaire/état n'est émis
            // (ils ne seraient de toute façon pas appliqués). displayName est ré-émis car il sert de
            // libellé d'aperçu ; au re-parse il retombe sur existingKey s'il est absent.
            if (entity.ExistingKey is not null)
            {
                entities.Add(new JsonObject
                {
                    ["ref"] = entity.Ref,
                    ["existingKey"] = entity.ExistingKey,
                    ["displayName"] = entity.EntityDisplayName
                });
                continue;
            }

            var node = new JsonObject
            {
                ["ref"] = entity.Ref,
                ["displayName"] = entity.EntityDisplayName,
                ["displayNamePlural"] = entity.EntityDisplayNamePlural
            };
            if (entity.Icon is not null) node["icon"] = entity.Icon;
            if (entity.Description is not null) node["description"] = entity.Description;
            node["fields"] = new JsonArray(entity.Fields
                .Select(f => FieldJson(f.Key, f.Label, f.FieldType, f.Required, f.Unique, f.Options, f.Config, f.RelationToRef))
                .ToArray());
            if (entity.Form is not null) node["form"] = FormJson(entity.Form);
            if (entity.Report is not null) node["report"] = ReportJson(entity.Report);
            entities.Add(node);
        }

        var root = new JsonObject
        {
            ["system"] = system,
            ["entities"] = entities
        };
        if (spec.Seed.Count > 0)
        {
            root["seed"] = new JsonArray(spec.Seed.Select(batch => (JsonNode)new JsonObject
            {
                ["entityRef"] = batch.EntityRef,
                ["records"] = new JsonArray(batch.Records.Select(r => (JsonNode)RecordJson(r)).ToArray())
            }).ToArray());
        }
        return Serialize(root);
    }

    // ---- Table simple ----------------------------------------------------------------------

    public static string CanonicalApp(ParsedAppSpec spec)
    {
        var entity = new JsonObject
        {
            ["displayName"] = spec.EntityDisplayName,
            ["displayNamePlural"] = spec.EntityDisplayNamePlural
        };
        if (spec.Icon is not null) entity["icon"] = spec.Icon;
        if (spec.Description is not null) entity["description"] = spec.Description;

        var root = new JsonObject
        {
            ["entity"] = entity,
            ["fields"] = new JsonArray(spec.Fields
                .Select(f => FieldJson(f.Key, f.Label, f.FieldType, f.Required, f.Unique, f.Options, f.Config, null))
                .ToArray())
        };
        if (spec.Report is not null) root["report"] = ReportJson(spec.Report);
        return Serialize(root);
    }

    // ---- Modification d'une table existante -------------------------------------------------

    public static string CanonicalAmendment(ParsedAmendmentSpec spec)
    {
        var operations = new JsonArray();
        foreach (var operation in spec.Operations)
        {
            switch (operation)
            {
                case AddFieldOp add:
                {
                    var field = add.Field;
                    var node = new JsonObject
                    {
                        ["op"] = "add_field",
                        ["key"] = field.Key,
                        ["label"] = field.Label,
                        ["type"] = CanonicalTypeName(field.FieldType),
                        ["required"] = field.Required,
                        ["unique"] = field.Unique
                    };
                    if (field.Options is { Count: > 0 }) node["options"] = OptionsJson(field.Options);
                    if (field.Config is { Count: > 0 }) node["config"] = ConfigJson(field.Config);
                    operations.Add(node);
                    break;
                }
                case UpdateFieldOp update:
                {
                    var node = new JsonObject { ["op"] = "update_field", ["field"] = update.FieldRef };
                    if (update.Label is not null) node["label"] = update.Label;
                    if (update.Required is not null) node["required"] = update.Required.Value;
                    if (update.Unique is not null) node["unique"] = update.Unique.Value;
                    if (update.AddOptions is { Count: > 0 }) node["addOptions"] = OptionsJson(update.AddOptions);
                    operations.Add(node);
                    break;
                }
                case RemoveFieldOp remove:
                    operations.Add(new JsonObject { ["op"] = "remove_field", ["field"] = remove.FieldRef });
                    break;
                case UpdateEntityOp updateEntity:
                {
                    var node = new JsonObject { ["op"] = "update_entity" };
                    if (updateEntity.DisplayName is not null) node["displayName"] = updateEntity.DisplayName;
                    if (updateEntity.DisplayNamePlural is not null) node["displayNamePlural"] = updateEntity.DisplayNamePlural;
                    if (updateEntity.Icon is not null) node["icon"] = updateEntity.Icon;
                    if (updateEntity.Description is not null) node["description"] = updateEntity.Description;
                    operations.Add(node);
                    break;
                }
                case SetFormOp setForm:
                    // Charge libre résolue contre le schéma réel à l'exécution : conservée telle quelle.
                    operations.Add(new JsonObject { ["op"] = "set_form", ["form"] = setForm.FormNode.DeepClone() });
                    break;
                case SetReportOp setReport:
                {
                    var node = new JsonObject { ["op"] = "set_report" };
                    if (setReport.DisplayName is not null) node["displayName"] = setReport.DisplayName;
                    node["report"] = setReport.ReportNode.DeepClone();
                    operations.Add(node);
                    break;
                }
            }
        }

        var root = new JsonObject
        {
            ["target"] = new JsonObject { ["entityKey"] = spec.TargetEntityRef },
            ["operations"] = operations
        };
        return Serialize(root);
    }

    // ---- Fenêtre (vue lecture seule) ---------------------------------------------------------

    public static string CanonicalView(ParsedViewSpec spec)
    {
        var columns = new JsonArray();
        foreach (var column in spec.Columns)
        {
            var node = new JsonObject { ["name"] = column.Name };
            if (column.Label is not null) node["label"] = column.Label;
            if (column.Format is not null) node["format"] = column.Format;
            columns.Add(node);
        }

        var root = new JsonObject
        {
            ["title"] = spec.Title,
            ["table"] = spec.Table,
            ["columns"] = columns,
            ["search"] = spec.Search
        };
        return Serialize(root);
    }

    // ---- État (rapport SQL) ------------------------------------------------------------------

    public static string CanonicalReport(ParsedReportSpec spec)
    {
        var root = new JsonObject { ["title"] = spec.Title };
        if (spec.PresetKey is not null) root["preset"] = spec.PresetKey;
        if (spec.Source is not null) root["source"] = spec.Source;
        if (spec.Grouping.Count > 0) root["groupBy"] = StringArray(spec.Grouping);
        if (spec.Measures.Count > 0) root["measures"] = MeasuresJson(spec.Measures);
        if (spec.Columns.Count > 0) root["columns"] = StringArray(spec.Columns);
        if (spec.Filters.Count > 0) root["filters"] = FiltersJson(spec.Filters);
        if (spec.Sort.Count > 0) root["sort"] = SortJson(spec.Sort);
        if (spec.From is not null) root["from"] = spec.From.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (spec.To is not null) root["to"] = spec.To.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return Serialize(root);
    }

    // ---- Briques communes --------------------------------------------------------------------

    /// <summary>Noms canoniques émis (premier alias de <c>StudioAiAppSpec.TypeAliases</c>).</summary>
    private static string CanonicalTypeName(CustomFieldType type) => type switch
    {
        CustomFieldType.Text => "text",
        CustomFieldType.MultilineText => "multilinetext",
        CustomFieldType.Number => "number",
        CustomFieldType.Decimal => "decimal",
        CustomFieldType.Boolean => "boolean",
        CustomFieldType.Date => "date",
        CustomFieldType.DateTime => "datetime",
        CustomFieldType.Select => "select",
        CustomFieldType.MultiSelect => "multiselect",
        CustomFieldType.Money => "money",
        CustomFieldType.Percentage => "percentage",
        CustomFieldType.Rating => "rating",
        CustomFieldType.QrCode => "qrcode",
        CustomFieldType.Barcode => "barcode",
        CustomFieldType.AutoNumber => "autonumber",
        CustomFieldType.Attachment => "attachment",
        CustomFieldType.Signature => "signature",
        CustomFieldType.RelationCustom => "relation",
        CustomFieldType.RelationExisting => "relation",
        // Jamais produit par les parseurs IA (Formula/Lookup/Rollup exclus à l'entrée) — repli défensif.
        _ => "text"
    };

    private static JsonObject FieldJson(
        string key, string label, CustomFieldType type, bool required, bool unique,
        IReadOnlyList<SelectOptionDto>? options, Dictionary<string, JsonNode?>? config, string? relationToRef)
    {
        var node = new JsonObject
        {
            ["key"] = key,
            ["label"] = label,
            ["type"] = CanonicalTypeName(type),
            ["required"] = required,
            ["unique"] = unique
        };
        if (options is { Count: > 0 }) node["options"] = OptionsJson(options);
        if (config is { Count: > 0 }) node["config"] = ConfigJson(config);
        if (relationToRef is not null) node["relationTo"] = relationToRef;
        return node;
    }

    private static JsonArray OptionsJson(IReadOnlyList<SelectOptionDto> options) =>
        new(options.Select(o => (JsonNode)new JsonObject { ["value"] = o.Value, ["label"] = o.Label }).ToArray());

    /// <summary>Seules les clés produites par les parseurs (currency/max/format), dans cet ordre fixe.</summary>
    private static JsonObject ConfigJson(IReadOnlyDictionary<string, JsonNode?> config)
    {
        var node = new JsonObject();
        foreach (var key in new[] { "currency", "max", "format" })
            if (config.TryGetValue(key, out var value))
                node[key] = value?.DeepClone();
        return node;
    }

    /// <summary>Formulaire : <c>{"sections":[{"title","fields":[{"field","width"?,"label"?}]}]}</c>, clé résolue.</summary>
    private static JsonObject FormJson(ParsedFormSpec form)
    {
        var sections = new JsonArray();
        foreach (var section in form.Sections)
        {
            var fields = new JsonArray();
            foreach (var field in section.Fields)
            {
                var node = new JsonObject { ["field"] = field.Key };
                if (field.Width is not null) node["width"] = field.Width;
                if (field.LabelOverride is not null) node["label"] = field.LabelOverride;
                fields.Add(node);
            }
            var sectionNode = new JsonObject();
            if (section.Title is not null) sectionNode["title"] = section.Title;
            sectionNode["fields"] = fields;
            sections.Add(sectionNode);
        }
        return new JsonObject { ["sections"] = sections };
    }

    private static JsonObject ReportJson(ParsedAppReport report)
    {
        var node = new JsonObject { ["displayName"] = report.DisplayName };
        if (report.Grouping.Count > 0) node["groupBy"] = StringArray(report.Grouping);
        if (report.Aggregations.Count > 0) node["measures"] = MeasuresJson(report.Aggregations);
        if (report.Fields.Count > 0) node["columns"] = StringArray(report.Fields);
        if (report.Filters.Count > 0) node["filters"] = FiltersJson(report.Filters);
        if (report.Sort.Count > 0) node["sort"] = SortJson(report.Sort);
        return node;
    }

    private static JsonArray MeasuresJson(IReadOnlyList<ReportAggregation> measures)
    {
        var array = new JsonArray();
        foreach (var measure in measures)
        {
            var node = new JsonObject();
            if (!string.IsNullOrEmpty(measure.Field)) node["field"] = measure.Field;
            node["fn"] = measure.Fn;
            array.Add(node);
        }
        return array;
    }

    private static JsonArray FiltersJson(IReadOnlyList<ReportFilter> filters)
    {
        var array = new JsonArray();
        foreach (var filter in filters)
        {
            var node = new JsonObject
            {
                ["field"] = filter.Field,
                ["op"] = filter.Op,
                ["value"] = filter.Value?.DeepClone()
            };
            if (filter.Value2 is not null) node["value2"] = filter.Value2.DeepClone();
            array.Add(node);
        }
        return array;
    }

    private static JsonArray SortJson(IReadOnlyList<ReportSort> sorts) =>
        new(sorts.Select(s => (JsonNode)new JsonObject { ["field"] = s.Field, ["dir"] = s.Dir }).ToArray());

    private static JsonArray StringArray(IReadOnlyList<string> values) =>
        new(values.Select(v => (JsonNode)JsonValue.Create(v)).ToArray());

    private static JsonObject RecordJson(IReadOnlyDictionary<string, JsonNode?> record)
    {
        var node = new JsonObject();
        foreach (var kvp in record)
            node[kvp.Key] = kvp.Value?.DeepClone();
        return node;
    }
}
