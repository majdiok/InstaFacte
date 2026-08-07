using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Payroll.HrDocuments;

/// <summary>
/// Agrège employeur, salarié, contrats et bulletins figés pour les documents RH.
/// </summary>
public sealed class PayrollHrDocumentDataLoader
{
    private static readonly string[] MonthNamesFr =
    {
        "", "Janvier", "Février", "Mars", "Avril", "Mai", "Juin",
        "Juillet", "Août", "Septembre", "Octobre", "Novembre", "Décembre"
    };

    private readonly AccountingSettings _settings;
    private readonly IPayrollRunRepository _runs;
    private readonly IEmployeeRepository _employees;
    private readonly ITenantCompanySummaryProvider _companySummary;

    public PayrollHrDocumentDataLoader(
        IOptions<AccountingSettings> settings,
        IPayrollRunRepository runs,
        IEmployeeRepository employees,
        ITenantCompanySummaryProvider companySummary)
    {
        _settings = settings.Value;
        _runs = runs;
        _employees = employees;
        _companySummary = companySummary;
    }

    public Result EnsureFeatureEnabled() =>
        _settings.PayrollHrDocumentsEnabled
            ? Result.Success()
            : Result.Failure(Error.Validation(
                "Feature",
                "La génération des documents RH n'est pas activée sur cet environnement."));

    public async Task<Result<(Employee Employee, TenantCompanySummaryDto Company)>> LoadEmployeeContextAsync(
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        var feature = EnsureFeatureEnabled();
        if (feature.IsFailure)
            return Result.Failure<(Employee, TenantCompanySummaryDto)>(feature.Error);

        var employee = await _employees.GetByIdWithContractsAsync(employeeId, cancellationToken);
        if (employee is null)
            return Result.Failure<(Employee, TenantCompanySummaryDto)>(Error.NotFound("Employee", employeeId));

        var company = await _companySummary.GetCurrentTenantSummaryAsync(cancellationToken);
        if (company is null)
        {
            return Result.Failure<(Employee, TenantCompanySummaryDto)>(
                Error.Validation("Company", "Impossible de charger les informations de la société."));
        }

        return Result.Success((employee, company));
    }

    public async Task<Result<EmploymentCertificateDto>> BuildEmploymentCertificateAsync(
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        var context = await LoadEmployeeContextAsync(employeeId, cancellationToken);
        if (context.IsFailure)
            return Result.Failure<EmploymentCertificateDto>(context.Error);

        var (employee, company) = context.Value;
        var warnings = new List<string>();
        var contracts = employee.Contracts.OrderBy(c => c.StartDate).ToList();

        if (contracts.Count == 0)
            warnings.Add("Aucun contrat enregistré — les informations de poste peuvent être incomplètes.");

        var referenceContract = ResolveReferenceContract(employee, contracts);
        var contractStart = contracts.Count > 0
            ? contracts.Min(c => c.StartDate)
            : employee.HireDate;
        if (contractStart.Date > employee.HireDate.Date)
            contractStart = employee.HireDate;

        var contractEnd = employee.TerminationDate
            ?? referenceContract?.EndDate
            ?? contracts.Where(c => c.EndDate.HasValue).MaxBy(c => c.EndDate)?.EndDate;

        var generatedAt = DateTime.Now;
        return Result.Success(new EmploymentCertificateDto
        {
            EmployerCompanyName = company.CompanyName,
            EmployerNif = company.Nif,
            EmployerAddressLine = company.AddressLine,
            EmployeeId = employee.Id,
            EmployeeNumber = employee.EmployeeNumber,
            EmployeeName = employee.FullName,
            Cin = employee.Cin,
            CnssNumber = employee.CnssNumber,
            AddressLine = employee.Address?.ToSingleLine(),
            HireDate = employee.HireDate,
            TerminationDate = employee.TerminationDate,
            IsStillEmployed = !employee.TerminationDate.HasValue,
            JobTitle = referenceContract?.JobTitle,
            ContractTypeDisplay = referenceContract?.Type.ToDisplayString(),
            ContractStartDate = contractStart,
            ContractEndDate = contractEnd,
            DocumentReference = $"CT-{employee.EmployeeNumber}",
            GeneratedAt = generatedAt,
            Warnings = warnings
        });
    }

