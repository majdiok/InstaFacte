using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Payroll.Terminations;

public sealed record ListTerminationSettlementsQuery(int? Year = null, int? Month = null)
    : IRequest<IReadOnlyList<TerminationSettlementDto>>;

public sealed class ListTerminationSettlementsQueryHandler
    : IRequestHandler<ListTerminationSettlementsQuery, IReadOnlyList<TerminationSettlementDto>>
{
    private readonly ITerminationSettlementRepository _settlements;
    private readonly IEmployeeRepository _employees;

    public ListTerminationSettlementsQueryHandler(
        ITerminationSettlementRepository settlements,
        IEmployeeRepository employees)
    {
        _settlements = settlements;
        _employees = employees;
    }

    public async Task<IReadOnlyList<TerminationSettlementDto>> Handle(
        ListTerminationSettlementsQuery request,
        CancellationToken cancellationToken)
    {
        var items = request.Year.HasValue && request.Month.HasValue
            ? await _settlements.ListForMonthAsync(request.Year.Value, request.Month.Value, cancellationToken)
            : await _settlements.ListAsync(cancellationToken);

        if (items.Count == 0)
            return Array.Empty<TerminationSettlementDto>();

        var names = await _employees.GetFullNamesByIdsAsync(
            items.Select(s => s.EmployeeId).Distinct().ToList(), cancellationToken);

        return items.Select(s => TerminationMappings.ToDto(s, names.GetValueOrDefault(s.EmployeeId))).ToList();
    }
}

public sealed record PreviewTerminationSettlementQuery(
    Guid EmployeeId,
    DateTime TerminationDate,
    string Reason) : IRequest<Result<TerminationSettlementPreviewDto>>;

public sealed class PreviewTerminationSettlementQueryHandler
    : IRequestHandler<PreviewTerminationSettlementQuery, Result<TerminationSettlementPreviewDto>>
{
    private readonly IEmployeeRepository _employees;
    private readonly AccountingSettings _settings;

    public PreviewTerminationSettlementQueryHandler(
        IEmployeeRepository employees,
        IOptions<AccountingSettings> settings)
    {
        _employees = employees;
        _settings = settings.Value;
    }

    public async Task<Result<TerminationSettlementPreviewDto>> Handle(
        PreviewTerminationSettlementQuery request,
        CancellationToken cancellationToken)
    {
        if (!_settings.PayrollTerminationIndemnityEnabled)
            return Result.Failure<TerminationSettlementPreviewDto>(
                Error.Validation("Feature", "Les indemnités de rupture ne sont pas activées."));

        if (!Enum.TryParse<TerminationReason>(request.Reason, out var reason))
            return Result.Failure<TerminationSettlementPreviewDto>(
                Error.Validation("Reason", "Motif de rupture invalide."));

        var employee = await _employees.GetByIdWithContractsAsync(request.EmployeeId, cancellationToken);
        if (employee is null)
            return Result.Failure<TerminationSettlementPreviewDto>(Error.NotFound("Employee", request.EmployeeId));

        var contract = employee.GetActiveContract(request.TerminationDate)
            ?? employee.Contracts.OrderByDescending(c => c.StartDate).FirstOrDefault();
        if (contract is null)
            return Result.Failure<TerminationSettlementPreviewDto>(
                Error.Validation("Contract", "Aucun contrat trouvé pour ce salarié."));

        var preview = TerminationMappings.BuildPreview(employee, contract, request.TerminationDate, reason);
        return Result.Success(preview);
    }
}

public sealed record UpsertTerminationSettlementCommand(UpsertTerminationSettlementDto Dto) : IRequest<Result<Guid>>;

public sealed class UpsertTerminationSettlementCommandValidator : AbstractValidator<UpsertTerminationSettlementCommand>
{
    public UpsertTerminationSettlementCommandValidator()
    {
        RuleFor(x => x.Dto.EmployeeId).NotEmpty();
        RuleFor(x => x.Dto.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Dto.Month).InclusiveBetween(1, 12);
    }
}

