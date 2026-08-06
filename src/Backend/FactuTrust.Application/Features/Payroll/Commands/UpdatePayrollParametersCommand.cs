using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FluentValidation;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Commands;

public sealed record UpdatePayrollParametersCommand(int FiscalYear, UpdatePayrollParametersDto Dto) : IRequest<Result>;

public sealed class UpdatePayrollParametersCommandValidator : AbstractValidator<UpdatePayrollParametersCommand>
{
    public UpdatePayrollParametersCommandValidator()
    {
        RuleFor(x => x.FiscalYear).InclusiveBetween(2000, 2100);

        // Taux exprimés en pourcentage.
        RuleFor(x => x.Dto.CnssEmployeeRate).InclusiveBetween(0, 100);
        RuleFor(x => x.Dto.CnssEmployerRate).InclusiveBetween(0, 100);
        RuleFor(x => x.Dto.CnssEmployeeRateRsa).InclusiveBetween(0, 100);
        RuleFor(x => x.Dto.CnssEmployerRateRsa).InclusiveBetween(0, 100);
        RuleFor(x => x.Dto.CssRate).InclusiveBetween(0, 100);
        RuleFor(x => x.Dto.CssEmployerRate).InclusiveBetween(0, 100);
        RuleFor(x => x.Dto.ProfessionalExpensesRate).InclusiveBetween(0, 100);
        RuleFor(x => x.Dto.TfpRateIndustry).InclusiveBetween(0, 100);
        RuleFor(x => x.Dto.TfpRateOther).InclusiveBetween(0, 100);
        RuleFor(x => x.Dto.FoprolosRate).InclusiveBetween(0, 100);
        RuleFor(x => x.Dto.ParentDeductionRatePercent).InclusiveBetween(0, 100);

        // Montants et plafonds.
        RuleFor(x => x.Dto.CssAnnualExemptionThreshold).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Dto.ProfessionalExpensesAnnualCap).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Dto.HeadOfFamilyAnnualDeduction).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Dto.ChildAnnualDeduction).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Dto.StudentChildAnnualDeduction).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Dto.DisabledChildAnnualDeduction).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Dto.ParentAnnualDeductionCap).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Dto.MonthlySmig).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Dto.MaxDeductibleChildren).InclusiveBetween(0, 10);

        RuleFor(x => x.Dto.SmigIrppExemptionRateOverride)
            .InclusiveBetween(0, 100)
            .When(x => x.Dto.SmigIrppExemptionRateOverride.HasValue);

        RuleFor(x => x.Dto)
            .Must(d => !Enum.TryParse<SmigIrppExemptionMode>(d.SmigIrppExemptionMode, true, out var mode)
                       || mode == SmigIrppExemptionMode.None
                       || d.MonthlySmig > 0)
            .WithMessage("Le SMIG mensuel doit être positif pour activer l'exonération IRPP SMIG.")
            .WithName("MonthlySmig");

        // Barème IRPP : première tranche à 0, seuils strictement croissants, taux 0-100.
        RuleFor(x => x.Dto.IrppBrackets).NotEmpty().WithMessage("Le barème IRPP doit comporter au moins une tranche.");
        RuleFor(x => x.Dto.IrppBrackets)
            .Must(b => b.Count == 0 || b.OrderBy(t => t.LowerBound).First().LowerBound == 0m)
            .WithMessage("La première tranche IRPP doit démarrer à 0.");
        RuleFor(x => x.Dto.IrppBrackets)
            .Must(b => b.Select(t => t.LowerBound).Distinct().Count() == b.Count)
            .WithMessage("Deux tranches IRPP ne peuvent pas avoir le même seuil inférieur.");
        RuleForEach(x => x.Dto.IrppBrackets).ChildRules(bracket =>
        {
            bracket.RuleFor(b => b.LowerBound).GreaterThanOrEqualTo(0);
            bracket.RuleFor(b => b.Rate).InclusiveBetween(0, 100);
        });
    }
}

public sealed class UpdatePayrollParametersCommandHandler : IRequestHandler<UpdatePayrollParametersCommand, Result>
{
    private readonly IPayrollParametersRepository _parameters;

    public UpdatePayrollParametersCommandHandler(IPayrollParametersRepository parameters)
    {
        _parameters = parameters;
    }

    public async Task<Result> Handle(UpdatePayrollParametersCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto;

        if (!Enum.TryParse<SmigIrppExemptionMode>(dto.SmigIrppExemptionMode, true, out var smigExemptionMode))
            return Result.Failure(Error.Validation("SmigIrppExemptionMode", "Mode d'exonération IRPP SMIG invalide."));

        var parameters = await _parameters.GetOrCreateForYearAsync(request.FiscalYear, cancellationToken);

        var ratesResult = parameters.UpdateRates(
            dto.CnssEmployeeRate,
            dto.CnssEmployerRate,
            dto.CssRate,
            dto.CssAnnualExemptionThreshold,
            dto.ProfessionalExpensesRate,
            dto.ProfessionalExpensesAnnualCap,
            dto.HeadOfFamilyAnnualDeduction,
            dto.ChildAnnualDeduction,
            dto.MaxDeductibleChildren,
            dto.TfpRateIndustry,
            dto.TfpRateOther,
            dto.FoprolosRate,
            dto.MonthlySmig,
            dto.CnssEmployeeRateRsa,
            dto.CnssEmployerRateRsa,
            dto.EnforceSmigOnContracts,
            dto.EnableExtendedOvertimeRates,
            dto.EnableAllowanceQuadrantMatrix,
            dto.StudentChildAnnualDeduction,
            dto.DisabledChildAnnualDeduction,
            dto.ParentDeductionRatePercent,
            dto.ParentAnnualDeductionCap,
            dto.IsIndustrialSector,
            mealVoucherDailyExemptionCap: parameters.MealVoucherDailyExemptionCap,
            enableIrppRegularization: parameters.EnableIrppRegularization,
            smigIrppExemptionMode: smigExemptionMode,
            smigIrppExemptionRateOverride: dto.SmigIrppExemptionRateOverride,
            cssEmployerRate: dto.CssEmployerRate);
        if (ratesResult.IsFailure)
            return ratesResult;

        var brackets = dto.IrppBrackets
            .OrderBy(b => b.LowerBound)
            .Select(b => PayrollIrppBracket.Create(b.LowerBound, b.Rate))
            .ToList();

        var bracketResult = parameters.ReplaceIrppBrackets(brackets);
        if (bracketResult.IsFailure)
            return bracketResult;

        await _parameters.UpdateAsync(parameters, cancellationToken);
        return Result.Success();
    }
}
