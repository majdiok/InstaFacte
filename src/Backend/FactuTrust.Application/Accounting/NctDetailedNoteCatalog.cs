namespace FactuTrust.Application.Accounting;

/// <summary>Famille d'annexe NCT pour le dialogue d'export filtré.</summary>
public enum NctAnnexFamily
{
    Actif = 0,
    Passif = 1,
    IncomeStatement = 2,
    CashFlow = 3
}

/// <summary>
/// Convention de signe pour présenter le solde net (débit − crédit) dans une note détaillée.
/// </summary>
public enum NctNoteAmountSign
{
    /// <summary>Actif / charges / liquidités : montant = net.</summary>
    AsNet = 0,
    /// <summary>Passif / produits / concours : montant = −net.</summary>
    NegateNet = 1
}

/// <summary>Définition statique d'une note détaillée (prédicat de comptes exclusif au sein du catalogue).</summary>
public sealed record NctDetailedNoteDefinition(
    int Number,
    string Title,
    NctAnnexFamily Family,
    Func<string, bool> AccountPredicate,
    NctNoteAmountSign AmountSign = NctNoteAmountSign.AsNet);

/// <summary>
/// Catalogue versionné des notes annexes détaillées NCT (SCE).
/// Les prédicats sont conçus pour être exclusifs : un compte ne matche qu'une seule note.
/// </summary>
public static class NctDetailedNoteCatalog
{
    public static IReadOnlyList<NctDetailedNoteDefinition> All { get; } = BuildCatalog();

    public static IReadOnlyList<NctDetailedNoteDefinition> ForFamily(NctAnnexFamily family) =>
        All.Where(d => d.Family == family).ToList();

    private static IReadOnlyList<NctDetailedNoteDefinition> BuildCatalog() =>
        new List<NctDetailedNoteDefinition>
        {
            // ── Annexes actif ────────────────────────────────────────────────
            new(1, "Immobilisations incorporelles", NctAnnexFamily.Actif, a => Starts(a, "21")),
            new(3, "Immobilisations corporelles", NctAnnexFamily.Actif,
                a => Starts(a, "22") || Starts(a, "23") || Starts(a, "24") || Starts(a, "25")),
            new(4, "Amortissements", NctAnnexFamily.Actif, a => Starts(a, "28")),
            new(5, "Dépréciations des immobilisations", NctAnnexFamily.Actif, a => Starts(a, "29")),
            new(6, "Stocks", NctAnnexFamily.Actif, a => Cls(a) == 3),
            new(7, "Clients et comptes rattachés", NctAnnexFamily.Actif, a => Starts(a, "41")),
            new(8, "Autres actifs courants", NctAnnexFamily.Actif,
                a => Cls(a) == 4 && !Starts(a, "40") && !Starts(a, "41") && !Starts(a, "16")),

            // ── Annexes passif ───────────────────────────────────────────────
            new(10, "Capital", NctAnnexFamily.Passif, a => Starts(a, "10"), NctNoteAmountSign.NegateNet),
            new(11, "Réserves", NctAnnexFamily.Passif,
                a => Starts(a, "11") || Starts(a, "14"), NctNoteAmountSign.NegateNet),
            new(12, "Résultats reportés", NctAnnexFamily.Passif, a => Starts(a, "12"), NctNoteAmountSign.NegateNet),
            new(13, "Emprunts et dettes assimilées", NctAnnexFamily.Passif, a => Starts(a, "16"), NctNoteAmountSign.NegateNet),
            new(14, "Fournisseurs et comptes rattachés", NctAnnexFamily.Passif, a => Starts(a, "40"), NctNoteAmountSign.NegateNet),
            new(15, "Autres dettes", NctAnnexFamily.Passif,
                a => Cls(a) == 1 && !Starts(a, "10") && !Starts(a, "11") && !Starts(a, "12") && !Starts(a, "14") && !Starts(a, "16"),
                NctNoteAmountSign.NegateNet),

            // ── Annexes compte de résultat ───────────────────────────────────
            new(20, "Charges", NctAnnexFamily.IncomeStatement, a => Cls(a) == 6),
            new(21, "Produits", NctAnnexFamily.IncomeStatement, a => Cls(a) == 7, NctNoteAmountSign.NegateNet),

            // ── Annexes flux de trésorerie ───────────────────────────────────
            // Classe 5 réservée aux annexes flux (exclusif vs note 8 actif).
            new(30, "Liquidités et équivalents", NctAnnexFamily.CashFlow, a => Cls(a) == 5),
        };

    private static bool Starts(string account, string prefix) =>
        !string.IsNullOrEmpty(account) && account.StartsWith(prefix, StringComparison.Ordinal);

    private static int Cls(string account) =>
        !string.IsNullOrEmpty(account) && char.IsDigit(account[0]) ? account[0] - '0' : 0;
}