public sealed class UpsertTerminationSettlementCommandHandler
    : IRequestHandler<UpsertTerminationSettlementCommand, Result<Guid>>
{
    private readonly ITerminationSettlementRepository _settlements;
    private readonly IEmployeeRepository _employees;
    private readonly IPayrollRunRepository _runs;
    private readonly AccountingSettings _settings;

    public UpsertTerminationSettlementCommandHandler(
        ITerminationSettlementRepository settlements,
        IEmployeeRepository employees,
        IPayrollRunRepository runs,
        IOptions<AccountingSettings> settings)
    {
        _settlements = settlements;
        _employees = employees;
        _runs = runs;
        _settings = settings.Value;
    }

    public async Task<Result<Guid>> Handle(UpsertTerminationSettlementCommand request, CancellationToken cancellationToken)
    {
        if (!_settings.PayrollTerminationIndemnityEnabled)
            return Result.Failure<Guid>(Error.Validation("Feature", "Les indemnités de rupture ne sont pas activées."));

        var dto = request.Dto;
        if (await _runs.HasValidatedOrClosedRunForMonthAsync(dto.Year, dto.Month, cancellationToken))
            return Result.Failure<Guid>(Error.Validation("Month", "Le mois de paie est verrouillé."));

        if (!Enum.TryParse<TerminationReason>(dto.Reason, out var reason))
            return Result.Failure<Guid>(Error.Validation("Reason", "Motif de rupture invalide."));

        var employee = await _employees.GetByIdWithContractsAsync(dto.EmployeeId, cancellationToken);
        if (employee is null)
            return Result.Failure<Guid>(Error.NotFound("Employee", dto.EmployeeId));

        var contract = employee.GetActiveContract(dto.TerminationDate)
            ?? employee.Contracts.OrderByDescending(c => c.StartDate).FirstOrDefault();
        if (contract is null)
            return Result.Failure<Guid>(Error.Validation("Contract", "Aucun contrat trouvé."));

        var preview = TerminationMappings.BuildPreview(employee, contract, dto.TerminationDate, reason);
        var legalAmount = dto.LegalIndemnityAmount ?? preview.LegalIndemnityAmount;

        var existing = await _settlements.GetByEmployeeAndMonthAsync(dto.EmployeeId, dto.Year, dto.Month, cancellationToken);
        if (existing is not null)
        {
            if (!existing.Status.CanBeEdited())
                return Result.Failure<Guid>(Error.Validation("Status", "Ce solde ne peut plus être modifié."));

            var refresh = existing.Refresh(
                preview.SeniorityMonths,
                preview.GrossMonthlyReference,
                legalAmount,
                dto.NoticeIndemnityAmount,
                dto.UnusedLeaveAmount,
                dto.OtherIndemnityAmount,
                dto.Notes);
            if (refresh.IsFailure)
                return Result.Failure<Guid>(refresh.Error);

            if (dto.Approve)
            {
                var approve = existing.Approve();
                if (approve.IsFailure)
                    return Result.Failure<Guid>(approve.Error);
            }

            await _settlements.UpdateAsync(existing, cancellationToken);
            return Result.Success(existing.Id);
        }

        var createResult = TerminationSettlement.Create(
            dto.EmployeeId,
            dto.Year,
            dto.Month,
            dto.TerminationDate,
            reason,
            preview.SeniorityMonths,
            preview.GrossMonthlyReference,
            legalAmount,
            dto.NoticeIndemnityAmount,
            dto.UnusedLeaveAmount,
            dto.OtherIndemnityAmount,
            dto.Notes);

        if (createResult.IsFailure)
            return Result.Failure<Guid>(createResult.Error);

        var settlement = createResult.Value;
        if (dto.Approve)
        {
            var approve = settlement.Approve();
            if (approve.IsFailure)
                return Result.Failure<Guid>(approve.Error);
        }

        await _settlements.AddAsync(settlement, cancellationToken);
        return Result.Success(settlement.Id);
    }
}

public sealed record ApproveTerminationSettlementCommand(Guid Id) : IRequest<Result>;

public sealed class ApproveTerminationSettlementCommandHandler : IRequestHandler<ApproveTerminationSettlementCommand, Result>
{
    private readonly ITerminationSettlementRepository _settlements;
    private readonly IPayrollRunRepository _runs;

    public ApproveTerminationSettlementCommandHandler(
        ITerminationSettlementRepository settlements,
        IPayrollRunRepository runs)
    {
        _settlements = settlements;
        _runs = runs;
    }

