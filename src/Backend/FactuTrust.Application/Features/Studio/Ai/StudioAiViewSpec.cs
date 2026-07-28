using System.Text.Json;
using System.Text.Json.Nodes;
using FactuTrust.Application.Features.Studio.Common;

namespace FactuTrust.Application.Features.Studio.Ai;

public sealed record ParsedViewColumn(string Name, string? Label, string? Format);

public sealed record ParsedViewSpec(
    string Title,
    string Table,
    IReadOnlyList<ParsedViewColumn> Columns,
    bool Search,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Analyse la spécification d'une FENÊTRE (vue lecture seule sur une table SQL réelle) — outil
/// <c>studio_plan_view</c>. Pur : la validation d'accès reste entièrement déléguée à
/// <see cref="SqlSchemaGuard"/> et à <c>ISqlSchemaProvider</c> (liste de refus + schéma vivant),
/// donc l'IA emprunte exactement le même chemin contrôlé que le concepteur humain.
/// Une colonne inconnue est écartée à la résolution, jamais devinée.
/// </summary>
public static class StudioAiViewSpec
{
    public const int MaxColumns = 25;

    private static readonly HashSet<string> KnownFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "text", "date", "datetime", "number", "money", "boolean", "uuid", "status", "fk"
    };

    public static bool TryParse(string? specJson, out ParsedViewSpec? spec, out string? error)
    {
        spec = null;
        error = null;
        if (string.IsNullOrWhiteSpace(specJson)) { error = "spec_json est vide."; return false; }

        JsonNode? root;
        try { root = JsonNode.Parse(specJson); }
        catch (JsonException) { error = "spec_json n'est pas un JSON valide."; return false; }

        var table = Str(root?["table"]) ?? Str(root?["sourceTable"]) ?? Str(root?["source"]);
        if (string.IsNullOrWhiteSpace(table)) { error = "table est obligatoire (la table SQL à afficher)."; return false; }

        table = table!.Trim();
        // Refus immédiat sur un identifiant douteux ou une table interdite : on ne laisse jamais
        // une valeur inventée par le modèle atteindre la couche SQL.
        if (!SqlSchemaGuard.IsValidIdentifier(table)) { error = $"Nom de table invalide : « {table} »."; return false; }
        if (SqlSchemaGuard.IsDenied(table)) { error = $"La table « {table} » n'est pas accessible."; return false; }

        var title = Str(root?["title"]) ?? Str(root?["displayName"]) ?? Str(root?["name"]) ?? table;

        var warnings = new List<string>();
        var columns = new List<ParsedViewColumn>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in root?["columns"]?.AsArray() ?? new JsonArray())
        {
            if (columns.Count >= MaxColumns)
            {
                warnings.Add($"Au-delà de {MaxColumns} colonnes, les suivantes ont été ignorées.");
                break;
            }

            string? name;
            string? label = null;
            string? format = null;
            if (node is JsonObject obj)
            {
                name = Str(obj["name"]) ?? Str(obj["column"]) ?? Str(obj["field"]);
                label = Str(obj["label"]);
                format = Str(obj["format"])?.Trim().ToLowerInvariant();
                if (format is not null && !KnownFormats.Contains(format)) format = null;
            }
            else
            {
                name = Str(node);
            }

            if (string.IsNullOrWhiteSpace(name)) continue;
            name = name!.Trim();
            if (!SqlSchemaGuard.IsValidIdentifier(name))
            {
                warnings.Add($"Colonne « {name} » ignorée : nom invalide.");
                continue;
            }
            if (!seen.Add(name)) continue;
            columns.Add(new ParsedViewColumn(name, label, format));
        }

        spec = new ParsedViewSpec(title.Trim(), table, columns, Bool(root?["search"]) ?? true, warnings);
        return true;
    }

    /// <summary>
    /// Confronte les colonnes demandées au schéma RÉEL : ne garde que celles qui existent, en
    /// conservant le format suggéré par l'introspection quand le modèle n'en a pas fourni. Spec sans
    /// colonne = toutes les colonnes réelles (plafonnées), comportement attendu d'un « affiche-moi la table ».
    /// </summary>
    public static (ViewDefinition Definition, IReadOnlyList<string> Warnings) ResolveAgainstSchema(
        ParsedViewSpec spec, IReadOnlyList<SqlColumnInfo> actualColumns)
    {
        var warnings = new List<string>(spec.Warnings);
        var byName = actualColumns.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

        var requested = spec.Columns;
        if (requested.Count == 0)
        {
            requested = actualColumns.Take(MaxColumns)
                .Select(c => new ParsedViewColumn(c.Name, null, null))
                .ToList();
        }

        var resolved = new List<ViewColumn>();
        foreach (var column in requested)
        {
            if (!byName.TryGetValue(column.Name, out var actual))
            {
                warnings.Add($"Colonne « {column.Name} » absente de la table : ignorée.");
                continue;
            }
            resolved.Add(new ViewColumn
            {
                Name = actual.Name,
                Label = string.IsNullOrWhiteSpace(column.Label) ? null : column.Label!.Trim(),
                Width = "full",
                Format = column.Format ?? actual.SuggestedFormat
            });
        }

        return (new ViewDefinition { Columns = resolved, Search = spec.Search }, warnings);
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
