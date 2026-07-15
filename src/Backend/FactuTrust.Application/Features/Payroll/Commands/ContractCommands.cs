using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FluentValidation;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Commands;

// ── Add contract ──
public sealed record AddContractCommand(Guid EmployeeId, CreateContractDto Dto) : IRequest<Result<Guid>>;

public sealed class AddContractCommandValidator : AbstractValidator<AddContractCommand>
{
    public AddContractCommandValidator()
    {
        RuleFor(x => x.Dto.StartDate).NotEmpty().WithMessage("La date de début du contrat est obligatoire.");
        RuleFor(x => x.Dto.BaseSalary).GreaterThanOrEqualTo(0).WithMessage("Le salaire de base ne peut pas être négatif.");
        RuleFor(x => x.Dto.WorkAccidentRate).InclusiveBetween(0, 100).WithMessage("Le taux d'accident de travail doit être compris entre 0 et 100 %.");
        RuleFor(x => x.Dto).Must(d => d.Type != "Cdd" || d.EndDate.HasValue)
            .WithMessage("La date de fin est obligatoire pour un CDD.");
    }
}

public sealed class AddContractCommandHandler : IRequestHandler<AddContractCommand, Result<Guid>>
{
    private readonly IEmployeeRepository _employees;
    private readonly IPayrollParametersRepository _parameters;

    public AddContractCommandHandler(IEmployeeRepository employees, IPayrollParametersRepository parameters)
    {
        _employees = employees;
        _parameters = parameters;
    }

    public async Task<Result<Guid>> Handle(AddContractCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto;

        if (!await _employees.ExistsAsync(request.EmployeeId, cancellationToken))
            return Result.Failure<Guid>(Error.NotFound("Employee", request.EmployeeId));

        if (!Enum.TryParse<ContractType>(dto.Type, out var type))
            return Result.Failure<Guid>(Error.Validation("Type", "Type de contrat invalide."));
        if (!Enum.TryParse<SocialRegime>(dto.Regime, out var regime))
            return Result.Failure<Guid>(Error.Validation("Regime", "Régime social invalide."));

        var fiscalYear = dto.StartDate.Year;
        var parameters = await _parameters.GetOrCreateForYearAsync(fiscalYear, cancellationToken);
        if (parameters.EnforceSmigOnContracts && dto.BaseSalary < parameters.MonthlySmig)
            return Result.Failure<Guid>(Error.Validation("BaseSalary", $"Le salaire de base ne peut pas être inférieur au SMIG ({parameters.MonthlySmig:N3} TND)."));

        var contractResult = EmploymentContract.CreatePublic(
            request.EmployeeId, type, regime, dto.StartDate, dto.BaseSalary, dto.WorkAccidentRate, dto.EndDate, dto.JobTitle);
        if (contractResult.IsFailure)
            return Result.Failure<Guid>(contractResult.Error);

        var contract = contractResult.Value;
        foreach (var allowance in dto.Allowances)
        {
            var allowanceResult = contract.AddAllowance(allowance.Label, allowance.Amount, allowance.Taxable, allowance.SubjectToCnss);
            if (allowanceResult.IsFailure)
                return Result.Failure<Guid>(allowanceResult.Error);
        }

        await _employees.AddContractAsync(contract, cancellationToken);
        return Result.Success(contract.Id);
    }
}

// ── Update contract ──
public sealed record UpdateContractCommand(Guid ContractId, UpdateContractDto Dto) : IRequest<Result>;

public sealed class UpdateContractCommandValidator : AbstractValidator<UpdateContractCommand>
{
    public UpdateContractCommandValidator()
    {
        RuleFor(x => x.Dto.BaseSalary).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Dto.WorkAccidentRate).InclusiveBetween(0, 100);
    }
}

public sealed class UpdateContractCommandHandler : IRequestHandler<UpdateContractCommand, Result>
{
    private readonly IEmployeeRepository _employees;
    private readonly IPayrollParametersRepository _parameters;

    public UpdateContractCommandHandler(IEmployeeRepository employees, IPayrollParametersRepository parameters)
    {
        _employees = employees;
        _parameters = parameters;
    }

    public async Task<Result> Handle(UpdateContractCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto;

        var contract = await _employees.GetContractAsync(request.ContractId, cancellationToken);
        if (contract is null)
            return Result.Failure(Error.NotFound("EmploymentContract", request.ContractId));

        if (!Enum.TryParse<ContractType>(dto.Type, out var type))
            return Result.Failure(Error.Validation("Type", "Type de contrat invalide."));
        if (!Enum.TryParse<SocialRegime>(dto.Regime, out var regime))
            return Result.Failure(Error.Validation("Regime", "Régime social invalide."));

        var fiscalYear = dto.StartDate.Year;
        var parameters = await _parameters.GetOrCreateForYearAsync(fiscalYear, cancellationToken);
        if (parameters.EnforceSmigOnContracts && dto.BaseSalary < parameters.MonthlySmig)
            return Result.Failure(Error.Validation("BaseSalary", $"Le salaire de base ne peut pas être inférieur au SMIG ({parameters.MonthlySmig:N3} TND)."));

        var updateResult = contract.Update(type, regime, dto.StartDate, dto.BaseSalary, dto.WorkAccidentRate, dto.EndDate, dto.JobTitle, dto.IsActive);
        if (updateResult.IsFailure)
            return updateResult;

        contract.ClearAllowances();
        foreach (var allowance in dto.Allowances)
        {
            var allowanceResult = contract.AddAllowance(allowance.Label, allowance.Amount, allowance.Taxable, allowance.SubjectToCnss);
            if (allowanceResult.IsFailure)
                return allowanceResult;
        }

        await _employees.UpdateContractAsync(contract, cancellationToken);
        return Result.Success();
    }
}

// ── Delete contract ──
public sealed record DeleteContractCommand(Guid ContractId) : IRequest<Result>;

public sealed class DeleteContractCommandHandler : IRequestHandler<DeleteContractCommand, Result>
{
    private readonly IEmployeeRepository _employees;

    public DeleteContractCommandHandler(IEmployeeRepository employees)
    {
        _employees = employees;
    }

    public async Task<Result> Handle(DeleteContractCommand request, CancellationToken cancellationToken)
    {
        var contract = await _employees.GetContractAsync(request.ContractId, cancellationToken);
        if (contract is null)
            return Result.Failure(Error.NotFound("EmploymentContract", request.ContractId));

        await _employees.DeleteContractAsync(request.ContractId, cancellationToken);
        return Result.Success();
    }
}
