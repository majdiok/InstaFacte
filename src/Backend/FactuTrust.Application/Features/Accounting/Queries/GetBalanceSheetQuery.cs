using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Accounting.Queries;

public sealed record GetBalanceSheetQuery(int FiscalYear) : IRequest<Result<BalanceSheetDto>>;

public sealed class GetBalanceSheetQueryHandler
    : IRequestHandler<GetBalanceSheetQuery, Result<BalanceSheetDto>>
{
    private readonly IAccountingReportingService _reporting;

    public GetBalanceSheetQueryHandler(IAccountingReportingService reporting)
    {
        _reporting = reporting;
    }

    public Task<Result<BalanceSheetDto>> Handle(GetBalanceSheetQuery request, CancellationToken cancellationToken)
        => _reporting.GetBalanceSheetAsync(request.FiscalYear, cancellationToken);
}
