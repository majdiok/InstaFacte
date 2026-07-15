namespace FactuTrust.Application.Features.AI;

/// <summary>
/// Heuristiques de qualité du texte OCR pour décider d'un fallback vision.
/// </summary>
public static class InvoiceImportOcrQuality
{
    private static readonly string[] DocumentKeywords =
    [
        "facture", "invoice", "bon", "livraison", "client", "total", "tva", "ht", "ttc",
        "montant", "prix", "quantit", "designation", "destinataire", "matricule"
    ];

    /// <summary>Score [0..1] : longueur, alphanum, mots-clés document.</summary>
    public static double Score(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        var t = text.Trim();
        var len = t.Length;
        if (len < 10)
            return 0.05;

        var alnum = t.Count(char.IsLetterOrDigit);
        var alnumRatio = (double)alnum / len;
        var lenScore = Math.Min(1.0, len / 200.0);
        var keywordHits = DocumentKeywords.Count(kw =>
            t.Contains(kw, StringComparison.OrdinalIgnoreCase));
        var keywordScore = Math.Min(1.0, keywordHits / 3.0);

        return Math.Clamp(alnumRatio * 0.4 + lenScore * 0.35 + keywordScore * 0.25, 0, 1);
    }

    public static bool IsSufficient(string? text, int minChars, double minScore)
    {
        var trimmed = (text ?? string.Empty).Trim();
        if (trimmed.Length >= minChars && Score(trimmed) >= minScore)
            return true;
        return trimmed.Length >= minChars * 2 && Score(trimmed) >= minScore * 0.7;
    }
}
