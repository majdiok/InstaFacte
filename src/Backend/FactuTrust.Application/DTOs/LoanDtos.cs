namespace FactuTrust.Application.DTOs;

/// <summary>Une échéance de l'échéancier d'un emprunt.</summary>
public sealed record LoanScheduleLineDto
{
    public int InstallmentNumber { get; init; }
    public DateTime DueDate { get; init; }
    /// <summary>Capital restant dû avant l'échéance.</summary>
    public decimal OpeningBalance { get; init; }
    public decimal InterestAmount { get; init; }
    public decimal PrincipalAmount { get; init; }
    /// <summary>Annuité = capital + intérêt.</summary>
    public decimal InstallmentAmount { get; init; }
    public decimal ClosingBalance { get; init; }
}

/// <summary>Emprunt du registre (sans son échéancier).</summary>
public sealed record LoanDto
{
    public Guid Id { get; init; }
    public string LoanNumber { get; init; } = null!;
    public string Label { get; init; } = null!;
    public string LenderName { get; init; } = null!;
    public decimal Principal { get; init; }
    public decimal AnnualRatePercent { get; init; }
    public DateTime StartDate { get; init; }
    public int InstallmentCount { get; init; }
    /// <summary>0 = mensuelle, 1 = trimestrielle, 2 = semestrielle, 3 = annuelle.</summary>
    public int Periodicity { get; init; }
    /// <summary>0 = annuité constante, 1 = amortissement constant.</summary>
    public int Method { get; init; }
    public string LoanAccountNumber { get; init; } = null!;
    public string InterestAccountNumber { get; init; } = null!;
    public string BankAccountNumber { get; init; } = null!;
    /// <summary>0 = actif, 1 = remboursé, 2 = annulé.</summary>
    public int Status { get; init; }
    public string? Notes { get; init; }
    public decimal TotalInterest { get; init; }
    public decimal TotalRepayment { get; init; }
}

/// <summary>Tableau d'amortissement complet d'un emprunt (en-tête + échéancier + totaux).</summary>
public sealed record LoanScheduleDto
{
    public LoanDto Loan { get; init; } = null!;
    public IReadOnlyList<LoanScheduleLineDto> Lines { get; init; } = Array.Empty<LoanScheduleLineDto>();
    public decimal TotalPrincipal { get; init; }
    public decimal TotalInterest { get; init; }
    public decimal TotalInstallments { get; init; }
    /// <summary>
    /// Contrôle d'auto-cohérence : Σ capital remboursé == capital emprunté et solde final nul.
    /// Faux signale un échéancier incohérent (ne doit jamais arriver).
    /// </summary>
    public bool IsSettled { get; init; }
}

/// <summary>Page du registre des emprunts.</summary>
public sealed record LoanListDto
{
    public IReadOnlyList<LoanDto> Items { get; init; } = Array.Empty<LoanDto>();
    public int TotalCount { get; init; }
}

/// <summary>Requête de création d'un emprunt (l'échéancier est généré automatiquement).</summary>
public sealed record CreateLoanRequest
{
    public string? LoanNumber { get; init; }
    public string Label { get; init; } = null!;
    public string LenderName { get; init; } = null!;
    public decimal Principal { get; init; }
    public decimal AnnualRatePercent { get; init; }
    public DateTime StartDate { get; init; }
    public int InstallmentCount { get; init; }
    public int Periodicity { get; init; }
    public int Method { get; init; }
    public string LoanAccountNumber { get; init; } = "16";
    public string InterestAccountNumber { get; init; } = "651";
    public string BankAccountNumber { get; init; } = "532";
    public string? Notes { get; init; }
}
