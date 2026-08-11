using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FluentValidation;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Commands;

public sealed record UpdatePayrollGarnishmentBracketsCommand(int FiscalYear, UpdatePayrollGarnishmentBracketsDto Dto)
    : IRequest<Result>;

public sealed class UpdatePayrollGarnishmentBracketsCommandValidator
    : AbstractValidator<UpdatePayrollGarnishmentBracketsCommand>
{
    public UpdatePayrollGarnishmentBracketsCommandValidator()
    {
        RuleFor(x => x.FiscalYear).InclusiveBetween(2000, 2100);

        RuleFor(x => x.Dto.Brackets)
            .NotEmpty()
            .WithMessage("Le barème de saisie doit comporter au moins une tranche.");

        RuleFor(x => x.Dto.Brackets)
            .Must(b => b.Count == 0 || b.OrderBy(t => t.LowerBoundMonthlyNet).First().LowerBoundMonthlyNet == 0m)
            .WithMessage("La première tranche de saisie doit démarrer à 0.");

        RuleFor(x => x.Dto.Brackets)
            .Must(b => b.Select(t => t.LowerBoundMonthlyNet).Distinct().Count() == b.Count)
            .WithMessage("Deux tranches de saisie ne peuvent pas avoir le même seuil inférieur.");

        RuleForEach(x => x.Dto.Brackets).ChildRules(bracket =>
        {
            bracket.RuleFor(b => b.LowerBoundMonthlyNet).GreaterThanOrEqualTo(0);
            bracket.RuleFor(b => b.SeizableFraction).InclusiveBetween(0, 1);
        });
    }
}

public sealed class UpdatePayrollGarnishmentBracketsCommandHandler
    : IRequestHandler<UpdatePayrollGarnishmentBracketsCommand, Result>
{
    private readonly IPayrollParametersRepository _parameters;

    public UpdatePayrollGarnishmentBracketsCommandHandler(IPayrollParametersRepository parameters)
    {
        _parameters = parameters;
    }

    public async Task<Result> Handle(UpdatePayrollGarnishmentBracketsCommand request, CancellationToken cancellationToken)
    {
        var parameters = await _parameters.GetOrCreateForYearAsync(request.FiscalYear, cancellationToken);

        var brackets = request.Dto.Brackets
            .OrderBy(b => b.LowerBoundMonthlyNet)
            .Select(b => PayrollGarnishmentBracket.Create(b.LowerBoundMonthlyNet, b.SeizableFraction))
            .ToList();

        var bracketResult = parameters.ReplaceGarnishmentBrackets(brackets);
        if (bracketResult.IsFailure)
            return bracketResult;

        await _parameters.UpdateAsync(parameters, cancellationToken);
        return Result.Success();
    }
}
