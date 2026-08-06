using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Payroll;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FluentValidation;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Suspensions;

public sealed record CreateEmployeePayrollSuspensionCommand(CreateEmployeePayrollSuspensionDto Dto)
    : IRequest<Result<Guid>>;

public sealed class CreateEmployeePayrollSuspensionCommandValidator
    : AbstractValidator<CreateEmployeePayrollSuspensionCommand>
{
    public CreateEmployeePayrollSuspensionCommandValidator()
    {
        RuleFor(x => x.Dto.EmployeeId).NotEmpty();
        RuleFor(x => x.Dto).Must(d => !d.EndDate.HasValue || d.EndDate >= d.StartDate)
            .WithMessage("La date de fin doit être postérieure ou égale à la date de début.");
    }
}

public sealed class CreateEmployeePayrollSuspensionCommandHandler
    : IRequestHandler<CreateEmployeePayrollSuspensionCommand, Result<Guid>>
{
    private readonly IEmployeePayrollSuspensionRepository _suspensions;
    private readonly IEmployeeRepository _employees;

    public CreateEmployeePayrollSuspensionCommandHandler(
        IEmployeePayrollSuspensionRepository suspensions,
        IEmployeeRepository employees)
    {
        _suspensions = suspensions;
        _employees = employees;
    }

    public async Task<Result<Guid>> Handle(
        CreateEmployeePayrollSuspensionCommand request,
        CancellationToken cancellationToken)
    {
        var dto = request.Dto;
        if (!await _employees.ExistsAsync(dto.EmployeeId, cancellationToken))
            return Result.Failure<Guid>(Error.NotFound("Employee", dto.EmployeeId));

        if (!Enum.TryParse<PayrollSuspensionType>(dto.Type, out var type))
            return Result.Failure<Guid>(Error.Validation("Type", "Type de suspension invalide."));

        var result = EmployeePayrollSuspension.Create(
            dto.EmployeeId, type, dto.StartDate, dto.EndDate, dto.IsPaid, dto.Reason);
        if (result.IsFailure)
            return Result.Failure<Guid>(result.Error);

        await _suspensions.AddAsync(result.Value, cancellationToken);
        return Result.Success(result.Value.Id);
    }
}

public sealed record UpdateEmployeePayrollSuspensionCommand(Guid Id, UpdateEmployeePayrollSuspensionDto Dto)
    : IRequest<Result>;

public sealed class UpdateEmployeePayrollSuspensionCommandHandler
    : IRequestHandler<UpdateEmployeePayrollSuspensionCommand, Result>
{
    private readonly IEmployeePayrollSuspensionRepository _suspensions;

    public UpdateEmployeePayrollSuspensionCommandHandler(IEmployeePayrollSuspensionRepository suspensions)
    {
        _suspensions = suspensions;
    }

    public async Task<Result> Handle(UpdateEmployeePayrollSuspensionCommand request, CancellationToken cancellationToken)
    {
        var suspension = await _suspensions.GetByIdAsync(request.Id, cancellationToken);
        if (suspension is null)
            return Result.Failure(Error.NotFound("EmployeePayrollSuspension", request.Id));

        if (suspension.IsApproved)
            return Result.Failure(Error.Validation("Status", "Une suspension approuvée ne peut plus être modifiée."));

        var dto = request.Dto;
        if (!Enum.TryParse<PayrollSuspensionType>(dto.Type, out var type))
            return Result.Failure(Error.Validation("Type", "Type de suspension invalide."));

        var update = suspension.Update(type, dto.StartDate, dto.EndDate, dto.IsPaid, dto.Reason);
        if (update.IsFailure)
            return update;

        await _suspensions.UpdateAsync(suspension, cancellationToken);
        return Result.Success();
    }
}

public sealed record DeleteEmployeePayrollSuspensionCommand(Guid Id) : IRequest<Result>;

public sealed class DeleteEmployeePayrollSuspensionCommandHandler
    : IRequestHandler<DeleteEmployeePayrollSuspensionCommand, Result>
{
    private readonly IEmployeePayrollSuspensionRepository _suspensions;

    public DeleteEmployeePayrollSuspensionCommandHandler(IEmployeePayrollSuspensionRepository suspensions)
    {
        _suspensions = suspensions;
    }

    public async Task<Result> Handle(DeleteEmployeePayrollSuspensionCommand request, CancellationToken cancellationToken)
    {
        var suspension = await _suspensions.GetByIdAsync(request.Id, cancellationToken);
        if (suspension is null)
            return Result.Failure(Error.NotFound("EmployeePayrollSuspension", request.Id));

        if (suspension.IsApproved)
            return Result.Failure(Error.Validation("Status", "Une suspension approuvée ne peut pas être supprimée."));

        await _suspensions.DeleteAsync(suspension, cancellationToken);
        return Result.Success();
    }
}

public sealed record ApproveEmployeePayrollSuspensionCommand(Guid Id) : IRequest<Result>;

public sealed class ApproveEmployeePayrollSuspensionCommandHandler
    : IRequestHandler<ApproveEmployeePayrollSuspensionCommand, Result>
{
    private readonly IEmployeePayrollSuspensionRepository _suspensions;
    private readonly ICurrentUser _currentUser;

    public ApproveEmployeePayrollSuspensionCommandHandler(
        IEmployeePayrollSuspensionRepository suspensions,
        ICurrentUser currentUser)
    {
        _suspensions = suspensions;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(ApproveEmployeePayrollSuspensionCommand request, CancellationToken cancellationToken)
    {
        var suspension = await _suspensions.GetByIdAsync(request.Id, cancellationToken);
        if (suspension is null)
            return Result.Failure(Error.NotFound("EmployeePayrollSuspension", request.Id));

        if (!suspension.IsApproved)
        {
            suspension.Approve(_currentUser.Email ?? _currentUser.UserId?.ToString() ?? "system");
            await _suspensions.UpdateAsync(suspension, cancellationToken);
        }

        return Result.Success();
    }
}

public sealed record GetEmployeePayrollSuspensionsQuery(Guid EmployeeId)
    : IRequest<IReadOnlyList<EmployeePayrollSuspensionDto>>;

public sealed class GetEmployeePayrollSuspensionsQueryHandler
    : IRequestHandler<GetEmployeePayrollSuspensionsQuery, IReadOnlyList<EmployeePayrollSuspensionDto>>
{
    private readonly IEmployeePayrollSuspensionRepository _suspensions;

    public GetEmployeePayrollSuspensionsQueryHandler(IEmployeePayrollSuspensionRepository suspensions)
    {
        _suspensions = suspensions;
    }

    public async Task<IReadOnlyList<EmployeePayrollSuspensionDto>> Handle(
        GetEmployeePayrollSuspensionsQuery request,
        CancellationToken cancellationToken)
    {
        var items = await _suspensions.ListByEmployeeAsync(request.EmployeeId, cancellationToken);
        return items.Select(PayrollMappings.ToDto).ToList();
    }
}
