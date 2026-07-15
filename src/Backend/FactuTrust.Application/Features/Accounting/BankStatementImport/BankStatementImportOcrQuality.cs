namespace FactuTrust.Application.Features.Accounting.BankStatementImport;

/// <summary>Heuristiques OCR pour relevés bancaires (complément à <see cref="AI.InvoiceImportOcrQuality"/>).</summary>
public static class BankStatementImportOcrQuality
{
    private static readonly string[] Keywords =
    {
        "RIB", "SOLDE", "VIREMENT", "PAIEMENT", "ENCAISSEMENT", "REGLEMENT", "RÈGLEMENT",
        "COMMISSION", "CHEQUE", "CHÈQUE", "DINAR", "TND", "RELEVE", "RELEVÉ", "BIAT", "STB", "BNA"
    };

    public static int Score(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        var upper = text.ToUpperInvariant();
        var hits = Keywords.Count(kw => upper.Contains(kw, StringComparison.Ordinal));
        var lengthScore = Math.Min(text.Length / 500, 10);
        return hits * 10 + lengthScore;
    }

    public static bool IsSufficient(string? text, int minScore = 25) => Score(text) >= minScore;
}
