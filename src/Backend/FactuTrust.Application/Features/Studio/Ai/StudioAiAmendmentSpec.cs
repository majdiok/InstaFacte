using System.Text.Json;
using System.Text.Json.Nodes;
using FactuTrust.Application.Features.Studio.Common;

namespace FactuTrust.Application.Features.Studio.Ai;

/// <summary>Une opération de modification d'un artefact Studio existant.</summary>
public abstract record ParsedAmendmentOp(string Op);

/// <summary>Ajout d'un champ. Le champ est déjà normalisé (type, options, config).</summary>
public sealed record AddFieldOp(ParsedAppField Field) : ParsedAmendmentOp("add_field");

/// <summary>
/// Modification d'un champ existant. <paramref name="FieldRef"/> est la référence BRUTE écrite par le
/// modèle (clé ou libellé) : elle est résolue contre le schéma réel au moment de l'aperçu et de
/// l'exécution. Seules les propriétés non nulles sont appliquées.
/// </summary>
public sealed record UpdateFieldOp(
    string FieldRef,
    string? Label,
    bool? Required,
    bool? Unique,
    IReadOnlyList<SelectOptionDto>? AddOptions) : ParsedAmendmentOp("update_field");

/// <summary>Retrait d'un champ : désactivation (les données saisies restent conservées).</summary>
public sealed record RemoveFieldOp(string FieldRef) : ParsedAmendmentOp("remove_field");

public sealed record UpdateEntityOp(
    string? DisplayName,
    string? DisplayNamePlural,
    string? Icon,
    string? Description) : ParsedAmendmentOp("update_entity");

/// <summary>Remplacement de la mise en page du formulaire. Résolu contre les champs réels.</summary>
public sealed record SetFormOp(JsonNode FormNode) : ParsedAmendmentOp("set_form");

/// <summary>Création/remplacement d'un état. Résolu contre les champs réels.</summary>
public sealed record SetReportOp(string? DisplayName, JsonNode ReportNode) : ParsedAmendmentOp("set_report");

