using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FluentValidation;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Commands;

public sealed record CreatePayrollRunCommand(CreatePayrollRunDto Dto) : IRequest<Result<Guid>>;

public sealed class CreatePayrollRunCommandValidator : AbstractValidator<CreatePayrollRunCommand>
{
    public CreatePayrollRunCommandValidator()
    {
        RuleFor(x => x.Dto.Year).InclusiveBetween(2000, 2100).WithMessage("L'année est invalide.");
        RuleFor(x => x.Dto.Month).InclusiveBetween(1, 12).WithMessage("Le mois est invalide.");
    }
}

public sealed class CreatePayrollRunCommandHandler : IRequestHandler<CreatePayrollRunCommand, Result<Guid>>
{
    private readonly IPayrollRunRepository _runs;
    private readonly IPayrollParametersRepository _parameters;
    private readonly ITenantContext _tenantContext;

    public CreatePayrollRunCommandHandler(
        IPayrollRunRepository runs,
        IPayrollParametersRepository parameters,
        ITenantContext tenantContext)
    {
        _runs = runs;
        _parameters = parameters;
        _tenantContext = tenantContext;
    }

    public async Task<Result<Guid>> Handle(CreatePayrollRunCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto;

        if (_tenantContext.TenantId is null)
            return Result.Failure<Guid>(Error.Unauthorized("Aucun contexte d'entreprise disponible."));

        if (await _runs.ExistsForPeriodAsync(dto.Year, dto.Month, cancellationToken))
            return Result.Failure<Guid>(Error.Conflict($"Un cycle de paie existe déjà pour {dto.Month:D2}/{dto.Year}."));

        // Ensure legal parameters exist for the run's fiscal year (seed defaults on first access).
        await _parameters.GetOrCreateForYearAsync(dto.Year, cancellationToken);

        var runResult = PayrollRun.Create(dto.Year, dto.Month, dto.Year, dto.Label);
        if (runResult.IsFailure)
            return Result.Failure<Guid>(runResult.Error);

        var run = runResult.Value;
        await _runs.AddAsync(run, cancellationToken);
        return Result.Success(run.Id);
    }
}
