using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Payroll;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FluentValidation;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.InKindBenefits;

public sealed record ListInKindBenefitsForEmployeeQuery(Guid EmployeeId) : IRequest<IReadOnlyList<EmployeeInKindBenefitDto>>;

public sealed class ListInKindBenefitsForEmployeeQueryHandler : IRequestHandler<ListInKindBenefitsForEmployeeQuery, IReadOnlyList<EmployeeInKindBenefitDto>>
{
    private readonly IEmployeeInKindBenefitRepository _benefits;
    private readonly IEmployeeRepository _employees;

    public ListInKindBenefitsForEmployeeQueryHandler(IEmployeeInKindBenefitRepository benefits, IEmployeeRepository employees)
    {
        _benefits = benefits;
        _employees = employees;
    }

    public async Task<IReadOnlyList<EmployeeInKindBenefitDto>> Handle(ListInKindBenefitsForEmployeeQuery request, CancellationToken cancellationToken)
    {
        var benefits = await _benefits.ListByEmployeeAsync(request.EmployeeId, cancellationToken);
        var employee = await _employees.GetByIdAsync(request.EmployeeId, cancellationToken);
        return benefits.Select(b => PayrollMappings.ToInKindBenefitDto(b, employee?.FullName)).ToList();
    }
}

public sealed record CreateInKindBenefitCommand(UpsertEmployeeInKindBenefitDto Dto) : IRequest<Result<Guid>>;

public sealed class CreateInKindBenefitCommandValidator : AbstractValidator<CreateInKindBenefitCommand>
{
    public CreateInKindBenefitCommandValidator()
    {
        RuleFor(x => x.Dto.EmployeeId).NotEmpty();
        RuleFor(x => x.Dto.Label).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Dto.MonthlyValue).GreaterThan(0);
    }
}

public sealed class CreateInKindBenefitCommandHandler : IRequestHandler<CreateInKindBenefitCommand, Result<Guid>>
{
    private readonly IEmployeeInKindBenefitRepository _benefits;
    private readonly IEmployeeRepository _employees;

    public CreateInKindBenefitCommandHandler(IEmployeeInKindBenefitRepository benefits, IEmployeeRepository employees)
    {
        _benefits = benefits;
        _employees = employees;
    }

    public async Task<Result<Guid>> Handle(CreateInKindBenefitCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto;
        if (await _employees.GetByIdAsync(dto.EmployeeId, cancellationToken) is null)
            return Result.Failure<Guid>(Error.NotFound("Employee", dto.EmployeeId));

        var result = EmployeeInKindBenefit.Create(
            dto.EmployeeId, dto.Type, dto.Label, dto.MonthlyValue, dto.StartDate, dto.EndDate, dto.Description);

        if (result.IsFailure)
            return Result.Failure<Guid>(result.Error);

        await _benefits.AddAsync(result.Value, cancellationToken);
        return Result.Success(result.Value.Id);
    }
}

public sealed record UpdateInKindBenefitCommand(Guid Id, UpsertEmployeeInKindBenefitDto Dto) : IRequest<Result>;

public sealed class UpdateInKindBenefitCommandHandler : IRequestHandler<UpdateInKindBenefitCommand, Result>
{
    private readonly IEmployeeInKindBenefitRepository _benefits;

    public UpdateInKindBenefitCommandHandler(IEmployeeInKindBenefitRepository benefits) => _benefits = benefits;

    public async Task<Result> Handle(UpdateInKindBenefitCommand request, CancellationToken cancellationToken)
    {
        var benefit = await _benefits.GetByIdAsync(request.Id, cancellationToken);
        if (benefit is null)
            return Result.Failure(Error.NotFound("EmployeeInKindBenefit", request.Id));

        var dto = request.Dto;
        var update = benefit.Update(dto.Type, dto.Label, dto.MonthlyValue, dto.EndDate, dto.Description);
        if (update.IsFailure)
            return update;

        await _benefits.UpdateAsync(benefit, cancellationToken);
        return Result.Success();
    }
}

public sealed record DeleteInKindBenefitCommand(Guid Id) : IRequest<Result>;

public sealed class DeleteInKindBenefitCommandHandler : IRequestHandler<DeleteInKindBenefitCommand, Result>
{
    private readonly IEmployeeInKindBenefitRepository _benefits;

    public DeleteInKindBenefitCommandHandler(IEmployeeInKindBenefitRepository benefits) => _benefits = benefits;

    public async Task<Result> Handle(DeleteInKindBenefitCommand request, CancellationToken cancellationToken)
    {
        var benefit = await _benefits.GetByIdAsync(request.Id, cancellationToken);
        if (benefit is null)
            return Result.Failure(Error.NotFound("EmployeeInKindBenefit", request.Id));

        await _benefits.DeleteAsync(benefit, cancellationToken);
        return Result.Success();
    }
}
