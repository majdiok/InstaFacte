using System.Text;
using System.Text.Json;

namespace FactuTrust.Application.Features.AI.Json;

/// <summary>
/// Rend exploitable une <see cref="JsonException"/> issue d'une sortie de LLM.
///
/// <para>Motivation concrète : l'aperçu tronqué à 500 caractères journalisé jusqu'ici s'arrêtait
/// systématiquement AVANT le champ fautif (en production, faute à la ligne 35 du JSON, aperçu coupé
/// à la ligne 22). Le diagnostic était donc inutilisable alors que l'exception portait déjà
/// <see cref="JsonException.Path"/>, <see cref="JsonException.LineNumber"/> et
/// <see cref="JsonException.BytePositionInLine"/>.</para>
/// </summary>
public static class LlmJsonDiagnostics
{
    private const int DefaultWindow = 240;

    /// <summary>Résumé d'une ligne : chemin, position, et extrait du JSON autour de la faute.</summary>
    public static string Describe(string? json, JsonException? error, int window = DefaultWindow)
    {
        if (error is null)
            return "aucun objet JSON équilibré n'a pu être isolé (réponse tronquée ou sans JSON)";

        var head = $"path={error.Path ?? "?"} ligne={error.LineNumber?.ToString() ?? "?"} "
                   + $"col={error.BytePositionInLine?.ToString() ?? "?"}";

        var excerpt = Excerpt(json, error.LineNumber, error.BytePositionInLine, window);
        return excerpt is null ? head : $"{head} — extrait: …{excerpt}…";
    }

    /// <summary>
    /// Extrait une fenêtre de texte centrée sur la position de la faute.
    ///
    /// <para><b>Attention</b> : <see cref="JsonException.LineNumber"/> et
    /// <see cref="JsonException.BytePositionInLine"/> sont exprimés en OCTETS, pas en caractères.
    /// Sur une facture française (« Société », « n° »), un découpage par index de chaîne viserait à
    /// côté — d'où le travail sur les octets UTF-8.</para>
    /// </summary>
    public static string? Excerpt(string? json, long? lineNumber, long? bytePositionInLine, int window)
    {
        if (string.IsNullOrEmpty(json) || lineNumber is null)
            return null;

        var utf8 = Encoding.UTF8.GetBytes(json);

        long offset = 0;
        long line = 0;
        while (offset < utf8.Length && line < lineNumber.Value)
        {
            if (utf8[offset] == (byte)'\n')
                line++;
            offset++;
        }

        offset = Math.Min(utf8.Length, offset + Math.Max(0, bytePositionInLine ?? 0));

        var safeWindow = Math.Clamp(window, 40, 2_000);
        var start = (int)Math.Max(0, offset - safeWindow / 2);
        var length = Math.Min(utf8.Length - start, safeWindow);
        if (length <= 0)
            return null;

        // Un caractère multi-octets coupé aux bords devient U+FFFD : acceptable dans un journal.
        return Encoding.UTF8.GetString(utf8, start, length).ReplaceLineEndings(" ").Trim();
    }
}
