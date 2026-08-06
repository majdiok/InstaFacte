using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Commands;

public sealed record TerminateEmployeeDto(
    DateTime TerminationDate,
    string? Reason,
    bool CloseActiveContract = true,
    bool DeactivateNow = true);

public sealed record TerminateEmployeeCommand(Guid EmployeeId, TerminateEmployeeDto Dto)
    : IRequest<Result<TerminateEmployeeResultDto>>;

public sealed class TerminateEmployeeResultDto
{
    public bool IsActive { get; init; }
    public DateTime TerminationDate { get; init; }
    public string? Warning { get; init; }
}

public sealed class TerminateEmployeeCommandHandler
    : IRequestHandler<TerminateEmployeeCommand, Result<TerminateEmployeeResultDto>>
{
    private readonly IEmployeeRepository _employees;
    private readonly IEmployeeDependentParentRepository _dependentParents;

    public TerminateEmployeeCommandHandler(
        IEmployeeRepository employees,
        IEmployeeDependentParentRepository dependentParents)
    {
        _employees = employees;
        _dependentParents = dependentParents;
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
                    activeContract.WeeklyRegime);
                if (updateResult.IsFailure)
                    return Result.Failure<TerminateEmployeeResultDto>(updateResult.Error);

                await _employees.UpdateContractAsync(activeContract, cancellationToken);
            }
        }

        employee.Terminate(dto.TerminationDate);

        if (dto.DeactivateNow)
        {
            await _dependentParents.EndAllActiveForEmployeeAsync(employee.Id, dto.TerminationDate, cancellationToken);
            employee.SyncDependentParentsCount(0);
        }

        await _employees.UpdateAsync(employee, cancellationToken);

        return Result.Success(new TerminateEmployeeResultDto
        {
            IsActive = employee.IsActive,
            TerminationDate = employee.TerminationDate!.Value,
            Warning = "Pensez à lancer la régularisation IRPP (solde de tout compte) si l'option est activée sur l'exercice."
        });
    }
}
