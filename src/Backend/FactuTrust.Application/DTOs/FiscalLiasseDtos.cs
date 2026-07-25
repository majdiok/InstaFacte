namespace FactuTrust.Application.DTOs;

/// <summary>Ligne de réintégration/déduction (Kind : 0 = réintégration, 1 = déduction).</summary>
public sealed record FiscalAdjustmentLineDto
{
    public string? CatalogCode { get; init; }
    public int Kind { get; init; }
    public string Label { get; init; } = null!;
    public decimal Amount { get; init; }
    public bool IsAutoSuggested { get; init; }
}

/// <summary>Élément reportable (Kind : 0 = déficit, 1 = amortissement différé).</summary>
public sealed record FiscalCarryForwardDto
{
    public int Kind { get; init; }
    public int OriginYear { get; init; }
    public decimal InitialAmount { get; init; }
    public decimal ImputedThisYear { get; init; }
    public int? ExpiryYear { get; init; }
}

/// <summary>Détail du calcul de l'impôt (chaque étape du passage comptable → net à payer).</summary>
public sealed record IncomeTaxComputationDto
{
    public decimal AccountingResult { get; init; }
    public decimal TotalReintegrations { get; init; }
    public decimal TotalDeductions { get; init; }
    /// <summary>Résultat fiscal avant imputation des reports.</summary>
    public decimal ResultBeforeCarryForward { get; init; }
    public decimal DeficitsImputed { get; init; }
    public decimal DeferredDepreciationImputed { get; init; }
    /// <summary>Résultat fiscal imposable (≥ 0).</summary>
    public decimal TaxableResult { get; init; }
    /// <summary>Déficit généré par l'exercice (si résultat négatif), reportable.</summary>
    public decimal DeficitGeneratedThisYear { get; init; }
    public int TaxpayerKind { get; init; }
    public decimal AppliedIsRate { get; init; }
    /// <summary>Impôt calculé sur le résultat imposable (IS = taux ; IRPP = barème).</summary>
    public decimal TaxOnResult { get; init; }
    /// <summary>Régime de minimum d'impôt retenu (0 = droit commun, 1 = réduit, 2 = exonéré).</summary>
    public int MinimumTaxRegime { get; init; }
    public decimal MinimumTax { get; init; }
    /// <summary>Impôt dû = max(impôt calculé, minimum d'impôt).</summary>
    public decimal TaxDue { get; init; }
    public decimal Css { get; init; }
    public decimal TotalTaxDue { get; init; }
    public decimal AcomptesPaid { get; init; }
    public decimal WithholdingSuffered { get; init; }
    public decimal PriorTaxCredit { get; init; }
    /// <summary>Net à payer (0 si crédit).</summary>
    public decimal NetToPay { get; init; }
    /// <summary>Crédit d'impôt à reporter (0 si net à payer).</summary>
    public decimal CreditToCarry { get; init; }
}