    public async Task<Result<SalaryCertificateDto>> BuildSalaryCertificateAsync(
        Guid employeeId,
        int periodMonths,
        CancellationToken cancellationToken)
    {
        if (periodMonths is not (3 or 6 or 12))
        {
            return Result.Failure<SalaryCertificateDto>(
                Error.Validation("Months", "La période doit être de 3, 6 ou 12 mois."));
        }

        var context = await LoadEmployeeContextAsync(employeeId, cancellationToken);
        if (context.IsFailure)
            return Result.Failure<SalaryCertificateDto>(context.Error);

        var (employee, company) = context.Value;
        var warnings = new List<string>();
        var payslips = await _runs.ListSettledPayslipsForEmployeeAsync(employeeId, cancellationToken);
        var referenceDate = employee.TerminationDate ?? DateTime.Today;
        var eligible = payslips
            .Where(p => new DateTime(p.Year, p.Month, 1) <= new DateTime(referenceDate.Year, referenceDate.Month, 1))
            .OrderByDescending(p => p.Year)
            .ThenByDescending(p => p.Month)
            .Take(periodMonths)
            .OrderBy(p => p.Year)
            .ThenBy(p => p.Month)
            .ToList();

        if (eligible.Count == 0)
        {
            return Result.Failure<SalaryCertificateDto>(
                Error.Validation("Payslips", "Aucun bulletin validé ou clôturé n'est disponible pour ce salarié."));
        }

        if (eligible.Count < periodMonths)
        {
            warnings.Add(
                $"Seuls {eligible.Count} bulletin(s) figé(s) sont disponibles sur les {periodMonths} mois demandés.");
        }

        var referenceContract = ResolveReferenceContract(employee, employee.Contracts.ToList());
        var periodStart = new DateTime(eligible[0].Year, eligible[0].Month, 1);
        var periodEnd = new DateTime(eligible[^1].Year, eligible[^1].Month, 1)
            .AddMonths(1)
            .AddDays(-1);

        var totalGross = eligible.Sum(p => p.GrossSalary);
        var totalNet = eligible.Sum(p => p.NetSalary);
        var count = eligible.Count;

        return Result.Success(new SalaryCertificateDto
        {
            EmployerCompanyName = company.CompanyName,
            EmployerNif = company.Nif,
            EmployerAddressLine = company.AddressLine,
            EmployeeId = employee.Id,
            EmployeeNumber = employee.EmployeeNumber,
            EmployeeName = employee.FullName,
            Cin = employee.Cin,
            CnssNumber = employee.CnssNumber,
            JobTitle = referenceContract?.JobTitle,
            PeriodMonths = periodMonths,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            PayslipCount = count,
            AverageGrossSalary = Math.Round(totalGross / count, 3, MidpointRounding.AwayFromZero),
            AverageNetSalary = Math.Round(totalNet / count, 3, MidpointRounding.AwayFromZero),
            TotalGrossSalary = totalGross,
            TotalNetSalary = totalNet,
            Months = eligible.Select(p => new SalaryCertificateMonthDto
            {
                Year = p.Year,
                Month = p.Month,
                MonthLabel = FormatMonth(p.Year, p.Month),
                GrossSalary = p.GrossSalary,
                NetSalary = p.NetSalary,
                HasPayslip = true
            }).ToList(),
            DocumentReference = $"AS-{employee.EmployeeNumber}-{periodMonths}M",
            GeneratedAt = DateTime.Now,
            Warnings = warnings
        });
    }

