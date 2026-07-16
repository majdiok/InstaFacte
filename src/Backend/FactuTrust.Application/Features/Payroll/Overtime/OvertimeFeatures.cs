using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Payroll;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using FluentValidation;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Overtime;

public sealed record ListOvertimeForMonthQuery(int Year, int Month) : IRequest<IReadOnlyList<PayrollOvertimeLineDto>>;

public sealed class ListOvertimeForMonthQueryHandler : IRequestHandler<ListOvertimeForMonthQuery, IReadOnlyList<PayrollOvertimeLineDto>>
{
    private readonly IPayrollOvertimeRepository _overtime;
    private readonly IEmployeeRepository _employees;

    public ListOvertimeForMonthQueryHandler(IPayrollOvertimeRepository overtime, IEmployeeRepository employees)
    {
        _overtime = overtime;
        _employees = employees;
    }

    public async Task<IReadOnlyList<PayrollOvertimeLineDto>> Handle(ListOvertimeForMonthQuery request, CancellationToken cancellationToken)
    {
        var lines = await _overtime.ListForMonthAsync(request.Year, request.Month, cancellationToken);
        if (lines.Count == 0)
            return Array.Empty<PayrollOvertimeLineDto>();

        var employeeIds = lines.Select(l => l.EmployeeId).Distinct().ToList();
        var names = await _employees.GetFullNamesByIdsAsync(employeeIds, cancellationToken);

        return lines
            .Select(l => PayrollMappings.ToOvertimeDto(l, names.GetValueOrDefault(l.EmployeeId)))
            .ToList();
    }
}

public sealed record ListEmployeeOvertimeQuery(Guid EmployeeId, int Year, int Month) : IRequest<IReadOnlyList<PayrollOvertimeLineDto>>;

public sealed class ListEmployeeOvertimeQueryHandler : IRequestHandler<ListEmployeeOvertimeQuery, IReadOnlyList<PayrollOvertimeLineDto>>
{
    private readonly IPayrollOvertimeRepository _overtime;
    private readonly IEmployeeRepository _employees;

    public ListEmployeeOvertimeQueryHandler(IPayrollOvertimeRepository overtime, IEmployeeRepository employees)
    {
        _overtime = overtime;
        _employees = employees;
    }

    public async Task<IReadOnlyList<PayrollOvertimeLineDto>> Handle(ListEmployeeOvertimeQuery request, CancellationToken cancellationToken)
    {
        var employee = await _employees.GetByIdAsync(request.EmployeeId, cancellationToken);
        if (employee is null)
            return Array.Empty<PayrollOvertimeLineDto>();

        var lines = await _overtime.ListByEmployeeAndMonthAsync(request.EmployeeId, request.Year, request.Month, cancellationToken);
        return lines.Select(l => PayrollMappings.ToOvertimeDto(l, employee.FullName)).ToList();
    }
}

public sealed record CreateOvertimeLineCommand(UpsertOvertimeLineDto Dto) : IRequest<Result<Guid>>;

public sealed class CreateOvertimeLineCommandValidator : AbstractValidator<CreateOvertimeLineCommand>
{
    public CreateOvertimeLineCommandValidator()
    {
        RuleFor(x => x.Dto.EmployeeId).NotEmpty();
        RuleFor(x => x.Dto.Hours).GreaterThan(0);
        RuleFor(x => x.Dto.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Dto.Month).InclusiveBetween(1, 12);
    }
}

public sealed class CreateOvertimeLineCommandHandler : IRequestHandler<CreateOvertimeLineCommand, Result<Guid>>
{
    private readonly IPayrollOvertimeRepository _overtime;
    private readonly IEmployeeRepository _employees;
    private readonly IPayrollRunRepository _runs;
    private readonly IPayrollParametersRepository _parameters;

