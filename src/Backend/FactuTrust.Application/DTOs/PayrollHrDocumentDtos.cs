namespace FactuTrust.Application.DTOs;

/// <summary>Données agrégées pour le certificat de travail d'un salarié.</summary>
public sealed record EmploymentCertificateDto
{
    public string EmployerCompanyName { get; init; } = null!;
    public string? EmployerNif { get; init; }
    public string? EmployerAddressLine { get; init; }

    public Guid EmployeeId { get; init; }
    public string EmployeeNumber { get; init; } = null!;
    public string EmployeeName { get; init; } = null!;
    public string? Cin { get; init; }
    public string? CnssNumber { get; init; }
    public string? AddressLine { get; init; }

    public DateTime HireDate { get; init; }
    public DateTime? TerminationDate { get; init; }
    public bool IsStillEmployed { get; init; }

    public string? JobTitle { get; init; }
    public string? ContractTypeDisplay { get; init; }
    public DateTime ContractStartDate { get; init; }
    public DateTime? ContractEndDate { get; init; }

    public string DocumentReference { get; init; } = null!;
    public DateTime GeneratedAt { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

/// <summary>Ligne mensuelle d'une attestation de salaire.</summary>
public sealed record SalaryCertificateMonthDto
{
    public int Year { get; init; }
    public int Month { get; init; }
    public string MonthLabel { get; init; } = null!;
    public decimal GrossSalary { get; init; }
    public decimal NetSalary { get; init; }
    public bool HasPayslip { get; init; }
}

/// <summary>Données agrégées pour l'attestation de salaire (3, 6 ou 12 mois).</summary>
public sealed record SalaryCertificateDto
{
    public string EmployerCompanyName { get; init; } = null!;
    public string? EmployerNif { get; init; }
    public string? EmployerAddressLine { get; init; }

    public Guid EmployeeId { get; init; }
    public string EmployeeNumber { get; init; } = null!;
    public string EmployeeName { get; init; } = null!;
    public string? Cin { get; init; }
    public string? CnssNumber { get; init; }
    public string? JobTitle { get; init; }

    public int PeriodMonths { get; init; }
    public DateTime PeriodStart { get; init; }
    public DateTime PeriodEnd { get; init; }
    public int PayslipCount { get; init; }

    public decimal AverageGrossSalary { get; init; }
    public decimal AverageNetSalary { get; init; }
    public decimal TotalGrossSalary { get; init; }
    public decimal TotalNetSalary { get; init; }

    public IReadOnlyList<SalaryCertificateMonthDto> Months { get; init; } = Array.Empty<SalaryCertificateMonthDto>();

    public string DocumentReference { get; init; } = null!;
    public DateTime GeneratedAt { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

/// <summary>Ligne récapitulative du solde de tout compte.</summary>
public sealed record SoldeToutCompteLineDto
{
    public string Label { get; init; } = null!;
    public decimal Amount { get; init; }
    public bool IsDeduction { get; init; }
}

/// <summary>Données agrégées pour le solde de tout compte (STC).</summary>
public sealed record SoldeToutCompteDto
{
    public string EmployerCompanyName { get; init; } = null!;
    public string? EmployerNif { get; init; }
    public string? EmployerAddressLine { get; init; }

    public Guid EmployeeId { get; init; }
    public string EmployeeNumber { get; init; } = null!;
    public string EmployeeName { get; init; } = null!;
    public string? Cin { get; init; }
    public string? CnssNumber { get; init; }
    public string? JobTitle { get; init; }

    public DateTime TerminationDate { get; init; }
    public int SettlementYear { get; init; }
    public int SettlementMonth { get; init; }
    public string SettlementMonthLabel { get; init; } = null!;

    public decimal GrossSalary { get; init; }
    public decimal CnssEmployee { get; init; }
    public decimal Irpp { get; init; }
    public decimal IrppRegularization { get; init; }
    public decimal Css { get; init; }
    public decimal CssRegularization { get; init; }
    public decimal OtherDeductions { get; init; }
    public decimal NetSalary { get; init; }

    public IReadOnlyList<SoldeToutCompteLineDto> Lines { get; init; } = Array.Empty<SoldeToutCompteLineDto>();

    public string DocumentReference { get; init; } = null!;
    public DateTime GeneratedAt { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}
