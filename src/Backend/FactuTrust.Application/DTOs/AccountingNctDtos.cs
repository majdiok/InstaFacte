using FactuTrust.Application.Accounting;

namespace FactuTrust.Application.DTOs;

/// <summary>
/// Ligne d'un état financier NCT (rubrique normalisée). <see cref="IsSubtotal"/> marque les sous-totaux ;
/// <see cref="Level"/> pilote l'indentation dans l'affichage.
/// </summary>
public sealed record NctLineDto
{
    public string Code { get; init; } = null!;
    public string Label { get; init; } = null!;
    public decimal Amount { get; init; }
    public decimal PreviousAmount { get; init; }
    public bool IsSubtotal { get; init; }
    public int Level { get; init; }
}

public sealed record NctBalanceSheetDto
{
    public IReadOnlyList<NctLineDto> Assets { get; init; } = Array.Empty<NctLineDto>();
    public IReadOnlyList<NctLineDto> EquityAndLiabilities { get; init; } = Array.Empty<NctLineDto>();
    public decimal TotalAssets { get; init; }
    public decimal TotalEquityAndLiabilities { get; init; }
    public decimal PreviousTotalAssets { get; init; }
    public decimal PreviousTotalEquityAndLiabilities { get; init; }
    /// <summary>Vrai si Total Actif = Total Capitaux propres et Passifs (contrôle d'équilibre).</summary>
    public bool IsBalanced { get; init; }
}

public sealed record NctIncomeStatementDto
{
    public IReadOnlyList<NctLineDto> Lines { get; init; } = Array.Empty<NctLineDto>();
    public decimal OperatingResult { get; init; }
    public decimal FinancialResult { get; init; }
    public decimal ResultBeforeTax { get; init; }
    public decimal NetResult { get; init; }
    public decimal PreviousNetResult { get; init; }
}

public sealed record NctCashFlowDto
{
    public IReadOnlyList<NctLineDto> Lines { get; init; } = Array.Empty<NctLineDto>();
    public decimal OperatingCashFlow { get; init; }
    public decimal InvestingCashFlow { get; init; }
    public decimal FinancingCashFlow { get; init; }
    public decimal NetChange { get; init; }
    public decimal OpeningCash { get; init; }
    public decimal ClosingCash { get; init; }
    /// <summary>Vrai si la variation calculée rapproche la variation réelle de trésorerie (classe 5).</summary>
    public bool IsReconciled { get; init; }
}

public sealed record NctEquityChangeDto
{
    public IReadOnlyList<NctLineDto> Lines { get; init; } = Array.Empty<NctLineDto>();
    public decimal OpeningEquity { get; init; }
    public decimal NetResult { get; init; }
    public decimal ClosingEquity { get; init; }
}

public sealed record NctNoteDto
{
    public string Title { get; init; } = null!;
    /// <summary>Texte narratif optionnel (ex. méthodes comptables) affiché avant les lignes.</summary>
    public string? Description { get; init; }
    public IReadOnlyList<NctLineDto> Lines { get; init; } = Array.Empty<NctLineDto>();
}

/// <summary>Ligne détaillée d'une note annexe (compte / intitulé / N / N-1).</summary>
public sealed record NctDetailedNoteLineDto
{
    public string AccountNumber { get; init; } = null!;
    public string Label { get; init; } = null!;
    public decimal Amount { get; init; }
    public decimal PreviousAmount { get; init; }
}

/// <summary>Note annexe détaillée compte par compte (format NOTES ACTIF/PASSIF BILAN).</summary>
public sealed record NctDetailedNoteDto
{
    public int Number { get; init; }
    public string Title { get; init; } = null!;
    public NctAnnexFamily Family { get; init; }
    public IReadOnlyList<NctDetailedNoteLineDto> Lines { get; init; } = Array.Empty<NctDetailedNoteLineDto>();
    public decimal Total { get; init; }
    public decimal PreviousTotal { get; init; }
    /// <summary>
    /// Texte narratif saisi par le comptable (personnalisation d'annexe) ; null par défaut —
    /// le catalogue n'en produit aucun.
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// Entrée du catalogue des notes annexes détaillées, exposée à l'écran de personnalisation.
/// DTO dédié : <c>NctDetailedNoteDefinition</c> porte un prédicat de comptes (délégué) qui n'est
/// pas sérialisable — seuls le numéro, le libellé par défaut et la famille sont publiés.
/// </summary>
public sealed record NctNoteCatalogEntryDto
{
    public int Number { get; init; }
    /// <summary>Libellé du catalogue, utilisé tant qu'aucune personnalisation ne le remplace.</summary>
    public string DefaultTitle { get; init; } = null!;
    public NctAnnexFamily Family { get; init; }
}

/// <summary>Personnalisation d'une note annexe pour un exercice (null = valeur du catalogue).</summary>
public sealed record NctNoteOverrideDto
{
    public int FiscalYear { get; init; }
    public int NoteNumber { get; init; }
    public string? CustomTitle { get; init; }
    public string? CustomDescription { get; init; }
    public bool IsHidden { get; init; }
}

/// <summary>Requête d'enregistrement d'une personnalisation de note.</summary>
public sealed record UpsertNctNoteOverrideRequest
{
    public int FiscalYear { get; init; }
    public int NoteNumber { get; init; }
    public string? CustomTitle { get; init; }
    public string? CustomDescription { get; init; }
    public bool IsHidden { get; init; }
}

/// <summary>
/// Liasse NCT (Normes Comptables Tunisiennes) : bilan + compte de résultat structurés, tableau de flux
/// de trésorerie, état de variation des capitaux propres et notes annexes. Calculée sur les écritures
/// VALIDÉES, avec comparatif N-1. Additive — n'altère pas <c>BalanceSheetDto</c>/<c>IncomeStatementDto</c>.
/// </summary>
public sealed record NctFinancialStatementsDto
{
    public int FiscalYear { get; init; }
    public NctBalanceSheetDto BalanceSheet { get; init; } = new();
    public NctIncomeStatementDto IncomeStatement { get; init; } = new();
    public NctCashFlowDto CashFlow { get; init; } = new();
    public NctEquityChangeDto EquityChanges { get; init; } = new();
    public IReadOnlyList<NctNoteDto> Notes { get; init; } = Array.Empty<NctNoteDto>();
    /// <summary>Notes détaillées compte par compte (additif — n'altère pas <see cref="Notes"/>).</summary>
    public IReadOnlyList<NctDetailedNoteDto> DetailedNotes { get; init; } = Array.Empty<NctDetailedNoteDto>();
    public bool NctStatementsEnabled { get; init; }
}
