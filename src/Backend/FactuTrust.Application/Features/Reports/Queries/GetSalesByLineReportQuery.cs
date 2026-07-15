using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Reports.Queries;

/// <summary>
/// Query to get sales by line report (détails ventes par ligne produit).
/// </summary>
public sealed record GetSalesByLineReportQuery(DateTime FromDate, DateTime ToDate)
    : IRequest<Result<IReadOnlyList<SalesByLineReportRowDto>>>;

/// <summary>
/// Handler for GetSalesByLineReportQuery.
/// </summary>
public sealed class GetSalesByLineReportQueryHandler
    : IRequestHandler<GetSalesByLineReportQuery, Result<IReadOnlyList<SalesByLineReportRowDto>>>
{
    private readonly IInvoiceRepository _invoiceRepository;

    public GetSalesByLineReportQueryHandler(IInvoiceRepository invoiceRepository)
    {
        _invoiceRepository = invoiceRepository;
    }

    public async Task<Result<IReadOnlyList<SalesByLineReportRowDto>>> Handle(
        GetSalesByLineReportQuery request,
        CancellationToken cancellationToken)
    {
        var from = request.FromDate.Date;
        var to = request.ToDate.Date;
        var rows = await _invoiceRepository.GetSalesByLineAggregatedAsync(from, to, cancellationToken);
        return Result.Success<IReadOnlyList<SalesByLineReportRowDto>>(rows);
    }
}