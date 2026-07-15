using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Reports.Queries;

/// <summary>
/// Query to get product performance report (classement avec marge et part du CA).
/// </summary>
public sealed record GetProductPerformanceReportQuery(DateTime FromDate, DateTime ToDate)
    : IRequest<Result<IReadOnlyList<ProductPerformanceReportRowDto>>>;

/// <summary>
/// Handler for GetProductPerformanceReportQuery.
/// </summary>
public sealed class GetProductPerformanceReportQueryHandler
    : IRequestHandler<GetProductPerformanceReportQuery, Result<IReadOnlyList<ProductPerformanceReportRowDto>>>
{
    private readonly IInvoiceRepository _invoiceRepository;

    public GetProductPerformanceReportQueryHandler(IInvoiceRepository invoiceRepository)
    {
        _invoiceRepository = invoiceRepository;
    }

    public async Task<Result<IReadOnlyList<ProductPerformanceReportRowDto>>> Handle(
        GetProductPerformanceReportQuery request,
        CancellationToken cancellationToken)
    {
        var from = request.FromDate.Date;
        var to = request.ToDate.Date;
        var aggregated = await _invoiceRepository.GetProductPerformanceAggregatedAsync(from, to, cancellationToken);
        var totalRevenue = aggregated.Sum(x => x.Revenue);

        var rows = aggregated
            .Select(row =>
            {
                var profit = row.Revenue - row.TotalCost;
                decimal? marginPercent = row.Revenue > 0 ? (profit / row.Revenue) * 100 : null;
                var revenueSharePercent = totalRevenue > 0 ? (row.Revenue / totalRevenue) * 100 : 0m;

                return new ProductPerformanceReportRowDto
                {
                    ProductId = row.ProductId,
                    ProductName = row.ProductName,
                    ProductCode = row.ProductCode,
                    CategoryName = row.CategoryName,
                    QuantitySold = row.Quantity,
                    Revenue = row.Revenue,
                    UnitCost = row.Quantity > 0 ? row.TotalCost / row.Quantity : 0m,
                    TotalCost = row.TotalCost,
                    Profit = profit,
                    MarginPercent = marginPercent,
                    RevenueSharePercent = revenueSharePercent,
                    Currency = row.Currency
                };
            })
            .ToList();

        return Result.Success<IReadOnlyList<ProductPerformanceReportRowDto>>(rows);
    }
}