    public CreateOvertimeLineCommandHandler(
        IPayrollOvertimeRepository overtime,
        IEmployeeRepository employees,
        IPayrollRunRepository runs,
        IPayrollParametersRepository parameters)
    {
        _overtime = overtime;
        _employees = employees;
        _runs = runs;
        _parameters = parameters;
    }

    public async Task<Result<Guid>> Handle(CreateOvertimeLineCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto;

        if (await _runs.HasValidatedOrClosedRunForMonthAsync(dto.Year, dto.Month, cancellationToken))
            return Result.Failure<Guid>(Error.Validation("Month", "Le mois de paie est verrouillé (cycle validé ou clôturé)."));

        var employee = await _employees.GetByIdWithContractsAsync(dto.EmployeeId, cancellationToken);
        if (employee is null)
            return Result.Failure<Guid>(Error.NotFound("Employee", dto.EmployeeId));

        var referenceDate = new DateTime(dto.Year, dto.Month, 1).AddMonths(1).AddDays(-1);
        var contract = employee.GetActiveContract(referenceDate);
        if (contract is null)
            return Result.Failure<Guid>(Error.Validation("Contract", "Aucun contrat actif pour ce salarié sur la période."));

        var payrollParams = await _parameters.GetOrCreateForYearAsync(dto.Year, cancellationToken);

        var lineResult = PayrollOvertimeLine.Create(
            dto.EmployeeId,
            dto.Year,
            dto.Month,
            dto.Hours,
            dto.RatePercent,
            contract.BaseSalary,
            dto.OverrideAmount,
            payrollParams.EnableExtendedOvertimeRates,
            contract.WeeklyRegime);

        if (lineResult.IsFailure)
            return Result.Failure<Guid>(lineResult.Error);

        await _overtime.AddAsync(lineResult.Value, cancellationToken);
        return Result.Success(lineResult.Value.Id);
    }
}

public sealed record UpdateOvertimeLineCommand(Guid Id, UpsertOvertimeLineDto Dto) : IRequest<Result>;

public sealed class UpdateOvertimeLineCommandValidator : AbstractValidator<UpdateOvertimeLineCommand>
{
    public UpdateOvertimeLineCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Dto.Hours).GreaterThan(0);
    }
}

public sealed class UpdateOvertimeLineCommandHandler : IRequestHandler<UpdateOvertimeLineCommand, Result>
{
    private readonly IPayrollOvertimeRepository _overtime;
    private readonly IEmployeeRepository _employees;
    private readonly IPayrollRunRepository _runs;
    private readonly IPayrollParametersRepository _parameters;

    public UpdateOvertimeLineCommandHandler(
        IPayrollOvertimeRepository overtime,
        IEmployeeRepository employees,
        IPayrollRunRepository runs,
        IPayrollParametersRepository parameters)
    {
        _overtime = overtime;
        _employees = employees;
        _runs = runs;
        _parameters = parameters;
    }

    public async Task<Result> Handle(UpdateOvertimeLineCommand request, CancellationToken cancellationToken)
    {
        var line = await _overtime.GetByIdAsync(request.Id, cancellationToken);
        if (line is null)
            return Result.Failure(Error.NotFound("PayrollOvertimeLine", request.Id));

        if (await _runs.HasValidatedOrClosedRunForMonthAsync(line.Year, line.Month, cancellationToken))
            return Result.Failure(Error.Validation("Month", "Le mois de paie est verrouillé (cycle validé ou clôturé)."));

        var employee = await _employees.GetByIdWithContractsAsync(line.EmployeeId, cancellationToken);
        if (employee is null)
            return Result.Failure(Error.NotFound("Employee", line.EmployeeId));

        var referenceDate = new DateTime(line.Year, line.Month, 1).AddMonths(1).AddDays(-1);
        var contract = employee.GetActiveContract(referenceDate);
        if (contract is null)
            return Result.Failure(Error.Validation("Contract", "Aucun contrat actif pour ce salarié sur la période."));

        var payrollParams = await _parameters.GetOrCreateForYearAsync(line.Year, cancellationToken);

        var updateResult = line.Update(
            request.Dto.Hours,
            request.Dto.RatePercent,
            contract.BaseSalary,
            request.Dto.OverrideAmount,
            payrollParams.EnableExtendedOvertimeRates,
            contract.WeeklyRegime);

        if (updateResult.IsFailure)
            return updateResult;

        await _overtime.UpdateAsync(line, cancellationToken);
        return Result.Success();
    }
}

