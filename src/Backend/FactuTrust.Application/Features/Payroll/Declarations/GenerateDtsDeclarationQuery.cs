using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Declarations;

/// <summary>
/// Déclaration Trimestrielle des Salaires (DTS CNSS) : récapitulatif des salaires et
/// cotisations CNSS par salarié sur un trimestre civil, agrégé depuis les bulletins.
/// </summary>
public sealed record GenerateDtsDeclarationQuery(int Year, int Quarter) : IRequest<Result<DtsDeclarationDto>>;

public sealed class GenerateDtsDeclarationQueryHandler : IRequestHandler<GenerateDtsDeclarationQuery, Result<DtsDeclarationDto>>
{
    private readonly IPayrollRunRepository _runs;

    public GenerateDtsDeclarationQueryHandler(IPayrollRunRepository runs)
    {
        _runs = runs;
    }

    public async Task<Result<DtsDeclarationDto>> Handle(GenerateDtsDeclarationQuery request, CancellationToken cancellationToken)
    {
        if (request.Quarter is < 1 or > 4)
            return Result.Failure<DtsDeclarationDto>(Error.Validation("Quarter", "Le trimestre doit être compris entre 1 et 4."));

        var runs = await _runs.ListByQuarterWithPayslipsAsync(request.Year, request.Quarter, cancellationToken);

        var byEmployee = runs
            .SelectMany(r => r.Payslips)
            .GroupBy(p => p.EmployeeId)
            .Select(g => new DtsLineDto
            {
                EmployeeId = g.Key,
                EmployeeName = g.First().EmployeeName,
                CnssNumber = g.First().CnssNumber,
                TotalGross = Round(g.Sum(p => p.GrossSalary)),
                TotalCnssableGross = Round(g.Sum(p => p.CnssableGross)),
                CnssEmployee = Round(g.Sum(p => p.CnssEmployee)),
                CnssEmployer = Round(g.Sum(p => p.CnssEmployer)),
                MonthsCount = g.Select(p => p.Month).Distinct().Count()
            })
            .OrderBy(l => l.EmployeeName)
            .ToList();

        var dto = new DtsDeclarationDto
        {
            Year = request.Year,
            Quarter = request.Quarter,
            TotalCnssableGross = Round(byEmployee.Sum(l => l.TotalCnssableGross)),
            TotalCnssEmployee = Round(byEmployee.Sum(l => l.CnssEmployee)),
            TotalCnssEmployer = Round(byEmployee.Sum(l => l.CnssEmployer)),
            TotalContributions = Round(byEmployee.Sum(l => l.CnssEmployee + l.CnssEmployer)),
            EmployeeCount = byEmployee.Count,
            Lines = byEmployee
        };

        return Result.Success(dto);
    }

    private static decimal Round(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}
