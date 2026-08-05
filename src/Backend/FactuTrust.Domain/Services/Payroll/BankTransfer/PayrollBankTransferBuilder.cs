using FactuTrust.Domain.Banking;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Services.Payroll.BankTransfer;

/// <summary>
/// Construit un lot de virements de salaires à partir d'un cycle validé/clôturé.
/// Fonction pure : aucune I/O, montants figés sur les bulletins.
/// </summary>
public static class PayrollBankTransferBuilder
{
    /// <summary>
    /// Agrège les bulletins + RIB salariés → lignes éligibles / exclues + totaux + avertissements.
    /// </summary>
    public static PayrollBankTransferBatch Build(
        PayrollRun run,
        IReadOnlyList<Payslip> payslips,
        IReadOnlyDictionary<Guid, Employee> employeesById,
        BankTransferExportOptions options,
        BankTransferCompanyInfo? company,
        BankTransferDebtorAccountInfo? debtorAccount,
        IReadOnlyList<GarnishmentTransferLineInput>? garnishmentLines = null)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(payslips);
        ArgumentNullException.ThrowIfNull(employeesById);
        ArgumentNullException.ThrowIfNull(options);

        var transferLabel = string.IsNullOrWhiteSpace(options.TransferLabel)
            ? run.Label
            : options.TransferLabel.Trim();

        var eligible = new List<PayrollBankTransferLine>();
        var excluded = new List<PayrollBankTransferExcludedLine>();
        var warnings = new List<PayrollBankTransferWarning>();

        if (debtorAccount is null)
        {
            warnings.Add(new PayrollBankTransferWarning(
                PayrollBankTransferWarningCode.NoDebtorAccount,
                "Aucun compte bancaire débiteur sélectionné : l'en-tête du fichier n'inclura pas le RIB de l'entreprise."));
        }

        foreach (var payslip in payslips.OrderBy(p => p.EmployeeName, StringComparer.OrdinalIgnoreCase))
        {
            if (!employeesById.TryGetValue(payslip.EmployeeId, out var employee))
            {
                excluded.Add(Exclude(
                    payslip.EmployeeId,
                    payslip.Id,
                    payslip.EmployeeNumber,
                    payslip.EmployeeName,
                    payslip.NetSalary,
                    PayrollBankTransferExclusionReason.EmployeeNotFound));
                continue;
            }

            if (payslip.NetSalary <= 0)
            {
                excluded.Add(Exclude(
                    employee.Id,
                    payslip.Id,
                    employee.EmployeeNumber,
                    employee.FullName,
                    payslip.NetSalary,
                    PayrollBankTransferExclusionReason.ZeroOrNegativeNet));
                continue;
            }

            var ribDigits = TunisianIban.NormalizeRibDigits(employee.Rib);
            if (ribDigits.Length == 0)
            {
                excluded.Add(Exclude(
                    employee.Id,
                    payslip.Id,
                    employee.EmployeeNumber,
                    employee.FullName,
                    payslip.NetSalary,
                    PayrollBankTransferExclusionReason.MissingRib));
                continue;
            }

            if (!TunisianIban.IsValidRibDigits(ribDigits))
            {
                excluded.Add(Exclude(
                    employee.Id,
                    payslip.Id,
                    employee.EmployeeNumber,
                    employee.FullName,
                    payslip.NetSalary,
                    PayrollBankTransferExclusionReason.InvalidRib));
                continue;
            }

            var iban = TunisianIban.FromRib(ribDigits)!;
            eligible.Add(new PayrollBankTransferLine
            {
                Kind = PayrollBankTransferLineKind.EmployeeSalary,
                EmployeeId = employee.Id,
                PayslipId = payslip.Id,
                EmployeeNumber = employee.EmployeeNumber,
                LastName = employee.LastName,
                FirstName = employee.FirstName,
                Rib = ribDigits,
                Iban = iban,
                NetSalary = Math.Round(payslip.NetSalary, 3, MidpointRounding.AwayFromZero),
                TransferLabel = transferLabel,
                Cin = employee.Cin,
                CnssNumber = payslip.CnssNumber ?? employee.CnssNumber
            });
        }

        if (garnishmentLines is { Count: > 0 })
        {
            foreach (var garnishment in garnishmentLines.OrderBy(g => g.BeneficiaryName, StringComparer.OrdinalIgnoreCase))
            {
                var ribDigits = TunisianIban.NormalizeRibDigits(garnishment.BeneficiaryRib);
                if (ribDigits.Length == 0 || !TunisianIban.IsValidRibDigits(ribDigits))
                    continue;

                var iban = TunisianIban.FromRib(ribDigits)!;
                eligible.Add(new PayrollBankTransferLine
                {
                    Kind = PayrollBankTransferLineKind.GarnishmentBeneficiary,
                    EmployeeId = garnishment.EmployeeId,
                    PayslipId = garnishment.PayslipId,
                    EmployeeNumber = garnishment.EmployeeNumber,
                    LastName = garnishment.BeneficiaryName,
                    FirstName = string.Empty,
                    Rib = ribDigits,
                    Iban = iban,
                    NetSalary = Math.Round(garnishment.Amount, 3, MidpointRounding.AwayFromZero),
                    TransferLabel = $"{transferLabel} — {garnishment.GarnishmentReference}",
                    BeneficiaryName = garnishment.BeneficiaryName,
                    GarnishmentReference = garnishment.GarnishmentReference,
                    SourceGarnishmentId = garnishment.GarnishmentId
                });
            }
        }

        if (excluded.Count > 0)
        {
            warnings.Add(new PayrollBankTransferWarning(
                PayrollBankTransferWarningCode.EmployeesExcluded,
                $"{excluded.Count} salarié(s) exclus de l'export (RIB manquant/invalide ou net nul)."));
        }

        var total = Math.Round(eligible.Sum(l => l.NetSalary), 3, MidpointRounding.AwayFromZero);

        return new PayrollBankTransferBatch
        {
            PayrollRunId = run.Id,
            Year = run.Year,
            Month = run.Month,
            PeriodLabel = run.Label,
            TransferLabel = transferLabel,
            ExportDateUtc = options.ExportDateUtc,
            Company = company,
            DebtorAccount = debtorAccount,
            Lines = eligible,
            ExcludedLines = excluded,
            Warnings = warnings,
            EligibleCount = eligible.Count,
            TotalAmount = total
        };
    }

    /// <summary>True si le cycle peut faire l'objet d'un export virement.</summary>
    public static bool CanExport(PayrollRunStatus status) =>
        status is PayrollRunStatus.Validated or PayrollRunStatus.Closed;

    private static PayrollBankTransferExcludedLine Exclude(
        Guid employeeId,
        Guid? payslipId,
        string employeeNumber,
        string employeeName,
        decimal netSalary,
        PayrollBankTransferExclusionReason reason) =>
        new()
        {
            EmployeeId = employeeId,
            PayslipId = payslipId,
            EmployeeNumber = employeeNumber,
            EmployeeName = employeeName,
            NetSalary = Math.Round(netSalary, 3, MidpointRounding.AwayFromZero),
            Reason = reason,
            ReasonDisplay = reason.ToDisplayString()
        };
}