/// <summary>Feuille de détermination du résultat fiscal + calcul de l'impôt, pour un exercice.</summary>
public sealed record FiscalResultDeclarationDto
{
    public int FiscalYear { get; init; }
    public int TaxpayerKind { get; init; }
    public int Status { get; init; }
    /// <summary>
    /// Résultat comptable <b>net (après impôt)</b> repris de l'état de résultat. L'IS comptabilisé
    /// (compte 69) est réintégré via une ligne d'ajustement — ne pas y saisir un résultat avant impôt.
    /// </summary>
    public decimal AccountingResult { get; init; }
    public decimal AppliedIsRate { get; init; }
    public decimal LocalTurnoverTtc { get; init; }
    /// <summary>Régime de minimum d'impôt (0 = droit commun, 1 = réduit, 2 = exonéré).</summary>
    public int MinimumTaxRegime { get; init; }
    /// <summary>
    /// CA local TTC calculé depuis la comptabilité (produits classe 70 + TVA collectée), proposé comme
    /// aide à la saisie de la base du minimum d'impôt. Le montant saisi par le comptable prime.
    /// </summary>
    public decimal SuggestedLocalTurnoverTtc { get; init; }
    public decimal AcomptesPaid { get; init; }
    public decimal WithholdingSuffered { get; init; }
    public decimal PriorTaxCredit { get; init; }
    public IReadOnlyList<FiscalAdjustmentLineDto> Adjustments { get; init; } = Array.Empty<FiscalAdjustmentLineDto>();
    public IReadOnlyList<FiscalCarryForwardDto> CarryForwards { get; init; } = Array.Empty<FiscalCarryForwardDto>();
    public IncomeTaxComputationDto Computation { get; init; } = new();
    public bool IsFinalized { get; init; }
    public DateTime? FinalizedAt { get; init; }
    /// <summary>Feuille jamais enregistrée (aperçu initial avec suggestions auto).</summary>
    public bool IsNew { get; init; }
    public bool FiscalLiasseEnabled { get; init; }
    /// <summary>
    /// Avertissements non bloquants issus des contrôles fiscaux (déficit prescrit non imputé,
    /// imputation plafonnée au stock disponible…).
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Paramètres fiscaux d'un exercice (taux IS, minimum d'impôt, CSS, acomptes, reports, barème IRPP).
/// Valeurs indicatives à valider selon la loi de finances applicable.
/// </summary>
public sealed record IncomeTaxYearParameterDto
{
    public int FiscalYear { get; init; }
    public decimal IsStandardRate { get; init; }
    public decimal IsReducedRate { get; init; }
    public decimal IsSectorRate { get; init; }
    public decimal MinTaxRate { get; init; }
    public decimal MinTaxReducedRate { get; init; }
    public decimal MinTaxFloorTnd { get; init; }
    public decimal MinTaxFloorReducedTnd { get; init; }
    public bool CssApplies { get; init; }
    public decimal CssRate { get; init; }
    public decimal CssFloorTnd { get; init; }
    public decimal AcompteRate { get; init; }
    public int AcompteCount { get; init; }
    public int DeficitCarryForwardYears { get; init; }
    public bool RoundTaxableToDinar { get; init; }
    public string IrppBracketsJson { get; init; } = "[]";
    /// <summary>Vrai si le comptable a déjà validé/modifié ces paramètres (défauts non réappliqués).</summary>
    public bool IsUserModified { get; init; }
}

/// <summary>Entrée du catalogue des lignes standard (Kind : 0 = réintégration, 1 = déduction).</summary>
public sealed record FiscalAdjustmentCatalogEntryDto
{
    public string Code { get; init; } = null!;
    public int Kind { get; init; }
    public string Label { get; init; } = null!;
    public string? Hint { get; init; }
}

/// <summary>Ligne générique d'un tableau annexe de la liasse (amortissements, provisions).</summary>
public sealed record FiscalTableRowDto
{
    public string Code { get; init; } = null!;
    public string Label { get; init; } = null!;
    public decimal Amount { get; init; }
    public decimal? PreviousAmount { get; init; }
}

/// <summary>
/// Liasse consolidée : états financiers NCT + détermination du résultat fiscal + tableaux annexes.
/// Alimente l'export « liasse complète » (PDF/Excel).
/// </summary>
public sealed record ConsolidatedLiasseDto
{
    public int FiscalYear { get; init; }
    public NctFinancialStatementsDto FinancialStatements { get; init; } = new();
    public FiscalResultDeclarationDto FiscalResult { get; init; } = new();
    public IReadOnlyList<FiscalTableRowDto> AmortizationTable { get; init; } = Array.Empty<FiscalTableRowDto>();
    public IReadOnlyList<FiscalTableRowDto> ProvisionsTable { get; init; } = Array.Empty<FiscalTableRowDto>();
    public string CompanyName { get; init; } = "Société";
}
