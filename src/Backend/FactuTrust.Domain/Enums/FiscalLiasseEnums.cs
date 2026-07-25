namespace FactuTrust.Domain.Enums;

/// <summary>Type de contribuable pour la détermination du résultat fiscal.</summary>
public enum TaxpayerKind
{
    /// <summary>Société soumise à l'impôt sur les sociétés (IS).</summary>
    CorporateIS = 0,
    /// <summary>Personne physique soumise à l'IRPP au titre des BIC (régime réel).</summary>
    IndividualIrppBic = 1
}

/// <summary>Sens d'un ajustement fiscal du passage résultat comptable → résultat fiscal.</summary>
public enum FiscalAdjustmentKind
{
    /// <summary>Réintégration (charge non déductible ajoutée au résultat).</summary>
    Reintegration = 0,
    /// <summary>Déduction (produit non imposable / charge déductible retranchée du résultat).</summary>
    Deduction = 1
}

/// <summary>Nature d'un élément reportable imputable sur le résultat fiscal.</summary>
public enum FiscalCarryForwardKind
{
    /// <summary>Déficit reportable (report limité, cf. DeficitCarryForwardYears).</summary>
    Deficit = 0,
    /// <summary>Amortissement réputé différé (report illimité).</summary>
    DeferredDepreciation = 1
}

/// <summary>
/// Régime de minimum d'impôt applicable à la déclaration (art. 49 CIS / 44 CIRPP).
/// </summary>
public enum MinimumTaxRegime
{
    /// <summary>Régime de droit commun : max(taux standard × CA local TTC, plancher standard).</summary>
    Standard = 0,
    /// <summary>Régime réduit : max(taux réduit × CA local TTC, plancher réduit).</summary>
    Reduced = 1,
    /// <summary>Exonéré : aucun minimum d'impôt (société nouvellement créée, ZDR, totalement exportatrice…).</summary>
    Exempt = 2
}

/// <summary>Statut d'une feuille de détermination du résultat fiscal.</summary>
public enum FiscalDeclarationStatus
{
    /// <summary>Brouillon éditable.</summary>
    Draft = 0,
    /// <summary>Finalisée (non éditable sans réouverture).</summary>
    Finalized = 1
}
