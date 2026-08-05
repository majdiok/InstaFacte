using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Payroll;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FluentValidation;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.VariableAllowances;

public sealed record ListVariableAllowancesForMonthQuery(int Year, int Month) : IRequest<IReadOnlyList<PayrollVariableAllowanceLineDto>>;

public sealed class ListVariableAllowancesForMonthQueryHandler : IRequestHandler<ListVariableAllowancesForMonthQuery, IReadOnlyList<PayrollVariableAllowanceLineDto>>
{
    private readonly IPayrollVariableAllowanceRepository _variableAllowances;
    private readonly IEmployeeRepository _employees;

    public ListVariableAllowancesForMonthQueryHandler(
        IPayrollVariableAllowanceRepository variableAllowances,
        IEmployeeRepository employees)
    {
        _variableAllowances = variableAllowances;
        _employees = employees;
    }

    public async Task<IReadOnlyList<PayrollVariableAllowanceLineDto>> Handle(ListVariableAllowancesForMonthQuery request, CancellationToken cancellationToken)
    {
        var lines = await _variableAllowances.ListForMonthAsync(request.Year, request.Month, cancellationToken);
        if (lines.Count == 0)
            return Array.Empty<PayrollVariableAllowanceLineDto>();

        var employeeIds = lines.Select(l => l.EmployeeId).Distinct().ToList();
        var names = await _employees.GetFullNamesByIdsAsync(employeeIds, cancellationToken);

        return lines
            .Select(l => PayrollMappings.ToVariableAllowanceDto(l, names.GetValueOrDefault(l.EmployeeId)))
            .ToList();
    }
}

public sealed record CreateVariableAllowanceLineCommand(UpsertVariableAllowanceLineDto Dto) : IRequest<Result<Guid>>;

public sealed class CreateVariableAllowanceLineCommandValidator : AbstractValidator<CreateVariableAllowanceLineCommand>
{
    public CreateVariableAllowanceLineCommandValidator()
    {
        RuleFor(x => x.Dto.EmployeeId).NotEmpty();
        RuleFor(x => x.Dto.Label).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Dto.Amount).GreaterThan(0);
        RuleFor(x => x.Dto.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Dto.Month).InclusiveBetween(1, 12);
    }
}

public sealed class CreateVariableAllowanceLineCommandHandler : IRequestHandler<CreateVariableAllowanceLineCommand, Result<Guid>>
{
    private readonly IPayrollVariableAllowanceRepository _variableAllowances;
    private readonly IEmployeeRepository _employees;
    private readonly IPayrollRunRepository _runs;

    public CreateVariableAllowanceLineCommandHandler(
        IPayrollVariableAllowanceRepository variableAllowances,
        IEmployeeRepository employees,
        IPayrollRunRepository runs)
    {
        _variableAllowances = variableAllowances;
        _employees = employees;
        _runs = runs;
    }

    public async Task<Result<Guid>> Handle(CreateVariableAllowanceLineCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto;

        if (await _runs.HasValidatedOrClosedRunForMonthAsync(dto.Year, dto.Month, cancellationToken))
            return Result.Failure<Guid>(Error.Validation("Month", "Le mois de paie est verrouillé (cycle validé ou clôturé)."));

        var employee = await _employees.GetByIdWithContractsAsync(dto.EmployeeId, cancellationToken);
        if (employee is null)
            return Result.Failure<Guid>(Error.NotFound("Employee", dto.EmployeeId));

        var referenceDate = new DateTime(dto.Year, dto.Month, 1).AddMonths(1).AddDays(-1);
        if (employee.GetActiveContract(referenceDate) is null)
            return Result.Failure<Guid>(Error.Validation("Contract", "Aucun contrat actif pour ce salarié sur la période."));

        var lineResult = PayrollVariableAllowanceLine.Create(
            dto.EmployeeId,
            dto.Year,
            dto.Month,
            dto.Label,
            dto.Amount,
            dto.Taxable,
            dto.SubjectToCnss);

        if (lineResult.IsFailure)
            return Result.Failure<Guid>(lineResult.Error);

        await _variableAllowances.AddAsync(lineResult.Value, cancellationToken);
        return Result.Success(lineResult.Value.Id);
    }
}

