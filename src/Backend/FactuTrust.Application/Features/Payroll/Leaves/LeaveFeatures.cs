using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Payroll.LeaveBalance;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FluentValidation;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Leaves;

// ── Create leave ──
public sealed record CreateLeaveCommand(CreateLeaveDto Dto) : IRequest<Result<Guid>>;

public sealed class CreateLeaveCommandValidator : AbstractValidator<CreateLeaveCommand>
{
    public CreateLeaveCommandValidator()
    {
        RuleFor(x => x.Dto.EmployeeId).NotEmpty();
        RuleFor(x => x.Dto.Days).GreaterThan(0).WithMessage("Le nombre de jours doit être strictement positif.");
        RuleFor(x => x.Dto).Must(d => d.EndDate >= d.StartDate)
            .WithMessage("La date de fin doit être postérieure ou égale à la date de début.");
    }
}

public sealed class CreateLeaveCommandHandler : IRequestHandler<CreateLeaveCommand, Result<Guid>>
{
    private readonly ILeaveRequestRepository _leaves;
    private readonly IEmployeeRepository _employees;
    private readonly ILeaveBalanceAccrualRepository _accruals;

    public CreateLeaveCommandHandler(
        ILeaveRequestRepository leaves,
        IEmployeeRepository employees,
        ILeaveBalanceAccrualRepository accruals)
    {
        _leaves = leaves;
        _employees = employees;
        _accruals = accruals;
    }

    public async Task<Result<Guid>> Handle(CreateLeaveCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto;

        if (!await _employees.ExistsAsync(dto.EmployeeId, cancellationToken))
            return Result.Failure<Guid>(Error.NotFound("Employee", dto.EmployeeId));

        if (!Enum.TryParse<LeaveType>(dto.Type, out var type))
            return Result.Failure<Guid>(Error.Validation("Type", "Type de congé invalide."));

        var balanceCheck = await LeaveBalanceQueryHelper.EnsurePaidLeaveCanBeCreatedAsync(
            dto.EmployeeId, type, dto.StartDate, dto.Days, _employees, _accruals, _leaves, cancellationToken);
        if (balanceCheck.IsFailure)
            return Result.Failure<Guid>(balanceCheck.Error);

        var leaveResult = LeaveRequest.Create(dto.EmployeeId, type, dto.StartDate, dto.EndDate, dto.Days, dto.Reason);
        if (leaveResult.IsFailure)
            return Result.Failure<Guid>(leaveResult.Error);

        await _leaves.AddAsync(leaveResult.Value, cancellationToken);
        return Result.Success(leaveResult.Value.Id);
    }
}

// ── Approve leave ──
public sealed record ApproveLeaveCommand(Guid Id) : IRequest<Result>;

public sealed class ApproveLeaveCommandHandler : IRequestHandler<ApproveLeaveCommand, Result>
{
    private readonly ILeaveRequestRepository _leaves;
    private readonly IEmployeeRepository _employees;
    private readonly ILeaveBalanceAccrualRepository _accruals;
    private readonly ICurrentUser _currentUser;

    public ApproveLeaveCommandHandler(
        ILeaveRequestRepository leaves,
        IEmployeeRepository employees,
        ILeaveBalanceAccrualRepository accruals,
        ICurrentUser currentUser)
    {
        _leaves = leaves;
        _employees = employees;
        _accruals = accruals;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(ApproveLeaveCommand request, CancellationToken cancellationToken)
    {
        var leave = await _leaves.GetByIdAsync(request.Id, cancellationToken);
        if (leave is null)
            return Result.Failure(Error.NotFound("LeaveRequest", request.Id));

        if (leave.IsApproved)
            return Result.Success();

        var balanceCheck = await LeaveBalanceQueryHelper.EnsurePaidLeaveCanBeApprovedAsync(
            leave, _employees, _accruals, _leaves, cancellationToken);
        if (balanceCheck.IsFailure)
            return balanceCheck;

        leave.Approve(_currentUser.Email ?? _currentUser.UserId?.ToString() ?? "system");
        await _leaves.UpdateAsync(leave, cancellationToken);
        return Result.Success();
    }
}

// ── Delete leave ──
public sealed record DeleteLeaveCommand(Guid Id) : IRequest<Result>;

public sealed class DeleteLeaveCommandHandler : IRequestHandler<DeleteLeaveCommand, Result>
{
    private readonly ILeaveRequestRepository _leaves;

    public DeleteLeaveCommandHandler(ILeaveRequestRepository leaves)
    {
        _leaves = leaves;
    }

    public async Task<Result> Handle(DeleteLeaveCommand request, CancellationToken cancellationToken)
    {
        var leave = await _leaves.GetByIdAsync(request.Id, cancellationToken);
        if (leave is null)
            return Result.Failure(Error.NotFound("LeaveRequest", request.Id));

        await _leaves.DeleteAsync(leave, cancellationToken);
        return Result.Success();
    }
}

// ── List leaves by employee ──
public sealed record GetEmployeeLeavesQuery(Guid EmployeeId) : IRequest<IReadOnlyList<LeaveRequestDto>>;

public sealed class GetEmployeeLeavesQueryHandler : IRequestHandler<GetEmployeeLeavesQuery, IReadOnlyList<LeaveRequestDto>>
{
    private readonly ILeaveRequestRepository _leaves;

    public GetEmployeeLeavesQueryHandler(ILeaveRequestRepository leaves)
    {
        _leaves = leaves;
    }

    public async Task<IReadOnlyList<LeaveRequestDto>> Handle(GetEmployeeLeavesQuery request, CancellationToken cancellationToken)
    {
        var leaves = await _leaves.ListByEmployeeAsync(request.EmployeeId, cancellationToken);
        return leaves.Select(l => PayrollMappings.ToDto(l)).ToList();
    }
}
