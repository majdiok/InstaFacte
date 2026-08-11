using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Queries;

public sealed record GetPayrollGarnishmentBracketsQuery(int FiscalYear)
    : IRequest<Result<IReadOnlyList<PayrollGarnishmentBracketDto>>>;

public sealed class GetPayrollGarnishmentBracketsQueryHandler
    : IRequestHandler<GetPayrollGarnishmentBracketsQuery, Result<IReadOnlyList<PayrollGarnishmentBracketDto>>>
{
    private readonly IPayrollParametersRepository _parameters;

    public GetPayrollGarnishmentBracketsQueryHandler(IPayrollParametersRepository parameters)
    {
        _parameters = parameters;
    }

    public async Task<Result<IReadOnlyList<PayrollGarnishmentBracketDto>>> Handle(
        GetPayrollGarnishmentBracketsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.FiscalYear is < 2000 or > 2100)
            return Result.Failure<IReadOnlyList<PayrollGarnishmentBracketDto>>(
                Error.Validation("FiscalYear", "L'exercice est invalide."));

        var parameters = await _parameters.GetOrCreateForYearAsync(request.FiscalYear, cancellationToken);
        return Result.Success(PayrollMappings.ToGarnishmentBracketDtos(parameters));
    }
}
