using System.Globalization;
using System.Text;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Aides génériques partagées par les parsers de fichiers tabulaires (reprise de dossier,
/// relevés bancaires) : décodage, détection de délimiteur, normalisation d'en-têtes,
/// dates et montants au format FR/TN. Extraites de <see cref="JournalImportParser"/> —
/// comportement strictement identique.
/// </summary>
internal static class TabularFileParsing
{
    public static string DecodeText(byte[] content)
    {
        // UTF-8 (avec ou sans BOM). StreamReader gère le BOM automatiquement.
        using var ms = new MemoryStream(content);
        using var reader = new StreamReader(ms, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    public static string DetectDelimiter(string text)
    {
        var firstLine = text.Split('\n').FirstOrDefault() ?? string.Empty;
        var semi = firstLine.Count(c => c == ';');
        var comma = firstLine.Count(c => c == ',');
        var tab = firstLine.Count(c => c == '\t');
        if (tab >= semi && tab >= comma && tab > 0) return "\t";
        return semi > comma ? ";" : ",";
    }

    /// <summary>Normalise un en-tête : minuscules, sans accents, lettres/chiffres uniquement.</summary>
    public static string Normalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s.Normalize(NormalizationForm.FormD))
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
        }
        return sb.ToString();
    }

    /// <summary>
    /// Associe chaque colonne canonique à son index dans l'en-tête (−1 si absente),
    /// en comparant les en-têtes normalisés aux synonymes fournis.
    /// </summary>
    public static Dictionary<string, int> ResolveColumns(
        IReadOnlyList<string> header,
        IReadOnlyDictionary<string, string[]> columnSynonyms)
    {
        var normalized = header.Select((h, i) => (Key: Normalize(h), Index: i)).ToList();
        var map = new Dictionary<string, int>();
        foreach (var (canonical, synonyms) in columnSynonyms)
        {
            var idx = -1;
            foreach (var (key, index) in normalized)
            {
                if (synonyms.Contains(key)) { idx = index; break; }
            }
            map[canonical] = idx;
        }
        return map;
    }

    private static readonly string[] DateFormats =
    {
        "yyyyMMdd", "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "yyyy/MM/dd", "dd.MM.yyyy"
    };

    public static DateTime? TryParseDate(string value)
    {
        value = value?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(value)) return null;
        if (DateTime.TryParseExact(value, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return d.Date;
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
            return d.Date;
        if (DateTime.TryParse(value, new CultureInfo("fr-FR"), DateTimeStyles.None, out d))
            return d.Date;
        return null;
    }

    public static bool TryParseAmount(string value, out decimal amount)
    {
        amount = 0m;
        value = value?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(value)) return true; // vide = 0
        // Espaces simples ET insécables (séparateurs de milliers FR).
        value = value.Replace(" ", string.Empty).Replace(" ", string.Empty);

        var hasDot = value.Contains('.');
        var hasComma = value.Contains(',');
        if (hasDot && hasComma)
        {
            // Séparateur de milliers + décimale : la virgule est traitée comme millier.
            value = value.Replace(",", string.Empty);
        }
        else if (hasComma)
        {
            // Virgule décimale (usage FR/TN).
            value = value.Replace(',', '.');
        }

        return decimal.TryParse(value, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out amount);
    }
}
