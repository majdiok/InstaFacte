using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services.AI;

/// <summary>Strips invalid navigation links from AI dashboard table rows (defense in depth with <see cref="ClientActionSanitizer"/>).</summary>
public static class DashboardConfigSanitizer
{
    public static JsonObject SanitizeSections(string title, JsonNode sectionsNode, ILogger? logger = null)
    {
        if (sectionsNode is not JsonArray sections)
            throw new ArgumentException("sections_json doit être un tableau JSON.", nameof(sectionsNode));

        foreach (var section in sections)
        {
            if (section is not JsonObject secObj)
                continue;
            if (!secObj.TryGetPropertyValue("type", out var typeNode) || typeNode is not JsonValue typeVal)
                continue;
            if (typeVal.GetValue<string>() != "table")
                continue;
            if (!secObj.TryGetPropertyValue("data", out var dataNode) || dataNode is not JsonObject dataObj)
                continue;
            if (!dataObj.TryGetPropertyValue("rows", out var rowsNode) || rowsNode is not JsonArray rows)
                continue;

            foreach (var row in rows)
            {
                if (row is not JsonObject rowObj)
                    continue;
                if (!rowObj.TryGetPropertyValue("_links", out var linksNode) || linksNode is not JsonObject links)
                    continue;

                var keysToRemove = new List<string>();
                foreach (var linkProp in links)
                {
                    var key = linkProp.Key;
                    if (linkProp.Value is not JsonObject linkObj)
                    {
                        keysToRemove.Add(key);
                        continue;
                    }

                    var json = linkObj.ToJsonString();
                    var result = ClientActionSanitizer.SanitizeNavigationLinkJson(json);
                    if (!result.Success || string.IsNullOrEmpty(result.Data))
                    {
                        logger?.LogWarning(
                            "Dashboard table row link stripped for column {Column}: {Reason}",
                            key,
                            result.ErrorMessage);
                        keysToRemove.Add(key);
                        continue;
                    }

                    using var parsed = JsonDocument.Parse(result.Data);
                    links[key] = JsonNode.Parse(parsed.RootElement.GetRawText())!;
                }

                foreach (var k in keysToRemove)
                    links.Remove(k);
                if (links.Count == 0)
                    rowObj.Remove("_links");
            }
        }

        // Cohérence widget ↔ texte : retirer toute section « table » sans lignes (rows vide ou absent).
        // Empêche l'affichage d'une carte « Aucune donnée » qui contredit une réponse texte renseignée.
        // Les sections KPI et graphiques ne sont jamais évaluées ici.
        for (int i = sections.Count - 1; i >= 0; i--)
        {
            if (sections[i] is JsonObject so
                && so.TryGetPropertyValue("type", out var typeNode) && typeNode is JsonValue typeVal
                && typeVal.TryGetValue<string>(out var typeStr) && typeStr == "table"
                && (!so.TryGetPropertyValue("data", out var dataNode) || dataNode is not JsonObject dataObj
                    || !dataObj.TryGetPropertyValue("rows", out var rowsNode) || rowsNode is not JsonArray rows
                    || rows.Count == 0))
            {
                logger?.LogDebug("Dashboard : section table vide supprimée (titre={Title}).", title);
                sections.RemoveAt(i);
            }
        }

        return new JsonObject
        {
            ["title"] = title,
            ["sections"] = sections
        };
    }
}
