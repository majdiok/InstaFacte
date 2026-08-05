using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Payroll;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FluentValidation;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Garnishments;

public sealed record ListEmployeeGarnishmentsQuery(Guid EmployeeId) : IRequest<IReadOnlyList<EmployeeGarnishmentDto>>;

public sealed class ListEmployeeGarnishmentsQueryHandler : IRequestHandler<ListEmployeeGarnishmentsQuery, IReadOnlyList<EmployeeGarnishmentDto>>
{
    private readonly IEmployeeGarnishmentRepository _garnishments;
    private readonly IEmployeeRepository _employees;

    public ListEmployeeGarnishmentsQueryHandler(IEmployeeGarnishmentRepository garnishments, IEmployeeRepository employees)
    {
        _garnishments = garnishments;
        _employees = employees;
    }

    public async Task<IReadOnlyList<EmployeeGarnishmentDto>> Handle(ListEmployeeGarnishmentsQuery request, CancellationToken cancellationToken)
    {
        var garnishments = await _garnishments.ListByEmployeeAsync(request.EmployeeId, cancellationToken);
        var employee = await _employees.GetByIdAsync(request.EmployeeId, cancellationToken);
        return garnishments.Select(g => PayrollMappings.ToEmployeeGarnishmentDto(g, employee?.FullName)).ToList();
    }
}

public sealed record CreateEmployeeGarnishmentCommand(CreateEmployeeGarnishmentDto Dto) : IRequest<Result<Guid>>;

public sealed class CreateEmployeeGarnishmentCommandValidator : AbstractValidator<CreateEmployeeGarnishmentCommand>
{
    public CreateEmployeeGarnishmentCommandValidator()
    {
        RuleFor(x => x.Dto.EmployeeId).NotEmpty();
        RuleFor(x => x.Dto.Reference).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Dto.BeneficiaryName).NotEmpty().MaximumLength(200);
    }
}

public sealed class CreateEmployeeGarnishmentCommandHandler : IRequestHandler<CreateEmployeeGarnishmentCommand, Result<Guid>>
{
    private readonly IEmployeeGarnishmentRepository _garnishments;
    private readonly IEmployeeRepository _employees;

    public CreateEmployeeGarnishmentCommandHandler(IEmployeeGarnishmentRepository garnishments, IEmployeeRepository employees)
    {
        _garnishments = garnishments;
        _employees = employees;
    }

    public async Task<Result<Guid>> Handle(CreateEmployeeGarnishmentCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto;
        if (await _employees.GetByIdAsync(dto.EmployeeId, cancellationToken) is null)
            return Result.Failure<Guid>(Error.NotFound("Employee", dto.EmployeeId));

        var result = EmployeeGarnishment.Create(
            dto.EmployeeId, dto.Type, dto.Reference, dto.IssuedAt, dto.BeneficiaryName, dto.BeneficiaryRib,
            dto.Priority, dto.Kind, dto.FixedAmount, dto.PercentOfNet, dto.TotalAmountDue,
            dto.StartDate, dto.EndDate);

        if (result.IsFailure)
            return Result.Failure<Guid>(result.Error);

        await _garnishments.AddAsync(result.Value, cancellationToken);
        return Result.Success(result.Value.Id);
    }
}

public sealed record CancelEmployeeGarnishmentCommand(Guid Id) : IRequest<Result>;

public sealed class CancelEmployeeGarnishmentCommandHandler : IRequestHandler<CancelEmployeeGarnishmentCommand, Result>
{
    private readonly IEmployeeGarnishmentRepository _garnishments;

    public CancelEmployeeGarnishmentCommandHandler(IEmployeeGarnishmentRepository garnishments) => _garnishments = garnishments;

    public async Task<Result> Handle(CancelEmployeeGarnishmentCommand request, CancellationToken cancellationToken)
    {
        var garnishment = await _garnishments.GetByIdAsync(request.Id, cancellationToken);
        if (garnishment is null)
            return Result.Failure(Error.NotFound("EmployeeGarnishment", request.Id));

        garnishment.Cancel();
        await _garnishments.UpdateAsync(garnishment, cancellationToken);
        return Result.Success();
    }
}
