using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Commands;

// ── Validate ──
public sealed record ValidatePayrollRunCommand(Guid RunId) : IRequest<Result>;

public sealed class ValidatePayrollRunCommandHandler : IRequestHandler<ValidatePayrollRunCommand, Result>
{
    private readonly IPayrollRunRepository _runs;
    private readonly IEmployeeAdvanceRepository _advances;
    private readonly ILeaveRequestRepository _leaves;
    private readonly ILeaveBalanceAccrualRepository _accruals;
    private readonly ICurrentUser _currentUser;

    public ValidatePayrollRunCommandHandler(
        IPayrollRunRepository runs,
        IEmployeeAdvanceRepository advances,
        ILeaveRequestRepository leaves,
        ILeaveBalanceAccrualRepository accruals,
        ICurrentUser currentUser)
    {
        _runs = runs;
        _advances = advances;
        _leaves = leaves;
        _accruals = accruals;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(ValidatePayrollRunCommand request, CancellationToken cancellationToken)
    {
        var run = await _runs.GetByIdWithPayslipsAsync(request.RunId, cancellationToken);
        if (run is null)
            return Result.Failure(Error.NotFound("PayrollRun", request.RunId));

        var employeeIds = run.Payslips.Select(p => p.EmployeeId).Distinct().ToList();
        var monthLeaves = await _leaves.ListForMonthAsync(run.Year, run.Month, cancellationToken);

        var validatedBy = _currentUser.Email ?? _currentUser.UserId?.ToString() ?? "system";
        var validateResult = run.Validate(validatedBy);
        if (validateResult.IsFailure)
            return validateResult;

        await _runs.UpdateScalarAsync(run, cancellationToken);

        foreach (var employeeId in employeeIds)
        {
            var outstanding = await _advances.ListOutstandingAsync(employeeId, cancellationToken);
            foreach (var advance in outstanding)
            {
                advance.Settle(run.Id);
                await _advances.UpdateAsync(advance, cancellationToken);
            }

            var existingAccrual = await _accruals.GetByEmployeePeriodAsync(employeeId, run.Year, run.Month, cancellationToken);
            if (existingAccrual is not null)
                continue;

            var unpaidDays = monthLeaves
                .Where(l => l.EmployeeId == employeeId && l.Type.ReducesGross())
                .Sum(l => l.Days);
            var workedDays = LeaveBalanceService.ComputeWorkedDays(unpaidDays);

            var accrualResult = LeaveBalanceAccrual.Create(employeeId, run.Year, run.Month, workedDays, run.Id);
            if (accrualResult.IsFailure)
                return accrualResult;

            await _accruals.AddAsync(accrualResult.Value, cancellationToken);
        }

        return Result.Success();
    }
}

// ── Reopen ──
public sealed record ReopenPayrollRunCommand(Guid RunId) : IRequest<Result>;

public sealed class ReopenPayrollRunCommandHandler : IRequestHandler<ReopenPayrollRunCommand, Result>
{
    private readonly IPayrollRunRepository _runs;
    private readonly IEmployeeAdvanceRepository _advances;
    private readonly ILeaveBalanceAccrualRepository _accruals;

    public ReopenPayrollRunCommandHandler(
        IPayrollRunRepository runs,
        IEmployeeAdvanceRepository advances,
        ILeaveBalanceAccrualRepository accruals)
    {
        _runs = runs;
        _advances = advances;
        _accruals = accruals;
    }

    public async Task<Result> Handle(ReopenPayrollRunCommand request, CancellationToken cancellationToken)
    {
        var run = await _runs.GetByIdAsync(request.RunId, cancellationToken);
        if (run is null)
            return Result.Failure(Error.NotFound("PayrollRun", request.RunId));

        var reopenResult = run.Reopen();
        if (reopenResult.IsFailure)
            return reopenResult;

        await _runs.UpdateScalarAsync(run, cancellationToken);

        var settled = (await _advances.GetAllAsync(cancellationToken))
            .Where(a => a.SettledInPayrollRunId == run.Id);
        foreach (var advance in settled)
        {
            advance.Unsettle();
            await _advances.UpdateAsync(advance, cancellationToken);
        }

        await _accruals.DeleteByPayrollRunIdAsync(run.Id, cancellationToken);

        return Result.Success();
    }
}

// ── Close ──
public sealed record ClosePayrollRunCommand(Guid RunId) : IRequest<Result>;

public sealed class ClosePayrollRunCommandHandler : IRequestHandler<ClosePayrollRunCommand, Result>
{
    private readonly IPayrollRunRepository _runs;

    public ClosePayrollRunCommandHandler(IPayrollRunRepository runs)
    {
        _runs = runs;
    }

    public async Task<Result> Handle(ClosePayrollRunCommand request, CancellationToken cancellationToken)
    {
        var run = await _runs.GetByIdAsync(request.RunId, cancellationToken);
        if (run is null)
            return Result.Failure(Error.NotFound("PayrollRun", request.RunId));

        var closeResult = run.Close();
        if (closeResult.IsFailure)
            return closeResult;

        await _runs.UpdateScalarAsync(run, cancellationToken);
        return Result.Success();
    }
}
