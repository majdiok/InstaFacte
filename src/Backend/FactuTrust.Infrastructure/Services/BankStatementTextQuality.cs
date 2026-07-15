namespace FactuTrust.Infrastructure.Services;

/// <summary>Heuristiques de qualité du texte extrait d'un relevé bancaire.</summary>
public static class BankStatementTextQuality
{
    private static readonly string[] Keywords =
    {
        "RIB", "SOLDE", "VIREMENT", "PAIEMENT", "ENCAISSEMENT", "REGLEMENT",
        "COMMISSION", "CHEQUE", "DINAR", "TND", "RELEVE", "RELEVÉ"
    };

    public static bool IsSufficient(string? text, int minChars = 200)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < minChars)
            return false;

        var upper = text.ToUpperInvariant();
        var hits = Keywords.Count(kw => upper.Contains(kw, StringComparison.Ordinal));
        return hits >= 3;
    }
}
