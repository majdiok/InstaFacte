namespace FactuTrust.Infrastructure.Services;

/// <summary>Infère le sens débit/crédit d'une ligne de relevé bancaire tunisien (BIAT…).</summary>
public static class BankOperationClassifier
{
    private static readonly string[] CreditKeywords =
    {
        "ENCAISSEMENT", "VIREMENT RECU", "VIREMENT REÇU", "VIREMENT ETRANGER REC",
        "VERSEMENT ESPECES", "DEBLOCAGE"
    };

    private static readonly string[] DebitKeywords =
    {
        "PAIEMENT EFFET", "REGLEMENT CHEQUE", "RÈGLEMENT CHEQUE", "REGLEMENT CHEQUE",
        "COM ET TVA", "COM TVA", "COMMISSION", "PRELEVEMENT", "PRÉLEVEMENT",
        "VIREMENT TN AUTRE BQ", "VIREMENT TN AUTRE", "BLOCAGE", "COM Q", "BQ A DISTANCE",
        "REDRESSEMENT", "DEBIT PAR CREATION", "ENG/SIGNATURE"
    };

    /// <summary>
    /// true = décaissement (débit banque), false = encaissement (crédit banque).
    /// </summary>
    public static bool IsDebit(string description)
    {
        var upper = description.ToUpperInvariant();

        foreach (var kw in CreditKeywords)
        {
            if (upper.Contains(kw, StringComparison.Ordinal))
                return false;
        }

        foreach (var kw in DebitKeywords)
        {
            if (upper.Contains(kw, StringComparison.Ordinal))
                return true;
        }

        // Virement même banque : ambigu — défaut crédit si « REC », sinon débit.
        if (upper.Contains("VIREMENT TN MEME BQ", StringComparison.Ordinal))
            return false;

        if (upper.Contains("VIREMENT", StringComparison.Ordinal))
            return true;

        return true;
    }
}
