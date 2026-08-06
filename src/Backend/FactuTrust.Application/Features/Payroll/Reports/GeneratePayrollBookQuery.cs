using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Reports;

/// <summary>
/// Livre de paie simplifié : registre des salaires par salarié sur une plage de mois, cumulé
/// depuis les bulletins gelés. Aucun recalcul — les montants sont ceux figés au calcul du cycle.
/// </summary>
public sealed record GeneratePayrollBookQuery(
    int Year,
    int FromMonth,
    int ToMonth,
    bool IncludeCalculated = false) : IRequest<Result<PayrollBookDto>>;

public sealed class GeneratePayrollBookQueryHandler
    : IRequestHandler<GeneratePayrollBookQuery, Result<PayrollBookDto>>
{
    private readonly IPayrollRunRepository _runs;
    private readonly IEmployeeRepository _employees;

    public GeneratePayrollBookQueryHandler(IPayrollRunRepository runs, IEmployeeRepository employees)
    {
        _runs = runs;
        _employees = employees;
    }

    public async Task<Result<PayrollBookDto>> Handle(GeneratePayrollBookQuery request, CancellationToken cancellationToken)
    {
        if (request.Year is < 2000 or > 2100)
            return Result.Failure<PayrollBookDto>(Error.Validation("Year", "L'année doit être comprise entre 2000 et 2100."));
        if (request.FromMonth is < 1 or > 12)
            return Result.Failure<PayrollBookDto>(Error.Validation("FromMonth", "Le mois de début doit être compris entre 1 et 12."));
        if (request.ToMonth is < 1 or > 12)
            return Result.Failure<PayrollBookDto>(Error.Validation("ToMonth", "Le mois de fin doit être compris entre 1 et 12."));
        if (request.FromMonth > request.ToMonth)
            return Result.Failure<PayrollBookDto>(Error.Validation("ToMonth", "Le mois de fin ne peut pas précéder le mois de début."));

        var runs = await _runs.ListByMonthRangeWithPayslipsAsync(
            request.Year, request.FromMonth, request.ToMonth, request.IncludeCalculated, cancellationToken);

        var expectedMonths = Enumerable.Range(request.FromMonth, request.ToMonth - request.FromMonth + 1).ToList();
        var includedMonths = runs.Select(r => r.Month).Distinct().OrderBy(m => m).ToList();
        var missingMonths = expectedMonths.Except(includedMonths).ToList();
        var provisionalMonths = runs
            .Where(r => r.Status == PayrollRunStatus.Calculated)
            .Select(r => r.Month)
            .Distinct()
            .OrderBy(m => m)
            .ToList();

        var payslips = runs.SelectMany(r => r.Payslips).ToList();

        // Identités enrichies en une seule requête (CIN, catégorie, échelon, date d'embauche) :
        // le bulletin ne fige que le nom, le matricule et le n° CNSS.
        var employeeIds = payslips.Select(p => p.EmployeeId).Distinct().ToList();
        var employees = employeeIds.Count > 0
            ? await _employees.GetByIdsAsync(employeeIds, cancellationToken)
            : new Dictionary<Guid, Domain.Entities.Payroll.Employee>();

        var lines = payslips
            .GroupBy(p => p.EmployeeId)
            .Select(g =>
            {
                var first = g.First();
                employees.TryGetValue(g.Key, out var employee);

                return new PayrollBookLineDto
                {
                    EmployeeId = g.Key,
                    EmployeeNumber = first.EmployeeNumber,
                    EmployeeName = first.EmployeeName,
                    Cin = employee?.Cin,
                    CnssNumber = first.CnssNumber,
                    Category = employee?.Category,
                    Echelon = employee?.Echelon,
                    HireDate = employee?.HireDate,
                    MonthsCount = g.Select(p => p.Month).Distinct().Count(),
                    GrossSalary = R(g.Sum(p => p.GrossSalary)),
                    CnssableGross = R(g.Sum(p => p.CnssableGross)),
                    CnssEmployee = R(g.Sum(p => p.CnssEmployee)),
                    ProfessionalExpenses = R(g.Sum(p => p.ProfessionalExpenses)),
                    FamilyDeductions = R(g.Sum(p => p.FamilyDeductions)),
                    NetTaxable = R(g.Sum(p => p.MonthlyNetTaxable)),
                    Irpp = R(g.Sum(p => p.Irpp)),
                    IrppRegularization = R(g.Sum(p => p.IrppRegularization)),
                    Css = R(g.Sum(p => p.Css)),
                    CssRegularization = R(g.Sum(p => p.CssRegularization)),
                    OtherDeductions = R(g.Sum(p => p.OtherDeductions)),
                    NonTaxableAllowances = R(g.Sum(p => p.NonTaxableAllowances)),
                    NetSalary = R(g.Sum(p => p.NetSalary))
                };
            })
            .OrderBy(l => l.EmployeeName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        // Les charges patronales ne figurent pas au détail (registre « simplifié ») mais sont
        // cumulées pour permettre le recoupement avec les totaux des cycles.
        var totalCnssEmployer = R(payslips.Sum(p => p.CnssEmployer));
        var totalWorkAccident = R(payslips.Sum(p => p.WorkAccidentContribution));
        var totalTfp = R(payslips.Sum(p => p.Tfp));
        var totalFoprolos = R(payslips.Sum(p => p.Foprolos));
        var totalCssEmployer = R(payslips.Sum(p => p.CssEmployer));
        var totalEmployerCharges = R(totalCnssEmployer + totalWorkAccident + totalTfp + totalFoprolos + totalCssEmployer);
        var totalGross = R(lines.Sum(l => l.GrossSalary));

        var dto = new PayrollBookDto
        {
            Year = request.Year,
            FromMonth = request.FromMonth,
            ToMonth = request.ToMonth,
            PeriodLabel = PayrollReportHelpers.PeriodLabel(request.Year, request.FromMonth, request.ToMonth),
            IncludeCalculated = request.IncludeCalculated,
            IncludedMonths = includedMonths,
            MissingMonths = missingMonths,
            ProvisionalMonths = provisionalMonths,
            IsProvisional = provisionalMonths.Count > 0,
            EmployeeCount = lines.Count,
            TotalGross = totalGross,
            TotalCnssableGross = R(lines.Sum(l => l.CnssableGross)),
            TotalCnssEmployee = R(lines.Sum(l => l.CnssEmployee)),
            TotalProfessionalExpenses = R(lines.Sum(l => l.ProfessionalExpenses)),
            TotalFamilyDeductions = R(lines.Sum(l => l.FamilyDeductions)),
            TotalNetTaxable = R(lines.Sum(l => l.NetTaxable)),
            TotalIrpp = R(lines.Sum(l => l.Irpp)),
            TotalIrppRegularization = R(lines.Sum(l => l.IrppRegularization)),
            TotalCss = R(lines.Sum(l => l.Css)),
            TotalCssRegularization = R(lines.Sum(l => l.CssRegularization)),
            TotalOtherDeductions = R(lines.Sum(l => l.OtherDeductions)),
            TotalNonTaxableAllowances = R(lines.Sum(l => l.NonTaxableAllowances)),
            TotalNetSalary = R(lines.Sum(l => l.NetSalary)),
            TotalCnssEmployer = totalCnssEmployer,
            TotalWorkAccident = totalWorkAccident,
            TotalTfp = totalTfp,
            TotalFoprolos = totalFoprolos,
            TotalCssEmployer = totalCssEmployer,
            TotalEmployerCharges = totalEmployerCharges,
            TotalEmployerCost = R(totalGross + totalEmployerCharges),
            Lines = lines
        };

        return Result.Success(dto);
    }

    private static decimal R(decimal value) => PayrollReportHelpers.Round(value);
}
