namespace FactuTrust.Domain.Services.Payroll;

/// <summary>Identité employeur pour le certificat de retenue à la source.</summary>
public sealed record EmployerSnapshot(
    string CompanyName,
    string Nif,
    string? AddressLine);

/// <summary>Identité salarié lue au moment de la génération (non figée sur le bulletin).</summary>
public sealed record EmployeeIdentitySnapshot(
    Guid EmployeeId,
    string EmployeeNumber,
    string EmployeeName,
    string? Cin,
    string? CnssNumber,
    string? AddressLine,
    bool IsHeadOfFamily);

/// <summary>Contribution mensuelle au certificat annuel.</summary>
public sealed record PayrollWithholdingCertificateMonth(
    int Month,
    string MonthLabel,
    decimal MonthlyNetTaxable,
    decimal Irpp,
    decimal IrppRegularization,
    decimal Css,
    decimal CssRegularization,
    bool HasPayslip);

/// <summary>Agrégat annuel par salarié.</summary>
public sealed record PayrollWithholdingCertificateLine
{
    public Guid EmployeeId { get; init; }
    public string EmployeeNumber { get; init; } = null!;
    public string EmployeeName { get; init; } = null!;
    public string? Cin { get; init; }
    public string? CnssNumber { get; init; }
    public string? AddressLine { get; init; }
    public bool IsHeadOfFamily { get; init; }

    public int MonthsCount { get; init; }
    public bool IsPartialYear { get; init; }

    public decimal TotalGross { get; init; }
    public decimal TotalCnssableGross { get; init; }
    public decimal TotalCnssEmployee { get; init; }
    public decimal TotalProfessionalExpenses { get; init; }
    public decimal TotalFamilyDeductions { get; init; }
    public decimal AnnualNetTaxable { get; init; }
    public decimal TotalIrppWithheld { get; init; }
    public decimal TotalCssWithheld { get; init; }
    public decimal TotalWithholding { get; init; }
    public decimal TotalIrppSmigExemption { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public IReadOnlyList<PayrollWithholdingCertificateMonth> Months { get; init; } = Array.Empty<PayrollWithholdingCertificateMonth>();

    public string DocumentReference => $"CRS-{EmployeeNumber}";
}

/// <summary>Lot de certificats pour un exercice.</summary>
public sealed record PayrollWithholdingCertificateBatch
{
    public int Year { get; init; }
    public string EmployerCompanyName { get; init; } = null!;
    public string EmployerNif { get; init; } = null!;
    public string? EmployerAddressLine { get; init; }

    public int EmployeeCount { get; init; }
    public IReadOnlyList<int> IncludedMonths { get; init; } = Array.Empty<int>();
    public IReadOnlyList<int> MissingMonths { get; init; } = Array.Empty<int>();
    public bool IsComplete { get; init; }

    public decimal TotalGross { get; init; }
    public decimal TotalAnnualNetTaxable { get; init; }
    public decimal TotalIrppWithheld { get; init; }
    public decimal TotalCssWithheld { get; init; }
    public decimal TotalWithholding { get; init; }

    public IReadOnlyList<PayrollWithholdingCertificateLine> Lines { get; init; } = Array.Empty<PayrollWithholdingCertificateLine>();
}
