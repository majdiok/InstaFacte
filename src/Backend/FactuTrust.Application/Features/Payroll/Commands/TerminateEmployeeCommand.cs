using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Payroll.Terminations;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Payroll.Commands;

public sealed record TerminateEmployeeDto(
    DateTime TerminationDate,
    string? Reason,
    bool CloseActiveContract = true,
    bool DeactivateNow = true,
    bool CreateSettlement = false,
    string TerminationReason = "Dismissal");

public sealed record TerminateEmployeeCommand(Guid EmployeeId, TerminateEmployeeDto Dto)
    : IRequest<Result<TerminateEmployeeResultDto>>;

public sealed class TerminateEmployeeResultDto
{
    public bool IsActive { get; init; }
    public DateTime TerminationDate { get; init; }
    public string? Warning { get; init; }
    public Guid? SettlementId { get; init; }
}

public sealed class TerminateEmployeeCommandHandler
    : IRequestHandler<TerminateEmployeeCommand, Result<TerminateEmployeeResultDto>>
{
    private readonly IEmployeeRepository _employees;
    private readonly IEmployeeDependentParentRepository _dependentParents;
    private readonly ITerminationSettlementRepository _settlements;
    private readonly AccountingSettings _settings;

    public TerminateEmployeeCommandHandler(
        IEmployeeRepository employees,
        IEmployeeDependentParentRepository dependentParents,
        ITerminationSettlementRepository settlements,
        IOptions<AccountingSettings> settings)
    {
        _employees = employees;
        _dependentParents = dependentParents;
        _settlements = settlements;
        _settings = settings.Value;
    }

    public async Task<Result<TerminateEmployeeResultDto>> Handle(
        TerminateEmployeeCommand request,
        CancellationToken cancellationToken)
    {
        var dto = request.Dto;
        if (dto.TerminationDate.Date > DateTime.UtcNow.Date)
        {
            return Result.Failure<TerminateEmployeeResultDto>(
                Error.Validation("TerminationDate", "La date de sortie ne peut pas être dans le futur."));
        }

        var employee = await _employees.GetByIdWithContractsAsync(request.EmployeeId, cancellationToken);
        if (employee is null)
            return Result.Failure<TerminateEmployeeResultDto>(Error.NotFound("Employee", request.EmployeeId));

        EmploymentContract? closedContract = null;
        if (dto.CloseActiveContract)
        {
            var activeContract = employee.GetActiveContract(dto.TerminationDate);
            if (activeContract is not null)
            {
                var updateResult = activeContract.Update(
                    activeContract.Type,
                    activeContract.Regime,
                    activeContract.StartDate,
                    activeContract.BaseSalary,
                    activeContract.WorkAccidentRate,
                    dto.TerminationDate,
                    activeContract.JobTitle,
                    isActive: false,
                    activeContract.WeeklyRegime,
                    activeContract.CivpStartDate,
                    activeContract.CivpEndDate,
                    activeContract.CivpStateGrant,
                    activeContract.CivpEmployerAllowance,
                    activeContract.AnetiReference);
                if (updateResult.IsFailure)
                    return Result.Failure<TerminateEmployeeResultDto>(updateResult.Error);

                await _employees.UpdateContractAsync(activeContract, cancellationToken);
                closedContract = activeContract;
            }
        }

        employee.Terminate(dto.TerminationDate);

        if (dto.DeactivateNow)
        {
            await _dependentParents.EndAllActiveForEmployeeAsync(employee.Id, dto.TerminationDate, cancellationToken);
            employee.SyncDependentParentsCount(0);
        }

        await _employees.UpdateAsync(employee, cancellationToken);

        Guid? settlementId = null;
        var warning = "Pensez à lancer la régularisation IRPP (solde de tout compte) si l'option est activée sur l'exercice.";

        if (dto.CreateSettlement && _settings.PayrollTerminationIndemnityEnabled)
        {
            if (!Enum.TryParse<TerminationReason>(dto.TerminationReason, out var reason))
                reason = TerminationReason.Dismissal;

            var contract = closedContract
                ?? employee.GetActiveContract(dto.TerminationDate)
                ?? employee.Contracts.OrderByDescending(c => c.StartDate).FirstOrDefault();

            if (contract is not null)
            {
                var year = dto.TerminationDate.Year;
                var month = dto.TerminationDate.Month;
                var preview = TerminationMappings.BuildPreview(employee, contract, dto.TerminationDate, reason);
                var existing = await _settlements.GetByEmployeeAndMonthAsync(employee.Id, year, month, cancellationToken);

                if (existing is null)
                {
                    var create = TerminationSettlement.Create(
                        employee.Id, year, month, dto.TerminationDate, reason,
                        preview.SeniorityMonths, preview.GrossMonthlyReference, preview.LegalIndemnityAmount);
                    if (create.IsSuccess)
                    {
                        await _settlements.AddAsync(create.Value, cancellationToken);
                        settlementId = create.Value.Id;
                    }
                }
                else
                {
                    settlementId = existing.Id;
                }

                warning = "Solde de tout compte créé ou existant. Validez-le puis recalculez le cycle de paie du mois de sortie.";
            }
        }

        return Result.Success(new TerminateEmployeeResultDto
        {
            IsActive = employee.IsActive,
            TerminationDate = employee.TerminationDate!.Value,
            Warning = warning,
            SettlementId = settlementId
        });
    }
}
