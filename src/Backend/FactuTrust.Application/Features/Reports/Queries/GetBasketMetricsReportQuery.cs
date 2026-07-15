using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Reports.Queries;

/// <summary>
/// Query to get basket metrics report (panier moyen et lignes par facture).
/// </summary>
public sealed record GetBasketMetricsReportQuery(DateTime FromDate, DateTime ToDate)
    : IRequest<Result<BasketMetricsReportDto>>;

/// <summary>
/// Handler for GetBasketMetricsReportQuery.
/// </summary>
public sealed class GetBasketMetricsReportQueryHandler
    : IRequestHandler<GetBasketMetricsReportQuery, Result<BasketMetricsReportDto>>
{
    private readonly IInvoiceRepository _invoiceRepository;

    public GetBasketMetricsReportQueryHandler(IInvoiceRepository invoiceRepository)
    {
        _invoiceRepository = invoiceRepository;
    }

    public async Task<Result<BasketMetricsReportDto>> Handle(
        GetBasketMetricsReportQuery request,
        CancellationToken cancellationToken)
    {
        var from = request.FromDate.Date;
        var to = request.ToDate.Date;
        var dto = await _invoiceRepository.GetBasketMetricsAggregatedAsync(from, to, cancellationToken);
        return Result.Success(dto);
    }
}