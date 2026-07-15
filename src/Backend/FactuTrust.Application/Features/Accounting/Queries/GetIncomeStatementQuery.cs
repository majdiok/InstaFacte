using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Accounting.Queries;

public sealed record GetIncomeStatementQuery(int FiscalYear) : IRequest<Result<IncomeStatementDto>>;

public sealed class GetIncomeStatementQueryHandler
    : IRequestHandler<GetIncomeStatementQuery, Result<IncomeStatementDto>>
{
    private readonly IAccountingReportingService _reporting;

    public GetIncomeStatementQueryHandler(IAccountingReportingService reporting)
    {
        _reporting = reporting;
    }

    public Task<Result<IncomeStatementDto>> Handle(GetIncomeStatementQuery request, CancellationToken cancellationToken)
        => _reporting.GetIncomeStatementAsync(request.FiscalYear, cancellationToken);
}
