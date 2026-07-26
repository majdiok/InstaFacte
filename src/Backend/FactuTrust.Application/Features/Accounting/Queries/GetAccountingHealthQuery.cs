using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Accounting.Queries;

/// <summary>
/// Centre de contrôle d'intégrité (lecture seule). <see cref="FiscalYear"/> null = tout l'historique.
/// </summary>
public sealed record GetAccountingHealthQuery(int? FiscalYear) : IRequest<Result<AccountingHealthReportDto>>;

public sealed class GetAccountingHealthQueryHandler
    : IRequestHandler<GetAccountingHealthQuery, Result<AccountingHealthReportDto>>
{
    private readonly IAccountingHealthService _service;

    public GetAccountingHealthQueryHandler(IAccountingHealthService service)
    {
        _service = service;
    }

    public Task<Result<AccountingHealthReportDto>> Handle(GetAccountingHealthQuery request, CancellationToken cancellationToken)
        => _service.RunAsync(request.FiscalYear, cancellationToken);
}
