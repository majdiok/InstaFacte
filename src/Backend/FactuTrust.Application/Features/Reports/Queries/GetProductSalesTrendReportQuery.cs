using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Reports.Queries;

/// <summary>
/// Query to get product sales trend report (évolution ventes par produit par mois).
/// </summary>
public sealed record GetProductSalesTrendReportQuery(DateTime FromDate, DateTime ToDate)
    : IRequest<Result<IReadOnlyList<ProductSalesTrendRowDto>>>;

/// <summary>
/// Handler for GetProductSalesTrendReportQuery.
/// </summary>
public sealed class GetProductSalesTrendReportQueryHandler
    : IRequestHandler<GetProductSalesTrendReportQuery, Result<IReadOnlyList<ProductSalesTrendRowDto>>>
{
    private readonly IInvoiceRepository _invoiceRepository;

    public GetProductSalesTrendReportQueryHandler(IInvoiceRepository invoiceRepository)
    {
        _invoiceRepository = invoiceRepository;
    }

    public async Task<Result<IReadOnlyList<ProductSalesTrendRowDto>>> Handle(
        GetProductSalesTrendReportQuery request,
        CancellationToken cancellationToken)
    {
        var from = request.FromDate.Date;
        var to = request.ToDate.Date;
        var aggregated = await _invoiceRepository.GetProductSalesTrendAggregatedAsync(from, to, cancellationToken);

        var rows = aggregated
            .Select(row => new ProductSalesTrendRowDto
            {
                ProductId = row.ProductId,
                ProductName = row.ProductName,
                ProductCode = row.ProductCode,
                CategoryName = row.CategoryName,
                Period = $"{row.Year:D4}-{row.Month:D2}",
                Quantity = row.Quantity,
                Revenue = row.Revenue,
                Currency = row.Currency
            })
            .ToList();

        return Result.Success<IReadOnlyList<ProductSalesTrendRowDto>>(rows);
    }
}