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
    public bool NctStatementsEnabled { get; init; }
}
