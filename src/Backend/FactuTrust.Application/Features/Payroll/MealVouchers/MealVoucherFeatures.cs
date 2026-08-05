using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Payroll;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FluentValidation;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.MealVouchers;

public sealed record ListMealVouchersForMonthQuery(int Year, int Month) : IRequest<IReadOnlyList<PayrollMealVoucherLineDto>>;

public sealed class ListMealVouchersForMonthQueryHandler : IRequestHandler<ListMealVouchersForMonthQuery, IReadOnlyList<PayrollMealVoucherLineDto>>
{
    private readonly IPayrollMealVoucherLineRepository _mealVouchers;
    private readonly IEmployeeRepository _employees;

    public ListMealVouchersForMonthQueryHandler(IPayrollMealVoucherLineRepository mealVouchers, IEmployeeRepository employees)
    {
        _mealVouchers = mealVouchers;
        _employees = employees;
    }

    public async Task<IReadOnlyList<PayrollMealVoucherLineDto>> Handle(ListMealVouchersForMonthQuery request, CancellationToken cancellationToken)
    {
        var lines = await _mealVouchers.ListForMonthAsync(request.Year, request.Month, cancellationToken);
        if (lines.Count == 0)
            return Array.Empty<PayrollMealVoucherLineDto>();

        var names = await _employees.GetFullNamesByIdsAsync(lines.Select(l => l.EmployeeId).Distinct().ToList(), cancellationToken);
        return lines.Select(l => PayrollMappings.ToMealVoucherDto(l, names.GetValueOrDefault(l.EmployeeId))).ToList();
    }
}

public sealed record CreateMealVoucherLineCommand(UpsertPayrollMealVoucherLineDto Dto) : IRequest<Result<Guid>>;

public sealed class CreateMealVoucherLineCommandValidator : AbstractValidator<CreateMealVoucherLineCommand>
{
    public CreateMealVoucherLineCommandValidator()
    {
        RuleFor(x => x.Dto.EmployeeId).NotEmpty();
        RuleFor(x => x.Dto.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Dto.Month).InclusiveBetween(1, 12);
        RuleFor(x => x.Dto.Days).GreaterThan(0);
        RuleFor(x => x.Dto.FaceValue).GreaterThan(0);
    }
}

public sealed class CreateMealVoucherLineCommandHandler : IRequestHandler<CreateMealVoucherLineCommand, Result<Guid>>
{
    private readonly IPayrollMealVoucherLineRepository _mealVouchers;
    private readonly IEmployeeRepository _employees;
    private readonly IPayrollRunRepository _runs;

    public CreateMealVoucherLineCommandHandler(
        IPayrollMealVoucherLineRepository mealVouchers,
        IEmployeeRepository employees,
        IPayrollRunRepository runs)
    {
        _mealVouchers = mealVouchers;
        _employees = employees;
        _runs = runs;
    }

    public async Task<Result<Guid>> Handle(CreateMealVoucherLineCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto;
        if (await _runs.HasValidatedOrClosedRunForMonthAsync(dto.Year, dto.Month, cancellationToken))
            return Result.Failure<Guid>(Error.Validation("Month", "Le mois de paie est verrouillé (cycle validé ou clôturé)."));

        if (await _employees.GetByIdWithContractsAsync(dto.EmployeeId, cancellationToken) is null)
            return Result.Failure<Guid>(Error.NotFound("Employee", dto.EmployeeId));

        var result = PayrollMealVoucherLine.Create(
            dto.EmployeeId, dto.Year, dto.Month, dto.Days, dto.FaceValue, dto.EmployerContributionRate);

        if (result.IsFailure)
            return Result.Failure<Guid>(result.Error);

        await _mealVouchers.AddAsync(result.Value, cancellationToken);
        return Result.Success(result.Value.Id);
    }
}

public sealed record UpdateMealVoucherLineCommand(Guid Id, UpsertPayrollMealVoucherLineDto Dto) : IRequest<Result>;

public sealed class UpdateMealVoucherLineCommandHandler : IRequestHandler<UpdateMealVoucherLineCommand, Result>
{
    private readonly IPayrollMealVoucherLineRepository _mealVouchers;
    private readonly IPayrollRunRepository _runs;

    public UpdateMealVoucherLineCommandHandler(IPayrollMealVoucherLineRepository mealVouchers, IPayrollRunRepository runs)
    {
        _mealVouchers = mealVouchers;
        _runs = runs;
    }

    public async Task<Result> Handle(UpdateMealVoucherLineCommand request, CancellationToken cancellationToken)
    {
        var line = await _mealVouchers.GetByIdAsync(request.Id, cancellationToken);
        if (line is null)
            return Result.Failure(Error.NotFound("PayrollMealVoucherLine", request.Id));

        if (await _runs.HasValidatedOrClosedRunForMonthAsync(line.Year, line.Month, cancellationToken))
            return Result.Failure(Error.Validation("Month", "Le mois de paie est verrouillé (cycle validé ou clôturé)."));

        var update = line.Update(request.Dto.Days, request.Dto.FaceValue, request.Dto.EmployerContributionRate);
        if (update.IsFailure)
            return update;

        await _mealVouchers.UpdateAsync(line, cancellationToken);
        return Result.Success();
    }
}

public sealed record DeleteMealVoucherLineCommand(Guid Id) : IRequest<Result>;

public sealed class DeleteMealVoucherLineCommandHandler : IRequestHandler<DeleteMealVoucherLineCommand, Result>
{
    private readonly IPayrollMealVoucherLineRepository _mealVouchers;
    private readonly IPayrollRunRepository _runs;

    public DeleteMealVoucherLineCommandHandler(IPayrollMealVoucherLineRepository mealVouchers, IPayrollRunRepository runs)
    {
        _mealVouchers = mealVouchers;
        _runs = runs;
    }

    public async Task<Result> Handle(DeleteMealVoucherLineCommand request, CancellationToken cancellationToken)
    {
        var line = await _mealVouchers.GetByIdAsync(request.Id, cancellationToken);
        if (line is null)
            return Result.Failure(Error.NotFound("PayrollMealVoucherLine", request.Id));

        if (await _runs.HasValidatedOrClosedRunForMonthAsync(line.Year, line.Month, cancellationToken))
            return Result.Failure(Error.Validation("Month", "Le mois de paie est verrouillé (cycle validé ou clôturé)."));

        await _mealVouchers.DeleteAsync(line, cancellationToken);
        return Result.Success();
    }
}
