using System.Text.RegularExpressions;

namespace FactuTrust.Infrastructure.Services.Email;

/// <summary>
/// Lot C2 — Moteur de templating maison minimaliste, sans dépendance externe.
///
/// Supporte une seule construction : <c>{{ key }}</c> → remplacé par la valeur du
/// dictionnaire. Les espaces autour de la clé sont tolérés. Les clés non définies
/// sont remplacées par chaîne vide (silencieux par design — l'éditeur de template
/// remarquera le rendu vide et ajustera).
///
/// Volontairement non-Scriban (vulnérabilités CVE 2024) ni Razor (overhead) :
/// les templates plateforme restent simples (substitution variables uniquement).
/// </summary>
public static class SimpleTemplateRenderer
{
    private static readonly Regex Pattern = new(@"\{\{\s*([\w\.\-]+)\s*\}\}", RegexOptions.Compiled);

    /// <summary>Rend un template en remplaçant chaque <c>{{ key }}</c> par la valeur correspondante.</summary>
    public static string Render(string template, IReadOnlyDictionary<string, string?> model)
    {
        if (string.IsNullOrEmpty(template)) return template ?? string.Empty;

        return Pattern.Replace(template, m =>
        {
            var key = m.Groups[1].Value.Trim();
            return model.TryGetValue(key, out var value) ? value ?? string.Empty : string.Empty;
        });
    }

    /// <summary>Variant pratique acceptant un dictionnaire d'objets ; appelle <c>ToString()</c> sur chaque valeur.</summary>
    public static string Render(string template, IReadOnlyDictionary<string, object?> model)
    {
        var stringModel = model.ToDictionary(
            kv => kv.Key,
            kv => kv.Value?.ToString());
        return Render(template, stringModel);
    }
}
