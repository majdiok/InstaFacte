namespace FactuTrust.Application.Common.Fiscal;

/// <summary>
/// Mathématiques pures et déterministes d'exercice comptable (plan « Exercices décalés »,
/// décision D2) — source de vérité canonique partagée par le résolveur
/// (<c>IFiscalYearResolver</c>), le moteur d'amortissement (<c>DepreciationEngine</c>) et les
/// handlers (run, cession). Aucune dépendance ; <paramref name="fiscalYearStartMonth"/> = 1
/// correspond à l'exercice civil (comportement historique).
/// </summary>
public static class FiscalYearMath
{
    /// <summary>
    /// Clé logique d'exercice (int = année de début d'exercice) contenant la date donnée.
    /// Mois ≥ startMonth → année de la date ; sinon → année précédente. Avec startMonth = 1,
    /// retourne toujours <c>date.Year</c> (exercice civil = année civile).
    /// </summary>
    public static int Key(DateTime date, int fiscalYearStartMonth)
    {
        ValidateStartMonth(fiscalYearStartMonth);
        return date.Month >= fiscalYearStartMonth ? date.Year : date.Year - 1;
    }

    /// <summary>Date de début d'exercice (premier jour du mois de début), pour la clé donnée.</summary>
    public static DateTime StartDateTime(int fiscalYearKey, int fiscalYearStartMonth)
    {
        ValidateStartMonth(fiscalYearStartMonth);
        return new DateTime(fiscalYearKey, fiscalYearStartMonth, 1);
    }

    /// <summary>
    /// Date de fin d'exercice = dernier jour du mois précédant le mois de début de l'exercice
    /// suivant (gère les années bissextiles : 28/29 février). Pour un exercice civil (mois 1),
    /// retourne le 31/12 de l'année de la clé.
    /// </summary>
    public static DateTime EndDateTime(int fiscalYearKey, int fiscalYearStartMonth)
    {
        ValidateStartMonth(fiscalYearStartMonth);
        return new DateTime(fiscalYearKey + 1, fiscalYearStartMonth, 1).AddDays(-1);
    }

    /// <summary>
    /// Libellé d'affichage de l'exercice. Exercice civil (mois 1) → « N » (ex. « 2026 »).
    /// Exercice décalé : « N/N+1 » → « 2026/2027 » ; « N » → « 2026 » (décision D2).
    /// Toute autre valeur de format est traitée comme « N/N+1 ».
    /// </summary>
    public static string Label(int fiscalYearKey, int fiscalYearStartMonth, string labelFormat)
    {
        ValidateStartMonth(fiscalYearStartMonth);
        if (fiscalYearStartMonth == 1)
            return fiscalYearKey.ToString();
        if (string.Equals(labelFormat, "N", StringComparison.Ordinal))
            return fiscalYearKey.ToString();
        return $"{fiscalYearKey}/{fiscalYearKey + 1}";
    }

    private static void ValidateStartMonth(int fiscalYearStartMonth)
    {
        if (fiscalYearStartMonth is < 1 or > 12)
            throw new ArgumentOutOfRangeException(
                nameof(fiscalYearStartMonth),
                "Le mois de début d'exercice doit être compris entre 1 et 12.");
    }
}