public sealed record ParsedAmendmentSpec(
    string TargetEntityRef,
    IReadOnlyList<ParsedAmendmentOp> Operations,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Analyse la spécification de MODIFICATION d'une table Studio existante (<c>studio_plan_changes</c>).
/// Pur (aucun accès base) et tolérant comme <see cref="StudioAiSystemSpec"/> : une opération inconnue
/// ou mal formée est ignorée avec un avertissement plutôt que de faire échouer toute la demande.
/// Liste blanche stricte d'opérations : ni suppression de table, ni suppression de système.
/// Les références de champ restent BRUTES ici — elles sont résolues contre le schéma réel par
/// <see cref="StudioAiAmendmentPlanner"/> (aperçu) puis à nouveau à l'exécution.
/// </summary>
public static class StudioAiAmendmentSpec
{
    public const int MaxOperations = 20;

    private static readonly HashSet<string> KnownOps = new(StringComparer.OrdinalIgnoreCase)
    {
        "add_field", "update_field", "remove_field", "update_entity", "set_form", "set_report"
    };

    public static bool TryParse(string? specJson, out ParsedAmendmentSpec? spec, out string? error)
    {
        spec = null;
        error = null;
        if (string.IsNullOrWhiteSpace(specJson)) { error = "spec_json est vide."; return false; }

        JsonNode? root;
        try { root = JsonNode.Parse(specJson); }
        catch (JsonException) { error = "spec_json n'est pas un JSON valide."; return false; }

        var target = Str(root?["target"]?["entityKey"])
            ?? Str(root?["target"]?["table"])
            ?? Str(root?["entityKey"])
            ?? Str(root?["table"]);
        if (string.IsNullOrWhiteSpace(target))
        {
            error = "target.entityKey est obligatoire (la table à modifier).";
            return false;
        }

        var opsArr = (root?["operations"] ?? root?["changes"] ?? root?["ops"])?.AsArray();
        if (opsArr is null || opsArr.Count == 0)
        {
            error = "Au moins une opération est requise.";
            return false;
        }

        var warnings = new List<string>();
        var operations = new List<ParsedAmendmentOp>();

        foreach (var node in opsArr)
        {
            if (operations.Count >= MaxOperations)
            {
                warnings.Add($"Au-delà de {MaxOperations} opérations, les suivantes ont été ignorées.");
                break;
            }
            if (node is not JsonObject obj) continue;

            var op = (Str(obj["op"]) ?? Str(obj["action"]) ?? string.Empty).Trim();
            if (!KnownOps.Contains(op))
            {
                warnings.Add($"Opération « {(string.IsNullOrEmpty(op) ? "?" : op)} » non prise en charge : ignorée.");
                continue;
            }

            var parsed = ParseOperation(op, obj, warnings);
            if (parsed is not null) operations.Add(parsed);
        }

        if (operations.Count == 0)
        {
            error = "Aucune opération de modification valide.";
            return false;
        }

        spec = new ParsedAmendmentSpec(StudioAiAppSpec.SlugKey(target!), operations, warnings);
        return true;
    }

    private static ParsedAmendmentOp? ParseOperation(string op, JsonObject obj, List<string> warnings)
    {
        switch (op.ToLowerInvariant())
        {
            case "add_field":
            {
                var field = ParseSingleField(obj);
                if (field is null)
                {
                    warnings.Add("Ajout de champ ignoré : libellé manquant.");
                    return null;
                }
                return new AddFieldOp(field);
            }
            case "update_field":
            {
                var fieldRef = FieldRef(obj);
                if (fieldRef is null)
                {
                    warnings.Add("Modification de champ ignorée : champ cible non précisé.");
                    return null;
                }
                return new UpdateFieldOp(
                    fieldRef,
                    Str(obj["label"]) ?? Str(obj["newLabel"]),
                    Bool(obj["required"]) ?? Bool(obj["isRequired"]),
                    Bool(obj["unique"]) ?? Bool(obj["isUnique"]),
                    ParseOptions(obj["addOptions"] ?? obj["options"]));
            }
            case "remove_field":
            {
                var fieldRef = FieldRef(obj);
                if (fieldRef is null)
                {
                    warnings.Add("Retrait de champ ignoré : champ cible non précisé.");
                    return null;
                }
                return new RemoveFieldOp(fieldRef);
            }
            case "update_entity":
            {
                var display = Str(obj["displayName"]) ?? Str(obj["name"]);
                var plural = Str(obj["displayNamePlural"]);
                var icon = Str(obj["icon"]);
                var description = Str(obj["description"]);
                if (display is null && plural is null && icon is null && description is null)
                {
                    warnings.Add("Modification de la table ignorée : aucune propriété fournie.");
                    return null;
                }
                return new UpdateEntityOp(display, plural, icon, description);
            }
            case "set_form":
            {
                // Le nœud peut être { "sections": [...] } ou directement [ ... ].
                var form = obj["form"] ?? (obj["sections"] is not null
                    ? new JsonObject { ["sections"] = obj["sections"]!.DeepClone() }
                    : null);
                if (form is null)
                {
                    warnings.Add("Mise en page du formulaire ignorée : sections manquantes.");
                    return null;
                }
                return new SetFormOp(form.DeepClone());
            }
            case "set_report":
            {
                var report = obj["definition"] ?? obj["report"];
                if (report is null)
                {
                    // Tolérance : le modèle a pu poser groupBy/measures directement sur l'opération.
                    var inline = new JsonObject();
                    foreach (var key in new[] { "groupBy", "measures", "columns", "filters", "sort" })
                        if (obj[key] is not null) inline[key] = obj[key]!.DeepClone();
                    if (inline.Count == 0)
                    {
                        warnings.Add("État ignoré : définition manquante.");
                        return null;
                    }
                    report = inline;
                }
                return new SetReportOp(Str(obj["displayName"]) ?? Str(obj["title"]), report.DeepClone());
            }
            default:
                return null;
        }
    }

    /// <summary>
    /// Normalise un champ à ajouter en réutilisant le parseur d'application (types, options, config) —
    /// même patron que <c>StudioAiSystemSpec.ParseEntityReport</c> : on enveloppe dans un spec minimal.
    /// </summary>
    private static ParsedAppField? ParseSingleField(JsonObject obj)
    {
        var wrapper = new JsonObject
        {
            ["entity"] = new JsonObject { ["displayName"] = "T" },
            ["fields"] = new JsonArray(obj.DeepClone())
        };
        return StudioAiAppSpec.TryParse(wrapper.ToJsonString(), out var appSpec, out _)
            ? appSpec?.Fields.FirstOrDefault()
            : null;
    }

    private static string? FieldRef(JsonObject obj) =>
        Str(obj["key"]) ?? Str(obj["field"]) ?? Str(obj["fieldKey"]) ?? Str(obj["label"]) ?? Str(obj["name"]);

    private static IReadOnlyList<SelectOptionDto>? ParseOptions(JsonNode? node)
    {
        if (node is not JsonArray arr) return null;
        var result = new List<SelectOptionDto>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in arr)
        {
            string? value;
            string? label;
            if (item is JsonObject o)
            {
                value = Str(o["value"]) ?? Str(o["label"]);
                label = Str(o["label"]) ?? value;
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
