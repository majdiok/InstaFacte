namespace FactuTrust.Domain.Services.Payroll.BankTransfer;

/// <summary>Formats d'export de virement de salaires (MVP : CSV standard ; banques en phase 2).</summary>
public enum PayrollBankTransferFormat
{
    /// <summary>CSV universel (; UTF-8 BOM) — importable via Excel / portails bancaires.</summary>
    StandardCsv = 0
}

/// <summary>Motif d'exclusion d'une ligne de virement.</summary>
public enum PayrollBankTransferExclusionReason
{
    ZeroOrNegativeNet = 0,
    MissingRib = 1,
    InvalidRib = 2,
    EmployeeNotFound = 3
}

/// <summary>Code d'avertissement au niveau du lot.</summary>
public enum PayrollBankTransferWarningCode
{
    NoDebtorAccount = 0,
    EmployeesExcluded = 1
}

/// <summary>Options d'export (libellé, format, date).</summary>
public sealed record BankTransferExportOptions
{
    public required string TransferLabel { get; init; }
    public PayrollBankTransferFormat Format { get; init; } = PayrollBankTransferFormat.StandardCsv;
    public DateTime ExportDateUtc { get; init; } = DateTime.UtcNow;
}

/// <summary>Informations société débiteur (méta CSV).</summary>
public sealed record BankTransferCompanyInfo(string Name);

/// <summary>Compte bancaire débiteur (méta CSV).</summary>
public sealed record BankTransferDebtorAccountInfo(
    string Rib,
    string Iban,
    string BankName,
    string? BankCode);

/// <summary>Ligne éligible à l'export.</summary>
public enum PayrollBankTransferLineKind
{
    EmployeeSalary = 0,
    GarnishmentBeneficiary = 1
}

/// <summary>Ligne éligible à l'export.</summary>
public sealed record PayrollBankTransferLine
{
    public PayrollBankTransferLineKind Kind { get; init; } = PayrollBankTransferLineKind.EmployeeSalary;
    public Guid EmployeeId { get; init; }
    public Guid PayslipId { get; init; }
    public string EmployeeNumber { get; init; } = null!;
    public string LastName { get; init; } = null!;
    public string FirstName { get; init; } = null!;
    public string Rib { get; init; } = null!;
    public string Iban { get; init; } = null!;
    public decimal NetSalary { get; init; }
    public string TransferLabel { get; init; } = null!;
    public string? Cin { get; init; }
    public string? CnssNumber { get; init; }
  /// <summary>Bénéficiaire de saisie (si <see cref="Kind"/> = GarnishmentBeneficiary).</summary>
    public string? BeneficiaryName { get; init; }
    public string? GarnishmentReference { get; init; }
    public Guid? SourceGarnishmentId { get; init; }
}

/// <summary>Ligne exclue de l'export avec motif.</summary>
public sealed record PayrollBankTransferExcludedLine
{
    public Guid EmployeeId { get; init; }
    public Guid? PayslipId { get; init; }
    public string EmployeeNumber { get; init; } = null!;
    public string EmployeeName { get; init; } = null!;
    public decimal NetSalary { get; init; }
    public PayrollBankTransferExclusionReason Reason { get; init; }
    public string ReasonDisplay { get; init; } = null!;
}

/// <summary>Avertissement au niveau du lot.</summary>
public sealed record PayrollBankTransferWarning(
    PayrollBankTransferWarningCode Code,
    string Message);

/// <summary>Lot de virement prêt à sérialiser.</summary>
public sealed record PayrollBankTransferBatch
{
    public Guid PayrollRunId { get; init; }
    public int Year { get; init; }
    public int Month { get; init; }
    public string PeriodLabel { get; init; } = null!;
    public string TransferLabel { get; init; } = null!;
    public DateTime ExportDateUtc { get; init; }
    public BankTransferCompanyInfo? Company { get; init; }
    public BankTransferDebtorAccountInfo? DebtorAccount { get; init; }
    public IReadOnlyList<PayrollBankTransferLine> Lines { get; init; } = Array.Empty<PayrollBankTransferLine>();
    public IReadOnlyList<PayrollBankTransferExcludedLine> ExcludedLines { get; init; } = Array.Empty<PayrollBankTransferExcludedLine>();
    public IReadOnlyList<PayrollBankTransferWarning> Warnings { get; init; } = Array.Empty<PayrollBankTransferWarning>();
    public int EligibleCount { get; init; }
    public decimal TotalAmount { get; init; }
}

public static class PayrollBankTransferExclusionReasonExtensions
{
    public static string ToDisplayString(this PayrollBankTransferExclusionReason reason) => reason switch
    {
        PayrollBankTransferExclusionReason.ZeroOrNegativeNet => "Net à payer nul ou négatif",
        PayrollBankTransferExclusionReason.MissingRib => "RIB manquant",
        PayrollBankTransferExclusionReason.InvalidRib => "RIB invalide (20 chiffres requis)",
        PayrollBankTransferExclusionReason.EmployeeNotFound => "Salarié introuvable",
        _ => throw new ArgumentOutOfRangeException(nameof(reason))
    };
}

/// <summary>Ligne de virement vers un bénéficiaire de saisie (post-validation).</summary>
public sealed record GarnishmentTransferLineInput(
    Guid GarnishmentId,
    Guid EmployeeId,
    Guid PayslipId,
    string EmployeeNumber,
    string BeneficiaryName,
    string? BeneficiaryRib,
    string GarnishmentReference,
    decimal Amount);
