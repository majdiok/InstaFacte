namespace FactuTrust.Application.DTOs;

// ── Postes budgétaires ───────────────────────────────────────────────────────

public sealed record BudgetPostDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = null!;
    public string Label { get; init; } = null!;
    /// <summary>0 = charges (débit − crédit), 1 = produits (crédit − débit).</summary>
    public int Kind { get; init; }
    /// <summary>Préfixes de comptes séparés par « ; » (ex. « 61;62 »).</summary>
    public string AccountPrefixes { get; init; } = null!;
    public bool IsActive { get; init; }
    public int DisplayOrder { get; init; }
}

public sealed record CreateBudgetPostRequest
{
    public string Code { get; init; } = null!;
    public string Label { get; init; } = null!;
    public int Kind { get; init; }
    public string AccountPrefixes { get; init; } = null!;
    public int DisplayOrder { get; init; }
}

public sealed record UpdateBudgetPostRequest
{
    public string Label { get; init; } = null!;
    public int Kind { get; init; }
    public string AccountPrefixes { get; init; } = null!;
    public int DisplayOrder { get; init; }
}

// ── Grille budgétaire d'un exercice ─────────────────────────────────────────

/// <summary>Ligne de la grille : un poste avec ses 12 montants par version (index 0 = janvier).</summary>
public sealed record BudgetGridRowDto
{
    public Guid BudgetPostId { get; init; }
    public string Code { get; init; } = null!;
    public string Label { get; init; } = null!;
    public int Kind { get; init; }
    public decimal[] InitialMonths { get; init; } = new decimal[12];
    public decimal[] RevisedMonths { get; init; } = new decimal[12];
}

public sealed record BudgetYearGridDto
{
    public int FiscalYear { get; init; }
    /// <summary>0 = brouillon (Initial modifiable), 1 = validé (Révisé modifiable).</summary>
    public int Status { get; init; }
    public DateTime? ValidatedAt { get; init; }
    public string? ValidatedBy { get; init; }
    /// <summary>Version actuellement modifiable : 0 = Initial, 1 = Révisé.</summary>
    public int EditableVersion { get; init; }
    public IReadOnlyList<BudgetGridRowDto> Rows { get; init; } = Array.Empty<BudgetGridRowDto>();
}

public sealed record SaveBudgetLineRequest
{
    public Guid BudgetPostId { get; init; }
    /// <summary>Mois 1-12.</summary>
    public int Month { get; init; }
    public decimal Amount { get; init; }
}

/// <summary>
/// Enregistre la version modifiable de l'exercice : remplace les valeurs des postes fournis
/// (montant zéro = suppression de la ligne).
/// </summary>
public sealed record SaveBudgetYearRequest
{
    public IReadOnlyList<SaveBudgetLineRequest> Lines { get; init; } = Array.Empty<SaveBudgetLineRequest>();
}

// ── État budgétaire (budget vs réalisé) ─────────────────────────────────────

public sealed record BudgetReportRowDto
{
    public string Code { get; init; } = null!;
    public string Label { get; init; } = null!;
    public int Kind { get; init; }
    /// <summary>Vrai pour les lignes synthétiques « Hors postes » (classes 6/7 non couvertes).</summary>
    public bool IsOffPost { get; init; }
    public decimal[] MonthlyInitial { get; init; } = new decimal[12];
    public decimal[] MonthlyRevised { get; init; } = new decimal[12];
    public decimal[] MonthlyActual { get; init; } = new decimal[12];
    public decimal PeriodInitial { get; init; }
    public decimal PeriodRevised { get; init; }
    public decimal PeriodActual { get; init; }
    /// <summary>Écart = réalisé − budget révisé (période).</summary>
    public decimal Variance { get; init; }
    /// <summary>Réalisé / budget révisé × 100 (null si budget révisé nul).</summary>
    public decimal? ConsumptionPercent { get; init; }
}

public sealed record BudgetReportTotalsDto
{
    public decimal ExpenseInitial { get; init; }
    public decimal ExpenseRevised { get; init; }
    public decimal ExpenseActual { get; init; }
    public decimal RevenueInitial { get; init; }
    public decimal RevenueRevised { get; init; }
    public decimal RevenueActual { get; init; }
}

public sealed record BudgetReportDto
{
    public int FiscalYear { get; init; }
    /// <summary>Dernier mois inclus dans les cumuls de période (1-12).</summary>
    public int ThroughMonth { get; init; }
    /// <summary>Faux tant que le budget initial n'est pas validé (Révisé affiché = Initial).</summary>
    public bool IsValidated { get; init; }
    public IReadOnlyList<BudgetReportRowDto> Rows { get; init; } = Array.Empty<BudgetReportRowDto>();
    public BudgetReportTotalsDto Totals { get; init; } = new();
}
