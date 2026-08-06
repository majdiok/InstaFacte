using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Services.Payroll;

/// <summary>
/// Agrège les bulletins figés d'un mois en bordereau de versement CNSS (compte 453).
/// Fonction pure : seuls les cycles Validés/Clôturés alimentent le calcul.
/// </summary>
public static class CnssContributionRemittanceBuilder
{
    public static CnssContributionRemittanceBatch Build(
        int year,
        int month,
        PayrollRun? run,
        EmployerSnapshot employer,
        bool hasExistingPayment,
        CnssRemittancePaymentStatus? paymentStatus)
    {
        ArgumentNullException.ThrowIfNull(employer);

        var batchWarnings = new List<string>();
        var isEligible = run is not null
            && (run.Status == PayrollRunStatus.Validated || run.Status == PayrollRunStatus.Closed);

        if (run is null)
            batchWarnings.Add("Aucun cycle de paie pour cette période.");
        else if (!isEligible)
            batchWarnings.Add("Le cycle de paie n'est pas validé ou clôturé.");

        if (string.IsNullOrWhiteSpace(employer.CnssEmployerNumber))
            batchWarnings.Add("Matricule employeur CNSS manquant.");

        if (!isEligible || run is null)
        {
            return new CnssContributionRemittanceBatch
            {
                Year = year,
                Month = month,
                EmployerCompanyName = employer.CompanyName,
                EmployerNif = employer.Nif,
                EmployerCnssNumber = employer.CnssEmployerNumber,
                EmployerAddressLine = employer.AddressLine,
                PayrollRunId = run?.Id,
                SourceRunStatus = run?.Status,
                IsEligible = false,
                HasExistingPayment = hasExistingPayment,
                PaymentStatus = paymentStatus,
                Warnings = batchWarnings
            };
        }

        var lines = run.Payslips
            .Select(BuildLine)
            .OrderBy(l => l.EmployeeName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var totalCnssEmployee = R(lines.Sum(l => l.CnssEmployee));
        var totalCnssEmployer = R(lines.Sum(l => l.CnssEmployer));
        var totalWorkAccident = R(lines.Sum(l => l.WorkAccident));
        var totalDue = R(totalCnssEmployee + totalCnssEmployer + totalWorkAccident);

        var missingCnss = lines.Count(l => string.IsNullOrWhiteSpace(l.CnssNumber));
        if (missingCnss > 0)
            batchWarnings.Add($"{missingCnss} salarié(s) sans numéro CNSS.");

        return new CnssContributionRemittanceBatch
        {
            Year = year,
            Month = month,
            EmployerCompanyName = employer.CompanyName,
            EmployerNif = employer.Nif,
            EmployerCnssNumber = employer.CnssEmployerNumber,
            EmployerAddressLine = employer.AddressLine,
            PayrollRunId = run.Id,
            SourceRunStatus = run.Status,
            IsEligible = true,
            HasExistingPayment = hasExistingPayment,
            PaymentStatus = paymentStatus,
            TotalCnssEmployee = totalCnssEmployee,
            TotalCnssEmployer = totalCnssEmployer,
            TotalWorkAccident = totalWorkAccident,
            TotalDue = totalDue,
            EmployeeCount = lines.Count,
            Warnings = batchWarnings,
            Lines = lines
        };
    }

    private static CnssContributionRemittanceLine BuildLine(Payslip payslip)
    {
        var warnings = new List<string>();
        if (string.IsNullOrWhiteSpace(payslip.CnssNumber))
            warnings.Add("CNSS manquant");

        var lineTotal = R(payslip.CnssEmployee + payslip.CnssEmployer + payslip.WorkAccidentContribution);

        return new CnssContributionRemittanceLine
        {
            EmployeeId = payslip.EmployeeId,
            EmployeeName = payslip.EmployeeName,
            CnssNumber = payslip.CnssNumber,
            CnssableGross = payslip.CnssableGross,
            CnssEmployee = payslip.CnssEmployee,
            CnssEmployer = payslip.CnssEmployer,
            WorkAccident = payslip.WorkAccidentContribution,
            LineTotal = lineTotal,
            Warnings = warnings
        };
    }

    private static decimal R(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}
