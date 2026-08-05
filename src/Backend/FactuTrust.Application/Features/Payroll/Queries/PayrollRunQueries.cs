using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Payroll;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Queries;

// ── List ──
public sealed record GetPayrollRunsQuery(int? Year) : IRequest<IReadOnlyList<PayrollRunListDto>>;

public sealed class GetPayrollRunsQueryHandler : IRequestHandler<GetPayrollRunsQuery, IReadOnlyList<PayrollRunListDto>>
{
    private readonly IPayrollRunRepository _runs;

    public GetPayrollRunsQueryHandler(IPayrollRunRepository runs)
    {
        _runs = runs;
    }

    public async Task<IReadOnlyList<PayrollRunListDto>> Handle(GetPayrollRunsQuery request, CancellationToken cancellationToken)
    {
        var runs = await _runs.ListAsync(request.Year, cancellationToken);
        var result = new List<PayrollRunListDto>(runs.Count);
        foreach (var run in runs)
        {
            var withPayslips = await _runs.GetByIdWithPayslipsAsync(run.Id, cancellationToken);
            result.Add(PayrollMappings.ToListDto(run, withPayslips?.Payslips.Count ?? 0));
        }
        return result;
    }
}

// ── Detail ──
public sealed record GetPayrollRunByIdQuery(Guid Id) : IRequest<Result<PayrollRunDetailDto>>;

public sealed class GetPayrollRunByIdQueryHandler : IRequestHandler<GetPayrollRunByIdQuery, Result<PayrollRunDetailDto>>
{
    private readonly IPayrollRunRepository _runs;
    private readonly IPayrollOvertimeRepository _overtime;
    private readonly IPayrollVariableAllowanceRepository _variableAllowances;
    private readonly IEmployeeRepository _employees;

    public GetPayrollRunByIdQueryHandler(
        IPayrollRunRepository runs,
        IPayrollOvertimeRepository overtime,
        IPayrollVariableAllowanceRepository variableAllowances,
        IEmployeeRepository employees)
    {
        _runs = runs;
        _overtime = overtime;
        _variableAllowances = variableAllowances;
        _employees = employees;
    }

    public async Task<Result<PayrollRunDetailDto>> Handle(GetPayrollRunByIdQuery request, CancellationToken cancellationToken)
    {
        var run = await _runs.GetByIdWithPayslipsAsync(request.Id, cancellationToken);
        if (run is null)
            return Result.Failure<PayrollRunDetailDto>(Error.NotFound("PayrollRun", request.Id));

        var overtimeLines = await _overtime.ListForMonthAsync(run.Year, run.Month, cancellationToken);
        var variableAllowanceLines = await _variableAllowances.ListForMonthAsync(run.Year, run.Month, cancellationToken);
        var employeeIds = overtimeLines.Select(l => l.EmployeeId)
            .Concat(variableAllowanceLines.Select(l => l.EmployeeId))
            .Distinct()
            .ToList();
        var names = await _employees.GetFullNamesByIdsAsync(employeeIds, cancellationToken);

        var overtimeDtos = overtimeLines
            .Select(l => PayrollMappings.ToOvertimeDto(l, names.GetValueOrDefault(l.EmployeeId)))
            .ToList();
        var variableAllowanceDtos = variableAllowanceLines
            .Select(l => PayrollMappings.ToVariableAllowanceDto(l, names.GetValueOrDefault(l.EmployeeId)))
            .ToList();

        var dto = PayrollMappings.ToDetailDto(run) with
        {
            OvertimeLines = overtimeDtos,
            VariableAllowanceLines = variableAllowanceDtos
        };
        return Result.Success(dto);
    }
}