public sealed record DeleteOvertimeLineCommand(Guid Id) : IRequest<Result>;

public sealed class DeleteOvertimeLineCommandHandler : IRequestHandler<DeleteOvertimeLineCommand, Result>
{
    private readonly IPayrollOvertimeRepository _overtime;
    private readonly IPayrollRunRepository _runs;

    public DeleteOvertimeLineCommandHandler(IPayrollOvertimeRepository overtime, IPayrollRunRepository runs)
    {
        _overtime = overtime;
        _runs = runs;
    }

    public async Task<Result> Handle(DeleteOvertimeLineCommand request, CancellationToken cancellationToken)
    {
        var line = await _overtime.GetByIdAsync(request.Id, cancellationToken);
        if (line is null)
            return Result.Failure(Error.NotFound("PayrollOvertimeLine", request.Id));

        if (await _runs.HasValidatedOrClosedRunForMonthAsync(line.Year, line.Month, cancellationToken))
            return Result.Failure(Error.Validation("Month", "Le mois de paie est verrouillé (cycle validé ou clôturé)."));

        await _overtime.DeleteAsync(line, cancellationToken);
        return Result.Success();
    }
}

public sealed record PreviewOvertimeAmountQuery(decimal BaseSalary, decimal Hours, decimal RatePercent, decimal? OverrideAmount, int? FiscalYear = null, Guid? EmployeeId = null)
    : IRequest<OvertimePreviewDto>;

public sealed class PreviewOvertimeAmountQueryHandler : IRequestHandler<PreviewOvertimeAmountQuery, OvertimePreviewDto>
{
    private readonly IPayrollParametersRepository _parameters;
    private readonly IEmployeeRepository _employees;

    public PreviewOvertimeAmountQueryHandler(IPayrollParametersRepository parameters, IEmployeeRepository employees)
    {
        _parameters = parameters;
        _employees = employees;
    }

    public async Task<OvertimePreviewDto> Handle(PreviewOvertimeAmountQuery request, CancellationToken cancellationToken)
    {
        var year = request.FiscalYear ?? DateTime.UtcNow.Year;
        var payrollParams = await _parameters.GetOrCreateForYearAsync(year, cancellationToken);
        var enableExtended = payrollParams.EnableExtendedOvertimeRates;

        // Le régime hebdomadaire est résolu depuis le contrat actif du salarié quand il est fourni ;
        // à défaut la convention 48 h s'applique (comportement historique).
        WeeklyWorkRegime? regime = null;
        if (request.EmployeeId.HasValue)
        {
            var employee = await _employees.GetByIdWithContractsAsync(request.EmployeeId.Value, cancellationToken);
            regime = employee?.GetActiveContract(DateTime.UtcNow)?.WeeklyRegime;
        }

        var computed = OvertimeAmountCalculator.ComputeAmount(request.BaseSalary, request.Hours, request.RatePercent, enableExtended, regime);
        var effective = OvertimeAmountCalculator.ResolveEffectiveAmount(computed, request.OverrideAmount);
        var hourlyRate = OvertimeAmountCalculator.ComputeHourlyRate(request.BaseSalary, regime);

        return new OvertimePreviewDto
        {
            HourlyRate = hourlyRate,
            ComputedAmount = computed,
            EffectiveAmount = effective,
            IsOverridden = request.OverrideAmount.HasValue && request.OverrideAmount.Value > 0
        };
    }
}