    public async Task<Result<SoldeToutCompteDto>> BuildSoldeToutCompteAsync(
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        var context = await LoadEmployeeContextAsync(employeeId, cancellationToken);
        if (context.IsFailure)
            return Result.Failure<SoldeToutCompteDto>(context.Error);

        var (employee, company) = context.Value;
        if (!employee.TerminationDate.HasValue)
        {
            return Result.Failure<SoldeToutCompteDto>(
                Error.Validation("Termination", "Le solde de tout compte n'est disponible que pour un salarié ayant une date de sortie."));
        }

        var termination = employee.TerminationDate.Value;
        var payslips = await _runs.ListSettledPayslipsForEmployeeAsync(employeeId, cancellationToken);
        var settlementPayslip = payslips
            .Where(p => p.Year == termination.Year && p.Month == termination.Month)
            .OrderByDescending(p => p.GrossSalary)
            .FirstOrDefault();

        if (settlementPayslip is null)
        {
            return Result.Failure<SoldeToutCompteDto>(
                Error.Validation(
                    "Payslip",
                    $"Aucun bulletin validé ou clôturé trouvé pour le mois de sortie ({FormatMonth(termination.Year, termination.Month)})."));
        }

        var warnings = new List<string>();
        if (settlementPayslip.IrppRegularization == 0 && settlementPayslip.CssRegularization == 0)
        {
            warnings.Add(
                "Aucune régularisation IRPP/CSS n'apparaît sur le bulletin de sortie — vérifiez que la régularisation annuelle a été calculée.");
        }

        var referenceContract = ResolveReferenceContract(employee, employee.Contracts.ToList());
        var lines = BuildSettlementLines(settlementPayslip);

        return Result.Success(new SoldeToutCompteDto
        {
            EmployerCompanyName = company.CompanyName,
            EmployerNif = company.Nif,
            EmployerAddressLine = company.AddressLine,
            EmployeeId = employee.Id,
            EmployeeNumber = employee.EmployeeNumber,
            EmployeeName = employee.FullName,
            Cin = employee.Cin,
            CnssNumber = employee.CnssNumber,
            JobTitle = referenceContract?.JobTitle,
            TerminationDate = termination,
            SettlementYear = settlementPayslip.Year,
            SettlementMonth = settlementPayslip.Month,
            SettlementMonthLabel = FormatMonth(settlementPayslip.Year, settlementPayslip.Month),
            GrossSalary = settlementPayslip.GrossSalary,
            CnssEmployee = settlementPayslip.CnssEmployee,
            Irpp = settlementPayslip.Irpp,
            IrppRegularization = settlementPayslip.IrppRegularization,
            Css = settlementPayslip.Css,
            CssRegularization = settlementPayslip.CssRegularization,
            OtherDeductions = settlementPayslip.OtherDeductions,
            NetSalary = settlementPayslip.NetSalary,
            Lines = lines,
            DocumentReference = $"STC-{employee.EmployeeNumber}",
            GeneratedAt = DateTime.Now,
            Warnings = warnings
        });
    }

    private static EmploymentContract? ResolveReferenceContract(Employee employee, IReadOnlyList<EmploymentContract> contracts)
    {
        if (contracts.Count == 0)
            return null;

        if (employee.TerminationDate.HasValue)
        {
            return employee.GetContractForPayrollMonth(employee.TerminationDate.Value.Year, employee.TerminationDate.Value.Month)
                   ?? contracts.OrderByDescending(c => c.StartDate).FirstOrDefault();
        }

        return contracts
            .Where(c => c.IsActive)
            .OrderByDescending(c => c.StartDate)
            .FirstOrDefault()
               ?? contracts.OrderByDescending(c => c.StartDate).FirstOrDefault();
    }

    private static IReadOnlyList<SoldeToutCompteLineDto> BuildSettlementLines(Payslip payslip)
    {
        var lines = new List<SoldeToutCompteLineDto>
        {
            new() { Label = "Salaire brut", Amount = payslip.GrossSalary, IsDeduction = false }
        };

        if (payslip.CnssEmployee > 0)
            lines.Add(new SoldeToutCompteLineDto { Label = "Cotisations CNSS salariales", Amount = payslip.CnssEmployee, IsDeduction = true });
        if (payslip.Irpp > 0)
            lines.Add(new SoldeToutCompteLineDto { Label = "IRPP", Amount = payslip.Irpp, IsDeduction = true });
        if (payslip.IrppRegularization != 0)
            lines.Add(new SoldeToutCompteLineDto { Label = "Régularisation IRPP", Amount = payslip.IrppRegularization, IsDeduction = payslip.IrppRegularization > 0 });
        if (payslip.Css > 0)
            lines.Add(new SoldeToutCompteLineDto { Label = "CSS", Amount = payslip.Css, IsDeduction = true });
        if (payslip.CssRegularization != 0)
            lines.Add(new SoldeToutCompteLineDto { Label = "Régularisation CSS", Amount = payslip.CssRegularization, IsDeduction = payslip.CssRegularization > 0 });
        if (payslip.OtherDeductions > 0)
            lines.Add(new SoldeToutCompteLineDto { Label = "Autres retenues", Amount = payslip.OtherDeductions, IsDeduction = true });

        lines.Add(new SoldeToutCompteLineDto { Label = "Net à payer", Amount = payslip.NetSalary, IsDeduction = false });
        return lines;
    }

    internal static string FormatMonth(int year, int month) =>
        month is >= 1 and <= 12 ? $"{MonthNamesFr[month]} {year}" : $"{month}/{year}";
}
