using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Services.Payroll;

namespace FactuTrust.Application.Features.Payroll.Declarations;

/// <summary>
/// Charge les bulletins arrêtés d'un exercice et construit le lot de certificats RS.
/// </summary>
internal sealed class PayrollWithholdingCertificateDataLoader
{
    private readonly IPayrollRunRepository _runs;
    private readonly IEmployeeRepository _employees;
    private readonly ITenantCompanySummaryProvider _companySummary;

    public PayrollWithholdingCertificateDataLoader(
        IPayrollRunRepository runs,
        IEmployeeRepository employees,
        ITenantCompanySummaryProvider companySummary)
    {
        _runs = runs;
        _employees = employees;
        _companySummary = companySummary;
    }

    public async Task<Result<PayrollWithholdingCertificateBatchDto>> LoadAsync(
        int year,
        CancellationToken cancellationToken)
    {
        if (year is < 2000 or > 2100)
            return Result.Failure<PayrollWithholdingCertificateBatchDto>(
                Error.Validation("Year", "L'exercice doit être compris entre 2000 et 2100."));

        var company = await _companySummary.GetCurrentTenantSummaryAsync(cancellationToken);
        if (company is null)
        {
            return Result.Failure<PayrollWithholdingCertificateBatchDto>(
                Error.Validation("Company", "Impossible de charger les informations de la société."));
        }

        if (string.IsNullOrWhiteSpace(company.Nif))
        {
            return Result.Failure<PayrollWithholdingCertificateBatchDto>(
                Error.Validation("Nif", "Le matricule fiscal (NIF) de l'employeur est obligatoire pour générer les certificats."));
        }

        var runs = await _runs.ListByMonthRangeWithPayslipsAsync(year, 1, 12, includeCalculated: false, cancellationToken);
        var includedMonths = runs.Select(r => r.Month).Distinct().OrderBy(m => m).ToList();
        var payslips = runs.SelectMany(r => r.Payslips).ToList();

        var employeeIds = payslips.Select(p => p.EmployeeId).Distinct().ToList();
        var employeeMap = await _employees.GetByIdsAsync(employeeIds, cancellationToken);

        var identitySnapshots = employeeMap.Values.ToDictionary(
            e => e.Id,
            e => new EmployeeIdentitySnapshot(
                e.Id,
                e.EmployeeNumber,
                e.FullName,
                e.Cin,
                e.CnssNumber,
                e.Address?.ToSingleLine(),
                e.IsHeadOfFamily));

        var employer = new EmployerSnapshot(company.CompanyName, company.Nif, company.AddressLine, company.CnssEmployerNumber);
        var batch = PayrollWithholdingCertificateBuilder.Build(
            year,
            payslips,
            identitySnapshots,
            employer,
            includedMonths);

        return Result.Success(ToDto(batch));
    }

    internal static PayrollWithholdingCertificateBatchDto ToDto(PayrollWithholdingCertificateBatch batch) =>
        new()
        {
            Year = batch.Year,
            EmployerCompanyName = batch.EmployerCompanyName,
            EmployerNif = batch.EmployerNif,
            EmployerAddressLine = batch.EmployerAddressLine,
            EmployeeCount = batch.EmployeeCount,
            IncludedMonths = batch.IncludedMonths,
            MissingMonths = batch.MissingMonths,
            IsComplete = batch.IsComplete,
            TotalGross = batch.TotalGross,
            TotalAnnualNetTaxable = batch.TotalAnnualNetTaxable,
            TotalIrppWithheld = batch.TotalIrppWithheld,
            TotalCssWithheld = batch.TotalCssWithheld,
            TotalWithholding = batch.TotalWithholding,
            Lines = batch.Lines.Select(ToLineDto).ToList()
        };

    internal static PayrollWithholdingCertificateLineDto ToLineDto(PayrollWithholdingCertificateLine line) =>
        new()
        {
            EmployeeId = line.EmployeeId,
            EmployeeNumber = line.EmployeeNumber,
            EmployeeName = line.EmployeeName,
            Cin = line.Cin,
            CnssNumber = line.CnssNumber,
            AddressLine = line.AddressLine,
            IsHeadOfFamily = line.IsHeadOfFamily,
            MonthsCount = line.MonthsCount,
            IsPartialYear = line.IsPartialYear,
            TotalGross = line.TotalGross,
            TotalCnssableGross = line.TotalCnssableGross,
            TotalCnssEmployee = line.TotalCnssEmployee,
            TotalProfessionalExpenses = line.TotalProfessionalExpenses,
            TotalFamilyDeductions = line.TotalFamilyDeductions,
            AnnualNetTaxable = line.AnnualNetTaxable,
            TotalIrppWithheld = line.TotalIrppWithheld,
            TotalCssWithheld = line.TotalCssWithheld,
            TotalWithholding = line.TotalWithholding,
            TotalIrppSmigExemption = line.TotalIrppSmigExemption,
            DocumentReference = $"CRS-{line.EmployeeNumber}",
            Warnings = line.Warnings,
            Months = line.Months.Select(m => new PayrollWithholdingCertificateMonthDto
            {
                Month = m.Month,
                MonthLabel = m.MonthLabel,
                MonthlyNetTaxable = m.MonthlyNetTaxable,
                Irpp = m.Irpp,
                IrppRegularization = m.IrppRegularization,
                Css = m.Css,
                CssRegularization = m.CssRegularization,
                HasPayslip = m.HasPayslip
            }).ToList()
        };
}
