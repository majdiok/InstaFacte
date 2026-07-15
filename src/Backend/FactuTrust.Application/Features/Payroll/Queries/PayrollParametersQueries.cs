using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Queries;

// ── Get by year (seeds defaults on first access) ──
public sealed record GetPayrollParametersQuery(int FiscalYear) : IRequest<Result<PayrollParametersDto>>;

public sealed class GetPayrollParametersQueryHandler : IRequestHandler<GetPayrollParametersQuery, Result<PayrollParametersDto>>
{
    private readonly IPayrollParametersRepository _parameters;

    public GetPayrollParametersQueryHandler(IPayrollParametersRepository parameters)
    {
        _parameters = parameters;
    }

    public async Task<Result<PayrollParametersDto>> Handle(GetPayrollParametersQuery request, CancellationToken cancellationToken)
    {
        if (request.FiscalYear is < 2000 or > 2100)
            return Result.Failure<PayrollParametersDto>(Error.Validation("FiscalYear", "L'exercice est invalide."));

        var parameters = await _parameters.GetOrCreateForYearAsync(request.FiscalYear, cancellationToken);
        return Result.Success(PayrollMappings.ToDto(parameters));
    }
}

// ── List all years ──
public sealed record ListPayrollParametersQuery : IRequest<IReadOnlyList<PayrollParametersDto>>;

public sealed class ListPayrollParametersQueryHandler : IRequestHandler<ListPayrollParametersQuery, IReadOnlyList<PayrollParametersDto>>
{
    private readonly IPayrollParametersRepository _parameters;

    public ListPayrollParametersQueryHandler(IPayrollParametersRepository parameters)
    {
        _parameters = parameters;
    }

    public async Task<IReadOnlyList<PayrollParametersDto>> Handle(ListPayrollParametersQuery request, CancellationToken cancellationToken)
    {
        var list = await _parameters.ListAsync(cancellationToken);
        return list.Select(PayrollMappings.ToDto).ToList();
    }
}
