namespace FactuTrust.Application.Features.Accounting.Audit;

/// <summary>
/// Quelles règles produisent un <c>Amount</c> qui représente un <b>impact chiffré en dinars</b>, et
/// lesquelles s'en servent seulement comme ordre de grandeur.
///
/// <para><b>Pourquoi cette distinction est nécessaire.</b> <c>AccountingAnomaly.Amount</c> est un
/// simple décimal, rempli au gré de chaque règle. Certaines y mettent un vrai enjeu financier
/// (l'écart de TVA, le montant du doublon). D'autres y mettent zéro faute de mieux (écritures en
/// brouillard, trous de numérotation, périodes ouvertes). D'autres encore y mettent une somme de
/// débits et crédits, qui compte donc deux fois le même flux — utile pour trier, absurde à
/// additionner.</para>
///
/// <para>Sommer aveuglément ces trois familles produirait un « impact total » faux, affiché en tête
/// du dossier de révision et repris dans la note. On ne totalise donc que les codes déclarés ici.
/// Toute règle absente de la liste voit son impact présenté comme <b>non chiffrable</b> — ce qui
/// est une information honnête, pas une lacune.</para>
///
/// <para>Ajouter une règle à cette liste est une décision comptable, pas technique : il faut que
/// son montant réponde à « combien coûte cette anomalie si elle n'est pas corrigée ? ».</para>
/// </summary>
public static class AuditAmountSemantics
{
    /// <summary>
    /// Règles dont <c>Amount</c> est un impact chiffré additionnable.
    /// </summary>
    private static readonly HashSet<string> MonetaryImpactRules = new(StringComparer.Ordinal)
    {
        // Socle historique.
        "suspense",                    // solde du compte d'attente à régulariser
        "unbalanced",                  // somme des déséquilibres
        "depreciation",                // dotations non comptabilisées
        "vat-deductible-no-proof",     // TVA déductible menacée de rejet

        // Réviseur — famille Comptable.
        "entry-vat-vs-document",       // écart de TVA entre document et comptabilité
        "reversal-missing",            // montant resté à tort dans les comptes

        // Réviseur — famille Documentaire.
        "supplier-invoice-no-proof",   // charges dont la déduction est menacée
        "supplier-invoice-duplicate",  // montant comptabilisé en trop
        "purchase-price-drift",        // surcoût facturé par rapport à la commande

        // Réviseur — famille Trésorerie.
        "cash-negative",               // découvert de caisse au plus bas
        "cash-in-without-invoice",     // encaissements sans justification

        // Réviseur — famille Fiscale.
        "withholding-missing-on-fees", // base des factures dont la retenue est due
        "fodec-missing",               // écart de FODEC non comptabilisé

        // Réviseur — famille Paie.
        "payroll-cnss-regime-mismatch",  // cotisations à régulariser
        "payroll-overtime-out-of-regime", // majorations sans fondement
        "payroll-below-smig"             // rappel de salaire à prévoir

        // Volontairement ABSENTS — leur montant n'est pas un enjeu financier :
        //   vat-period-not-closed        (0 : c'est un défaut de procédure)
        //   payroll-dependent-no-proof   (0 : l'impact IRPP dépend du barème individuel)
        //   self-validation              (0 : défaut de contrôle interne, pas de perte chiffrable)
        //   off-hours-entry              (0 : signal de contexte)
        //   backdated-entry              (0 : signal de contexte)
        //   threshold-structuring        (le total des factures n'est PAS une perte : la dépense
        //                                 est réelle, seule la retenue éventuelle serait en jeu)
        //   supplier-created-then-paid   (idem : le règlement n'est pas une perte en soi)
    };

    /// <summary>Vrai si le montant de cette règle peut entrer dans un total en dinars.</summary>
    public static bool HasMonetaryImpact(string? ruleCode) =>
        !string.IsNullOrWhiteSpace(ruleCode) && MonetaryImpactRules.Contains(ruleCode);

    /// <summary>Montant à totaliser pour une anomalie, ou <c>null</c> s'il n'est pas chiffrable.</summary>
    public static decimal? ImpactOf(string? ruleCode, decimal amount) =>
        HasMonetaryImpact(ruleCode) ? amount : null;

    /// <summary>
    /// Total des impacts chiffrables d'un lot d'anomalies. Les non chiffrables sont ignorées, pas
    /// comptées pour zéro : la nuance importe quand on présente le total au réviseur.
    /// </summary>
    public static decimal SumImpacts(IEnumerable<(string RuleCode, decimal Amount)> anomalies) =>
        anomalies
            .Where(a => HasMonetaryImpact(a.RuleCode))
            .Sum(a => a.Amount);
}
