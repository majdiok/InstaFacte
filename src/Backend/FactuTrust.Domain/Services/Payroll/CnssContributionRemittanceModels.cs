using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Services.Payroll;

/// <summary>Ligne salarié du bordereau de versement CNSS mensuel.</summary>
public sealed record CnssContributionRemittanceLine
{
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; init; } = null!;
    public string? CnssNumber { get; init; }
    public decimal CnssableGross { get; init; }
    public decimal CnssEmployee { get; init; }
    public decimal CnssEmployer { get; init; }
    public decimal WorkAccident { get; init; }
    public decimal LineTotal { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

/// <summary>Bordereau mensuel de paiement des cotisations CNSS (compte 453).</summary>
public sealed record CnssContributionRemittanceBatch
{
    public int Year { get; init; }
    public int Month { get; init; }
    public string EmployerCompanyName { get; init; } = null!;
    public string EmployerNif { get; init; } = null!;
    public string? EmployerCnssNumber { get; init; }
    public string? EmployerAddressLine { get; init; }
    public Guid? PayrollRunId { get; init; }
    public PayrollRunStatus? SourceRunStatus { get; init; }
    public bool IsEligible { get; init; }
    public bool HasExistingPayment { get; init; }
    public CnssRemittancePaymentStatus? PaymentStatus { get; init; }
    public decimal TotalCnssEmployee { get; init; }
    public decimal TotalCnssEmployer { get; init; }
    public decimal TotalWorkAccident { get; init; }
    public decimal TotalDue { get; init; }
    public int EmployeeCount { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public IReadOnlyList<CnssContributionRemittanceLine> Lines { get; init; } = Array.Empty<CnssContributionRemittanceLine>();

    public string DocumentReference => $"BCNSS-{Year}{Month:D2}";
}
