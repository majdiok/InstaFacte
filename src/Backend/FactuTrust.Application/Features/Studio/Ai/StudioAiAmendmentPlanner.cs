using System.Text.Json;
using System.Text.Json.Nodes;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Features.Studio.Ai;

/// <summary>Une ligne de l'aperçu de modification : ce qui change, avant → après, et son risque.</summary>
public sealed record AmendmentPreviewItem(
    string Op,
    string Target,
    string? Before,
    string? After,
    string Severity,
    string? Warning);

public sealed record AmendmentPreview(
    string EntityKey,
    string EntityDisplayName,
    IReadOnlyList<AmendmentPreviewItem> Items,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Calcule le diff « avant → après » d'une modification de table Studio, et résout les références de
/// champ brutes émises par le modèle contre le schéma RÉEL. Pur (aucun accès base) : l'appelant fournit
/// le schéma. Les mêmes fonctions de résolution servent à l'aperçu et à l'exécution, de sorte que ce que
/// l'utilisateur valide est exactement ce qui sera appliqué.
/// </summary>
public static class StudioAiAmendmentPlanner
{
    private static readonly JsonSerializerOptions SummaryOptions = new(JsonSerializerDefaults.Web);

    public static AmendmentPreview BuildPreview(ParsedAmendmentSpec spec, CustomEntitySchemaDto schema)
    {
        var items = new List<AmendmentPreviewItem>();
        var warnings = new List<string>(spec.Warnings);
        var activeFields = schema.Fields.Where(f => f.IsActive).ToList();

        foreach (var op in spec.Operations)
        {
            switch (op)
            {
                case AddFieldOp add:
                {
                    var clash = activeFields.FirstOrDefault(f =>
                        string.Equals(f.Label, add.Field.Label, StringComparison.OrdinalIgnoreCase));
                    items.Add(new AmendmentPreviewItem("add_field", add.Field.Label, null,
                        DescribeNewField(add.Field), "info",
                        clash is null ? null : $"Un champ « {clash.Label} » existe déjà : le nouveau champ recevra une clé distincte."));
                    break;
                }
                case UpdateFieldOp upd:
                {
                    var target = ResolveField(upd.FieldRef, activeFields);
                    if (target is null)
                    {
                        warnings.Add($"Champ « {upd.FieldRef} » introuvable : modification ignorée.");
                        break;
                    }
                    items.Add(new AmendmentPreviewItem("update_field", target.Label,
                        DescribeField(target), DescribeUpdatedField(target, upd), "info", null));
                    break;
                }
                case RemoveFieldOp rem:
                {
                    var target = ResolveField(rem.FieldRef, activeFields);
                    if (target is null)
                    {
                        warnings.Add($"Champ « {rem.FieldRef} » introuvable : retrait ignoré.");
                        break;
                    }
                    items.Add(new AmendmentPreviewItem("remove_field", target.Label,
                        DescribeField(target), "retiré du formulaire", "warning",
                        "Les valeurs déjà saisies sont CONSERVÉES ; le champ est seulement masqué."));
                    break;
                }
                case UpdateEntityOp ent:
                {
                    items.Add(new AmendmentPreviewItem("update_entity", schema.Entity.DisplayName,
                        schema.Entity.DisplayName,
                        ent.DisplayName ?? schema.Entity.DisplayName, "info", null));
                    break;
                }
                case SetFormOp form:
                {
                    var layout = ResolveForm(form.FormNode, activeFields);
                    if (layout is null || layout.Sections.Count == 0)
                    {
                        warnings.Add("Mise en page du formulaire ignorée : aucun champ reconnu.");
                        break;
                    }
                    var after = string.Join(" | ", layout.Sections.Select(s =>
                        $"{s.Title ?? "Section"} ({s.Fields.Count})"));
                    items.Add(new AmendmentPreviewItem("set_form", "Formulaire",
                        $"{schema.Form.Sections.Count} section(s)", after, "warning",
                        "La mise en page actuelle du formulaire sera remplacée."));
                    break;
                }
                case SetReportOp report:
                {
                    var def = ResolveReport(report.ReportNode, activeFields);
                    if (def is null)
                    {
                        warnings.Add("État ignoré : aucun champ reconnu dans la définition.");
                        break;
                    }
                    items.Add(new AmendmentPreviewItem("set_report", report.DisplayName ?? "Rapport",
                        null, DescribeReport(def), "info", null));
                    break;
                }
            }
        }

        return new AmendmentPreview(schema.Entity.Key, schema.Entity.DisplayName, items, warnings);
    }

    /// <summary>Sérialise l'aperçu au format attendu par la carte de validation du frontend.</summary>
    public static string ToSummaryJson(AmendmentPreview preview)
    {
        var steps = preview.Items
            .Select(i => new StudioAiPlanSummary.SummaryStep(i.Op, StepLabel(i), StepDetail(i)))
            .ToList();

        var summary = new StudioAiPlanSummary.PlanSummary(
            StudioAiPlanKind.Amendment.ToString(),
            $"Modification de « {preview.EntityDisplayName} »",
            steps,
            // Pas de pastilles « table » ici : les étapes portent déjà le détail du diff, et un
            // compteur de champs serait trompeur sur une modification.
            Array.Empty<StudioAiPlanSummary.SummaryEntity>(),
            preview.Warnings.Concat(preview.Items.Where(i => i.Warning is not null).Select(i => i.Warning!))
                .Distinct().ToList());

        return JsonSerializer.Serialize(summary, SummaryOptions);
    }

    // ---- Résolution partagée aperçu / exécution ----

    /// <summary>Résout une référence brute (clé exacte, clé slugifiée ou libellé) vers un champ actif.</summary>
    public static CustomFieldDto? ResolveField(string raw, IReadOnlyList<CustomFieldDto> fields)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var trimmed = raw.Trim();
        var exact = fields.FirstOrDefault(f => string.Equals(f.Key, trimmed, StringComparison.Ordinal));
        if (exact is not null) return exact;

        var slug = StudioAiAppSpec.SlugKey(trimmed);
        return fields.FirstOrDefault(f => string.Equals(f.Key, slug, StringComparison.Ordinal))
            ?? fields.FirstOrDefault(f => string.Equals(StudioAiAppSpec.SlugKey(f.Label), slug, StringComparison.Ordinal));
    }

    /// <summary>
    /// Construit une mise en page à partir du nœud émis par le modèle. Accepte, pour chaque entrée,
    /// une chaîne (clé/libellé) ou un objet { field, width, label } ; toute référence non résolue est
    /// écartée — la sanitisation côté commande reste le dernier filet.
    /// </summary>
    public static FormLayout? ResolveForm(JsonNode formNode, IReadOnlyList<CustomFieldDto> fields)
    {
        var sectionsArr = (formNode["sections"] ?? formNode)?.AsArray();
        if (sectionsArr is null) return null;

        var sections = new List<FormSection>();
        foreach (var sec in sectionsArr)
        {
            var refs = new List<FormFieldRef>();
            foreach (var entry in sec?["fields"]?.AsArray() ?? new JsonArray())
            {
                string? raw;
                string? width = null;
                string? labelOverride = null;
                if (entry is JsonObject o)
                {
                    raw = AsString(o["field"]) ?? AsString(o["key"]) ?? AsString(o["name"]);
                    width = string.Equals(AsString(o["width"]), "half", StringComparison.OrdinalIgnoreCase) ? "half" : "full";
                    labelOverride = AsString(o["label"]) ?? AsString(o["labelOverride"]);
                }
                else
                {
                    raw = AsString(entry);
                }
                if (raw is null) continue;
                var field = ResolveField(raw, fields);
                if (field is null) continue;
                refs.Add(new FormFieldRef
                {
                    Key = field.Key,
                    Width = width ?? "full",
                    LabelOverride = string.IsNullOrWhiteSpace(labelOverride) ? null : labelOverride!.Trim()
                });
            }
            if (refs.Count > 0)
                sections.Add(new FormSection { Title = AsString(sec?["title"]), Fields = refs });
        }
        return sections.Count > 0 ? new FormLayout { Sections = sections } : null;
    }

    /// <summary>Construit une définition d'état en résolvant ses références contre les champs réels.</summary>
    public static ReportDefinition? ResolveReport(JsonNode reportNode, IReadOnlyList<CustomFieldDto> fields)
    {
        var parsed = StudioAiAppSpec.ParseReportForFields(
            reportNode, fields.Select(f => (f.Key, f.Label)).ToList());
        if (parsed is null) return null;

        return new ReportDefinition
        {
            Fields = parsed.Fields,
            Filters = parsed.Filters,
            Grouping = parsed.Grouping,
            Aggregations = parsed.Aggregations,
            Sort = parsed.Sort
        };
    }

    // ---- Descriptions lisibles ----

    private static string StepLabel(AmendmentPreviewItem item) => item.Op switch
    {
        "add_field" => $"Ajouter « {item.Target} »",
        "update_field" => $"Modifier « {item.Target} »",
        "remove_field" => $"Retirer « {item.Target} »",
        "update_entity" => "Renommer la table",
        "set_form" => "Réorganiser le formulaire",
        "set_report" => $"État « {item.Target} »",
        _ => item.Target
    };

    private static string StepDetail(AmendmentPreviewItem item) =>
        item.Before is null ? item.After ?? string.Empty : $"{item.Before} → {item.After}";

    private static string DescribeField(CustomFieldDto f)
    {
        var flags = new List<string> { TypeLabel(f.FieldType) };
        if (f.IsRequired) flags.Add("obligatoire");
        if (f.IsUnique) flags.Add("unique");
        return string.Join(", ", flags);
    }

    private static string DescribeNewField(ParsedAppField f)
    {
        var flags = new List<string> { TypeLabel(f.FieldType) };
        if (f.Required) flags.Add("obligatoire");
        if (f.Unique) flags.Add("unique");
        if (f.Options is { Count: > 0 }) flags.Add($"{f.Options.Count} option(s)");
        return string.Join(", ", flags);
    }

    private static string DescribeUpdatedField(CustomFieldDto before, UpdateFieldOp op)
    {
        var flags = new List<string> { TypeLabel(before.FieldType) };
        if (op.Label is not null && !string.Equals(op.Label, before.Label, StringComparison.Ordinal))
            flags.Insert(0, $"libellé « {op.Label} »");
        var required = op.Required ?? before.IsRequired;
        var unique = op.Unique ?? before.IsUnique;
        if (required) flags.Add("obligatoire");
        if (unique) flags.Add("unique");
        if (op.AddOptions is { Count: > 0 }) flags.Add($"+{op.AddOptions.Count} option(s)");
        return string.Join(", ", flags);
    }

    private static string DescribeReport(ReportDefinition def)
    {
        var parts = new List<string>();
        if (def.Grouping.Count > 0) parts.Add($"{def.Grouping.Count} regroupement(s)");
        if (def.Aggregations.Count > 0) parts.Add($"{def.Aggregations.Count} mesure(s)");
        if (def.Fields.Count > 0) parts.Add($"{def.Fields.Count} colonne(s)");
        if (def.Filters.Count > 0) parts.Add($"{def.Filters.Count} filtre(s)");
        if (def.Sort.Count > 0) parts.Add($"{def.Sort.Count} tri(s)");
        return parts.Count > 0 ? string.Join(", ", parts) : "état vide";
    }

    private static string TypeLabel(CustomFieldType type) => type switch
    {
        CustomFieldType.Text => "texte",
        CustomFieldType.MultilineText => "texte long",
        CustomFieldType.Number => "nombre",
        CustomFieldType.Decimal => "décimal",
        CustomFieldType.Money => "montant",
        CustomFieldType.Percentage => "pourcentage",
        CustomFieldType.Rating => "note",
        CustomFieldType.Boolean => "oui/non",
        CustomFieldType.Date => "date",
        CustomFieldType.DateTime => "date et heure",
        CustomFieldType.Select => "liste",
        CustomFieldType.MultiSelect => "choix multiple",
        CustomFieldType.Attachment => "pièce jointe",
        CustomFieldType.Signature => "signature",
        CustomFieldType.QrCode => "QR code",
        CustomFieldType.Barcode => "code-barres",
        CustomFieldType.AutoNumber => "numéro auto",
        _ => type.ToString().ToLowerInvariant()
    };

    private static string? AsString(JsonNode? n)
    {
        if (n is not JsonValue v) return null;
        if (v.TryGetValue<string>(out var s)) return string.IsNullOrWhiteSpace(s) ? null : s;
        return v.ToString();
    }
}