public sealed record UpdateVariableAllowanceLineCommand(Guid Id, UpsertVariableAllowanceLineDto Dto) : IRequest<Result>;

public sealed class UpdateVariableAllowanceLineCommandValidator : AbstractValidator<UpdateVariableAllowanceLineCommand>
{
    public UpdateVariableAllowanceLineCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Dto.Label).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Dto.Amount).GreaterThan(0);
    }
}

public sealed class UpdateVariableAllowanceLineCommandHandler : IRequestHandler<UpdateVariableAllowanceLineCommand, Result>
{
    private readonly IPayrollVariableAllowanceRepository _variableAllowances;
    private readonly IEmployeeRepository _employees;
    private readonly IPayrollRunRepository _runs;

    public UpdateVariableAllowanceLineCommandHandler(
        IPayrollVariableAllowanceRepository variableAllowances,
        IEmployeeRepository employees,
        IPayrollRunRepository runs)
    {
        _variableAllowances = variableAllowances;
        _employees = employees;
        _runs = runs;
    }

    public async Task<Result> Handle(UpdateVariableAllowanceLineCommand request, CancellationToken cancellationToken)
    {
        var line = await _variableAllowances.GetByIdAsync(request.Id, cancellationToken);
        if (line is null)
            return Result.Failure(Error.NotFound("PayrollVariableAllowanceLine", request.Id));

        if (await _runs.HasValidatedOrClosedRunForMonthAsync(line.Year, line.Month, cancellationToken))
            return Result.Failure(Error.Validation("Month", "Le mois de paie est verrouillé (cycle validé ou clôturé)."));

        var employee = await _employees.GetByIdWithContractsAsync(line.EmployeeId, cancellationToken);
        if (employee is null)
            return Result.Failure(Error.NotFound("Employee", line.EmployeeId));

        var referenceDate = new DateTime(line.Year, line.Month, 1).AddMonths(1).AddDays(-1);
        if (employee.GetActiveContract(referenceDate) is null)
            return Result.Failure(Error.Validation("Contract", "Aucun contrat actif pour ce salarié sur la période."));

        var updateResult = line.Update(
            request.Dto.Label,
            request.Dto.Amount,
            request.Dto.Taxable,
            request.Dto.SubjectToCnss);

        if (updateResult.IsFailure)
            return updateResult;

        await _variableAllowances.UpdateAsync(line, cancellationToken);
        return Result.Success();
    }
}

public sealed record DeleteVariableAllowanceLineCommand(Guid Id) : IRequest<Result>;

public sealed class DeleteVariableAllowanceLineCommandHandler : IRequestHandler<DeleteVariableAllowanceLineCommand, Result>
{
    private readonly IPayrollVariableAllowanceRepository _variableAllowances;
    private readonly IPayrollRunRepository _runs;

    public DeleteVariableAllowanceLineCommandHandler(
        IPayrollVariableAllowanceRepository variableAllowances,
        IPayrollRunRepository runs)
    {
        _variableAllowances = variableAllowances;
        _runs = runs;
    }

    public async Task<Result> Handle(DeleteVariableAllowanceLineCommand request, CancellationToken cancellationToken)
    {
        var line = await _variableAllowances.GetByIdAsync(request.Id, cancellationToken);
        if (line is null)
            return Result.Failure(Error.NotFound("PayrollVariableAllowanceLine", request.Id));

        if (await _runs.HasValidatedOrClosedRunForMonthAsync(line.Year, line.Month, cancellationToken))
            return Result.Failure(Error.Validation("Month", "Le mois de paie est verrouillé (cycle validé ou clôturé)."));

        await _variableAllowances.DeleteAsync(line, cancellationToken);
        return Result.Success();
    }
}
