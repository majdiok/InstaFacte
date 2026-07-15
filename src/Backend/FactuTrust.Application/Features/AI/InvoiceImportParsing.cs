using System.Globalization;
using FactuTrust.Application.Features.AI.DTOs;

namespace FactuTrust.Application.Features.AI;

/// <summary>
/// Fonctions pures de parsing, normalisation et rapprochement pour l'import de
/// facture depuis un fichier (PDF, image, Word, Excel). Isolées du handler
/// <see cref="Commands.ImportInvoiceFromFileHandler"/> afin d'être testables
/// unitairement, à l'image de <see cref="ModelRef"/> et
/// <see cref="AiModelCapabilityDetector"/>.
/// </summary>
public static class InvoiceImportParsing
{
    /// <summary>
    /// Seuil de similarité [0..1] retenu pour rapprocher un nom de client ou de
    /// produit extrait d'un enregistrement existant.
    /// </summary>
    public const double NameMatchThreshold = 0.9;

    /// <summary>
    /// Isole le premier objet JSON équilibré d'une réponse pouvant contenir du
    /// texte parasite ou des clôtures markdown autour du JSON.
    /// </summary>
    public static string? ExtractFirstJsonObject(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var start = raw.IndexOf('{');
        if (start < 0) return null;

        var depth = 0;
        var inString = false;
        var escape = false;

        for (var i = start; i < raw.Length; i++)
        {
            var c = raw[i];
            if (inString)
            {
                if (escape) escape = false;
                else if (c == '\\') escape = true;
                else if (c == '"') inString = false;
            }
            else
            {
                if (c == '"') inString = true;
                else if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0) return raw.Substring(start, i - start + 1);
                }
            }
        }

        return null;
    }

    /// <summary>Renvoie la chaîne nettoyée (trim) ou null si elle est vide ou blanche.</summary>
    public static string? CleanOrNull(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>
    /// Analyse une date issue du LLM (ISO "AAAA-MM-JJ" ou formats courants comme
    /// "JJ/MM/AAAA"). Renvoie null si la valeur est absente ou non reconnue.
    /// </summary>
    public static DateOnly? ParseDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw.Trim();

        if (DateOnly.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var iso))
            return iso;

        string[] formats = { "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "MM/dd/yyyy" };
        if (DateOnly.TryParseExact(s, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
            return exact;

        return null;
    }

    /// <summary>
    /// Ramène un taux de TVA au taux tunisien légal le plus proche (0, 7, 13 ou 19).
    /// Un taux absent est considéré comme le taux normal (19 %).
    /// </summary>
    public static int ClampVatRate(int? rate)
    {
        if (rate is null) return 19;

        int[] allowed = { 0, 7, 13, 19 };
        var best = 19;
        var bestDiff = int.MaxValue;
        foreach (var a in allowed)
        {
            var diff = Math.Abs(a - rate.Value);
            if (diff < bestDiff)
            {
                bestDiff = diff;
                best = a;
            }
        }
        return best;
    }

    /// <summary>
    /// Calcule une fenêtre de contexte (num_ctx) adaptée à la taille réelle du prompt
    /// d'import, bornée à [4096, configuredMaxNumCtx]. Réduit le coût de chargement et de
    /// prefill pour les factures courtes, sans jamais dépasser la valeur configurée
    /// (donc sans régression). Renvoie null si num_ctx n'est pas configuré (on laisse
    /// Ollama décider) ; renvoie la pleine fenêtre si des images sont jointes (modèle
    /// vision, qui consomme beaucoup de tokens).
    /// </summary>
    public static int? ResolveImportNumCtx(
        int configuredMaxNumCtx, int promptChars, int maxOutputTokens, bool hasImages)
    {
        if (configuredMaxNumCtx <= 0) return null;
        if (hasImages) return configuredMaxNumCtx;

        var approxInputTokens = Math.Max(0, promptChars) / 3;          // ~3 caractères/token (prudent)
        var needed = approxInputTokens + Math.Max(1, maxOutputTokens) + 512;
        var rounded = ((needed + 2047) / 2048) * 2048;                 // multiple de 2048 supérieur
        var lowerBound = Math.Min(4096, configuredMaxNumCtx);          // garde-fou si num_ctx configuré < 4096
        return Math.Clamp(rounded, lowerBound, configuredMaxNumCtx);
    }

    /// <summary>Normalise la devise en "TND", "EUR" ou "USD" ("TND" par défaut).</summary>
    public static string NormalizeCurrency(string? raw)
    {
        var c = (raw ?? string.Empty).Trim().ToUpperInvariant();
        return c is "TND" or "EUR" or "USD" ? c : "TND";
    }

    /// <summary>Vrai si le LLM a qualifié le document de non-facture explicite.</summary>
    public static bool IsUnknownDocumentType(string? raw)
    {
        var t = (raw ?? string.Empty).Trim().ToUpperInvariant();
        return t is "UNKNOWN";
    }

    public static bool IsDeliveryNoteDocumentType(string? raw) =>
        string.Equals((raw ?? string.Empty).Trim(), "DELIVERY_NOTE", StringComparison.OrdinalIgnoreCase);

    public static bool IsProformaDocumentType(string? raw) =>
        string.Equals((raw ?? string.Empty).Trim(), "PROFORMA", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Normalise le type de document : INVOICE, CREDIT_NOTE, DELIVERY_NOTE, PROFORMA.
    /// UNKNOWN et valeurs inconnues → INVOICE (avec avertissement côté handler).
    /// </summary>
    public static string NormalizeDocumentType(string? raw)
    {
        var t = (raw ?? string.Empty).Trim().ToUpperInvariant();
        return t switch
        {
            "CREDIT_NOTE" => "CREDIT_NOTE",
            "DELIVERY_NOTE" => "DELIVERY_NOTE",
            "PROFORMA" => "PROFORMA",
            _ => "INVOICE"
        };
    }

    /// <summary>Normalise l'auto-évaluation du LLM en "high", "medium" ou "low" ("medium" par défaut).</summary>
    public static string NormalizeConfidence(string? raw)
    {
        var c = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return c is "high" or "medium" or "low" ? c : "medium";
    }

    /// <summary>
    /// Convertit une ligne brute du LLM en ligne de facture validée et normalisée
    /// (quantité, prix HT, remise et TVA bornés). Renvoie null si la désignation
    /// est absente : la ligne doit alors être ignorée.
    /// </summary>
    public static InvoiceImportLineDto? MapLine(LlmInvoiceLine? line)
    {
        if (line is null) return null;

        var designation = (line.Designation ?? string.Empty).Trim();
        if (designation.Length == 0) return null;

        return new InvoiceImportLineDto
        {
            Designation = designation,
            Description = CleanOrNull(line.Description),
            Quantity = line.Quantity is > 0m ? line.Quantity!.Value : 1m,
            Unit = CleanOrNull(line.Unit),
            UnitPriceHT = line.UnitPriceHT is >= 0m ? line.UnitPriceHT!.Value : 0m,
            DiscountPercent = line.DiscountPercent.HasValue
                ? Math.Clamp(line.DiscountPercent.Value, 0m, 100m)
                : null,
            VatRatePercent = ClampVatRate(line.VatRatePercent)
        };
    }

    /// <summary>Réduit un matricule fiscal à ses seuls caractères alphanumériques en majuscules.</summary>
    public static string NormalizeNif(string? nif) =>
        string.IsNullOrEmpty(nif)
            ? string.Empty
            : new string(nif.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    /// <summary>Similarité [0..1] basée sur la distance de Levenshtein normalisée (insensible à la casse).</summary>
    public static double Similarity(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return 0d;
        var x = a.Trim().ToLowerInvariant();
        var y = b.Trim().ToLowerInvariant();
        if (x == y) return 1d;

        var maxLen = Math.Max(x.Length, y.Length);
        if (maxLen == 0) return 1d;
        return 1d - (double)Levenshtein(x, y) / maxLen;
    }

    private static int Levenshtein(string a, string b)
    {
        var n = a.Length;
        var m = b.Length;
        if (n == 0) return m;
        if (m == 0) return n;

        var prev = new int[m + 1];
        var curr = new int[m + 1];
        for (var j = 0; j <= m; j++) prev[j] = j;

        for (var i = 1; i <= n; i++)
        {
            curr[0] = i;
            for (var j = 1; j <= m; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(Math.Min(prev[j] + 1, curr[j - 1] + 1), prev[j - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }

        return prev[m];
    }
}
