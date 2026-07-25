using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common;

/// <summary>
/// Catalogue de référence (Tunisie) des lignes standard de réintégration et de déduction utilisées dans
/// le passage du résultat comptable au résultat fiscal. Sert à pré-remplir la feuille guidée ; le
/// comptable saisit les montants et peut ajouter des lignes libres. Purement indicatif — à valider selon
/// la législation en vigueur pour l'exercice concerné.
/// </summary>
public static class FiscalAdjustmentCatalog
{
    public sealed record Entry(string Code, FiscalAdjustmentKind Kind, string Label, string? Hint = null);

    public static IReadOnlyList<Entry> All { get; } = new List<Entry>
    {
        // ── Réintégrations (charges non déductibles / produits sous-évalués) ──────────────
        new("R-IS", FiscalAdjustmentKind.Reintegration, "Impôt sur les sociétés (compte 69)", "L'IS comptabilisé n'est pas déductible."),
        new("R-PENALITES", FiscalAdjustmentKind.Reintegration, "Amendes, pénalités et majorations de retard", "Non déductibles (pénalités fiscales, CNSS, contractuelles à caractère de sanction)."),
        new("R-PROV-NONDED", FiscalAdjustmentKind.Reintegration, "Provisions non déductibles", "Provisions non admises fiscalement (au-delà des limites/conditions légales)."),
        new("R-AMORT-EXCED", FiscalAdjustmentKind.Reintegration, "Amortissements excédentaires", "Fraction d'amortissement au-delà des taux fiscalement admis."),
        new("R-DONS", FiscalAdjustmentKind.Reintegration, "Dons et subventions hors limites", "Fraction excédant les plafonds déductibles."),
        new("R-RECEPTIONS", FiscalAdjustmentKind.Reintegration, "Cadeaux, réceptions et frais somptuaires hors limites", "Fraction non déductible."),
        new("R-CCA", FiscalAdjustmentKind.Reintegration, "Intérêts des comptes courants associés excédentaires", "Au-delà du taux/plafond légal."),
        new("R-RS-NON-EFF", FiscalAdjustmentKind.Reintegration, "Charges dont la retenue à la source n'a pas été effectuée", "Réintégration des montants non soumis à RS obligatoire."),
        new("R-CHARGES-NONJUST", FiscalAdjustmentKind.Reintegration, "Charges non justifiées ou non liées à l'exploitation", null),
        new("R-AUTRE", FiscalAdjustmentKind.Reintegration, "Autres réintégrations", "Ligne libre de réintégration."),

        // ── Déductions (produits non imposables / charges déductibles complémentaires) ─────
        new("D-PV-EXO", FiscalAdjustmentKind.Deduction, "Plus-values exonérées", "Plus-values bénéficiant d'une exonération (conditions légales)."),
        new("D-DIVIDENDES", FiscalAdjustmentKind.Deduction, "Dividendes et revenus déjà soumis à l'impôt", null),
        new("D-REPRISE-PROV", FiscalAdjustmentKind.Deduction, "Reprises de provisions antérieurement réintégrées", null),
        new("D-DEGREV-REINVEST", FiscalAdjustmentKind.Deduction, "Dégrèvements financiers / physiques (réinvestissement)", "Déductions pour réinvestissement, dans les limites légales."),
        new("D-PRODUITS-NONIMP", FiscalAdjustmentKind.Deduction, "Autres produits non imposables", null),
        new("D-AUTRE", FiscalAdjustmentKind.Deduction, "Autres déductions", "Ligne libre de déduction.")
    };

    public static Entry? Find(string? code) =>
        string.IsNullOrWhiteSpace(code) ? null : All.FirstOrDefault(e => e.Code == code);
}
