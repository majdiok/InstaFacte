using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Accounting.Queries;

public sealed record GetNctStatementsQuery(int FiscalYear) : IRequest<Result<NctFinancialStatementsDto>>;

public sealed class GetNctStatementsQueryHandler
    : IRequestHandler<GetNctStatementsQuery, Result<NctFinancialStatementsDto>>
{
    private readonly IAccountingReportingService _reporting;

    public GetNctStatementsQueryHandler(IAccountingReportingService reporting)
    {
        _reporting = reporting;
    }

    public Task<Result<NctFinancialStatementsDto>> Handle(GetNctStatementsQuery request, CancellationToken cancellationToken)
        => _reporting.GetNctStatementsAsync(request.FiscalYear, cancellationToken);
}
