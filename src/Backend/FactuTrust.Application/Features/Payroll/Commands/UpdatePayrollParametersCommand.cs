using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FluentValidation;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Commands;

public sealed record UpdatePayrollParametersCommand(int FiscalYear, UpdatePayrollParametersDto Dto) : IRequest<Result>;

public sealed class UpdatePayrollParametersCommandValidator : AbstractValidator<UpdatePayrollParametersCommand>
{
    public UpdatePayrollParametersCommandValidator()
    {
        RuleFor(x => x.FiscalYear).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Dto.IrppBrackets).NotEmpty().WithMessage("Le barème IRPP doit comporter au moins une tranche.");
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
            dto.EnableAllowanceQuadrantMatrix);
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