    public async Task<Result> Handle(ApproveTerminationSettlementCommand request, CancellationToken cancellationToken)
    {
        var settlement = await _settlements.GetByIdAsync(request.Id, cancellationToken);
        if (settlement is null)
            return Result.Failure(Error.NotFound("TerminationSettlement", request.Id));

        if (await _runs.HasValidatedOrClosedRunForMonthAsync(settlement.Year, settlement.Month, cancellationToken))
            return Result.Failure(Error.Validation("Month", "Le mois de paie est verrouillé."));

        var approve = settlement.Approve();
        if (approve.IsFailure)
            return approve;

        await _settlements.UpdateAsync(settlement, cancellationToken);
        return Result.Success();
    }
}

public sealed record DeleteTerminationSettlementCommand(Guid Id) : IRequest<Result>;

public sealed class DeleteTerminationSettlementCommandHandler : IRequestHandler<DeleteTerminationSettlementCommand, Result>
{
    private readonly ITerminationSettlementRepository _settlements;
    private readonly IPayrollRunRepository _runs;

    public DeleteTerminationSettlementCommandHandler(
        ITerminationSettlementRepository settlements,
        IPayrollRunRepository runs)
    {
        _settlements = settlements;
        _runs = runs;
    }

    public async Task<Result> Handle(DeleteTerminationSettlementCommand request, CancellationToken cancellationToken)
    {
        var settlement = await _settlements.GetByIdAsync(request.Id, cancellationToken);
        if (settlement is null)
            return Result.Failure(Error.NotFound("TerminationSettlement", request.Id));

        if (await _runs.HasValidatedOrClosedRunForMonthAsync(settlement.Year, settlement.Month, cancellationToken))
            return Result.Failure(Error.Validation("Month", "Le mois de paie est verrouillé."));

        if (!settlement.Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Ce solde ne peut plus être supprimé."));

        await _settlements.DeleteAsync(settlement, cancellationToken);
        return Result.Success();
    }
}

internal static class TerminationMappings
{
    public static TerminationSettlementPreviewDto BuildPreview(
        Employee employee,
        EmploymentContract contract,
        DateTime terminationDate,
        TerminationReason reason)
    {
        var seniorityStart = employee.HireDate < contract.StartDate ? contract.StartDate : employee.HireDate;
        var calc = TerminationIndemnityCalculator.Compute(new TerminationIndemnityCalculator.Input(
            seniorityStart, terminationDate, contract.BaseSalary, reason));

        return new TerminationSettlementPreviewDto
        {
            EmployeeId = employee.Id,
            EmployeeName = employee.FullName,
            TerminationDate = terminationDate.Date,
            Reason = reason.ToString(),
            ReasonDisplay = reason.ToDisplayString(),
            SeniorityMonths = calc.SeniorityMonths,
            IndemnityDays = calc.IndemnityDays,
            DailyRate = calc.DailyRate,
            GrossMonthlyReference = contract.BaseSalary,
            LegalIndemnityAmount = calc.AppliedAmount,
            NoticeIndemnityAmount = 0m,
            UnusedLeaveAmount = 0m,
            OtherIndemnityAmount = 0m,
            TotalIndemnityAmount = calc.AppliedAmount
        };
    }

    public static TerminationSettlementDto ToDto(TerminationSettlement s, string? employeeName) => new()
    {
        Id = s.Id,
        EmployeeId = s.EmployeeId,
        EmployeeName = employeeName,
        Year = s.Year,
        Month = s.Month,
        TerminationDate = s.TerminationDate,
        Reason = s.Reason.ToString(),
        ReasonDisplay = s.Reason.ToDisplayString(),
        Status = s.Status.ToString(),
        StatusDisplay = s.Status.ToDisplayString(),
        SeniorityMonths = s.SeniorityMonths,
        GrossMonthlyReference = s.GrossMonthlyReference,
        LegalIndemnityAmount = s.LegalIndemnityAmount,
        NoticeIndemnityAmount = s.NoticeIndemnityAmount,
        UnusedLeaveAmount = s.UnusedLeaveAmount,
        OtherIndemnityAmount = s.OtherIndemnityAmount,
        TotalIndemnityAmount = s.TotalIndemnityAmount,
        Notes = s.Notes
    };
}
