using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Payroll;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using FluentValidation;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.LeaveBalance;

public sealed record GetEmployeeLeaveBalanceQuery(Guid EmployeeId, int Year) : IRequest<Result<LeaveBalanceDto>>;

public sealed class GetEmployeeLeaveBalanceQueryHandler : IRequestHandler<GetEmployeeLeaveBalanceQuery, Result<LeaveBalanceDto>>
{
    private readonly IEmployeeRepository _employees;
    private readonly ILeaveBalanceAccrualRepository _accruals;
    private readonly ILeaveRequestRepository _leaves;

    public GetEmployeeLeaveBalanceQueryHandler(
        IEmployeeRepository employees,
        ILeaveBalanceAccrualRepository accruals,
        ILeaveRequestRepository leaves)
    {
        _employees = employees;
        _accruals = accruals;
        _leaves = leaves;
    }

    public async Task<Result<LeaveBalanceDto>> Handle(GetEmployeeLeaveBalanceQuery request, CancellationToken cancellationToken)
    {
        var employee = await _employees.GetByIdAsync(request.EmployeeId, cancellationToken);
        if (employee is null)
            return Result.Failure<LeaveBalanceDto>(Error.NotFound("Employee", request.EmployeeId));

        var dto = await GetEmployeeLeaveBalanceQueryHandler.BuildBalanceDtoAsync(
            employee, request.Year, _accruals, _leaves, cancellationToken);
        return Result.Success(dto);
    }

    internal static async Task<LeaveBalanceDto> BuildBalanceDtoAsync(
        Employee employee,
        int year,
        ILeaveBalanceAccrualRepository accruals,
        ILeaveRequestRepository leaves,
        CancellationToken cancellationToken)
    {
        var accrualRows = await accruals.ListByEmployeeAndYearAsync(employee.Id, year, cancellationToken);
        var accruedInYear = LeaveBalanceService.SumAccruedDays(accrualRows.Select(a => a.AccruedDays));

        var allLeaves = await leaves.ListByEmployeeAsync(employee.Id, cancellationToken);
        var consumed = LeaveBalanceService.SumConsumedPaidLeaveDays(
            allLeaves.Select(l => (l.Type, l.IsApproved, l.Days, l.StartDate.Year)),
            year);

        var opening = employee.LeaveOpeningBalanceDays;
        var totalAcquired = LeaveBalanceService.ComputeTotalAcquired(opening, accruedInYear);
        var remaining = LeaveBalanceService.ComputeRemaining(opening, accruedInYear, consumed);

        return new LeaveBalanceDto
        {
            EmployeeId = employee.Id,
            Year = year,
            OpeningBalance = opening,
            AccruedInYear = accruedInYear,
            TotalAcquired = totalAcquired,
            Consumed = consumed,
            Remaining = remaining
        };
    }
}

public sealed record SetLeaveOpeningBalanceCommand(Guid EmployeeId, SetLeaveOpeningBalanceDto Dto) : IRequest<Result>;

public sealed class SetLeaveOpeningBalanceCommandValidator : AbstractValidator<SetLeaveOpeningBalanceCommand>
{
    public SetLeaveOpeningBalanceCommandValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.Dto.OpeningBalanceDays).GreaterThanOrEqualTo(0);
    }
}

public sealed class SetLeaveOpeningBalanceCommandHandler : IRequestHandler<SetLeaveOpeningBalanceCommand, Result>
{
    private readonly IEmployeeRepository _employees;

    public SetLeaveOpeningBalanceCommandHandler(IEmployeeRepository employees)
    {
        _employees = employees;
    }

    public async Task<Result> Handle(SetLeaveOpeningBalanceCommand request, CancellationToken cancellationToken)
    {
        var employee = await _employees.GetByIdAsync(request.EmployeeId, cancellationToken);
        if (employee is null)
            return Result.Failure(Error.NotFound("Employee", request.EmployeeId));

        var setResult = employee.SetLeaveOpeningBalance(request.Dto.OpeningBalanceDays);
        if (setResult.IsFailure)
            return setResult;

        await _employees.UpdateAsync(employee, cancellationToken);
        return Result.Success();
    }
}

/// <summary>
/// Shared helper for leave balance checks and DTO building.
/// </summary>
public static class LeaveBalanceQueryHelper
{
    public static async Task<LeaveBalanceDto> BuildBalanceDtoAsync(
        Employee employee,
        int year,
        ILeaveBalanceAccrualRepository accruals,
        ILeaveRequestRepository leaves,
        CancellationToken cancellationToken) =>
        await GetEmployeeLeaveBalanceQueryHandler.BuildBalanceDtoAsync(
            employee, year, accruals, leaves, cancellationToken);

    public static async Task<Result> EnsurePaidLeaveCanBeApprovedAsync(
        LeaveRequest leave,
        IEmployeeRepository employees,
        ILeaveBalanceAccrualRepository accruals,
        ILeaveRequestRepository leaveRepo,
        CancellationToken cancellationToken)
    {
        if (leave.Type != LeaveType.Paid)
            return Result.Success();

        var employee = await employees.GetByIdAsync(leave.EmployeeId, cancellationToken);
        if (employee is null)
            return Result.Failure(Error.NotFound("Employee", leave.EmployeeId));

        var year = leave.StartDate.Year;
        var balance = await BuildBalanceDtoAsync(employee, year, accruals, leaveRepo, cancellationToken);

        // Exclude this leave if already approved (re-approve edge case).
        var requiredDays = leave.Days;
        if (leave.IsApproved)
            return Result.Success();

        if (balance.Remaining < requiredDays)
        {
            return Result.Failure(Error.Validation(
                "LeaveBalance",
                $"Solde congés insuffisant (reste : {balance.Remaining:0.###} j)."));
        }

        return Result.Success();
    }
}
