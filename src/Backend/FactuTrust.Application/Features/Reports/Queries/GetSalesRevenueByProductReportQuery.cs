using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Reports.Queries;

/// <summary>
/// Grouping mode for sales revenue report.
/// </summary>
public enum SalesRevenueGroupBy
{
    Product,
    Category,
    ProductAndClient,
    Client
}

/// <summary>
/// Query to get sales revenue report (CA par produit, par catégorie, ou par produit par client).
/// </summary>
public sealed record GetSalesRevenueByProductReportQuery(
    DateTime FromDate,
    DateTime ToDate,
    SalesRevenueGroupBy GroupBy)
    : IRequest<Result<IReadOnlyList<SalesRevenueReportRowDto>>>;

/// <summary>
/// Handler for GetSalesRevenueByProductReportQuery.
/// </summary>
public sealed class GetSalesRevenueByProductReportQueryHandler
    : IRequestHandler<GetSalesRevenueByProductReportQuery, Result<IReadOnlyList<SalesRevenueReportRowDto>>>
{
    private readonly IInvoiceRepository _invoiceRepository;

    public GetSalesRevenueByProductReportQueryHandler(IInvoiceRepository invoiceRepository)
    {
        _invoiceRepository = invoiceRepository;
    }

    public async Task<Result<IReadOnlyList<SalesRevenueReportRowDto>>> Handle(
        GetSalesRevenueByProductReportQuery request,
        CancellationToken cancellationToken)
    {
        var from = request.FromDate.Date;
        var to = request.ToDate.Date;
        var rows = await _invoiceRepository.GetSalesRevenueAggregatedAsync(from, to, request.GroupBy, cancellationToken);
        return Result.Success<IReadOnlyList<SalesRevenueReportRowDto>>(rows);
    }
}