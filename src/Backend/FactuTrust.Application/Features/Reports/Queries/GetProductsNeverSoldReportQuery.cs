using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Reports.Queries;

/// <summary>
/// Query to get products never sold report (produits actifs sans vente sur la période).
/// </summary>
public sealed record GetProductsNeverSoldReportQuery(DateTime FromDate, DateTime ToDate)
    : IRequest<Result<IReadOnlyList<ProductNeverSoldReportRowDto>>>;

/// <summary>
/// Handler for GetProductsNeverSoldReportQuery.
/// </summary>
public sealed class GetProductsNeverSoldReportQueryHandler
    : IRequestHandler<GetProductsNeverSoldReportQuery, Result<IReadOnlyList<ProductNeverSoldReportRowDto>>>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IProductRepository _productRepository;

    public GetProductsNeverSoldReportQueryHandler(
        IInvoiceRepository invoiceRepository,
        IProductRepository productRepository)
    {
        _invoiceRepository = invoiceRepository;
        _productRepository = productRepository;
    }

    public async Task<Result<IReadOnlyList<ProductNeverSoldReportRowDto>>> Handle(
        GetProductsNeverSoldReportQuery request,
        CancellationToken cancellationToken)
    {
        var from = request.FromDate.Date;
        var to = request.ToDate.Date;

        var soldProductIdsTask = _invoiceRepository.GetSoldProductIdsInDateRangeAsync(from, to, cancellationToken);
        var activeProductsTask = _productRepository.GetActiveProductsWithCategoryAsync(cancellationToken);
        await Task.WhenAll(soldProductIdsTask, activeProductsTask);

        var soldProductIds = await soldProductIdsTask;
        var activeProducts = await activeProductsTask;

        var neverSold = activeProducts
            .Where(p => !soldProductIds.Contains(p.Id))
            .Select(p => new ProductNeverSoldReportRowDto
            {
                ProductId = p.Id,
                ProductName = p.Name,
                ProductCode = p.Code,
                CategoryName = p.Category?.Name,
                UnitPrice = p.UnitPrice.Amount,
                Currency = p.UnitPrice.Currency
            })
            .OrderBy(p => p.ProductName)
            .ToList();

        return Result.Success<IReadOnlyList<ProductNeverSoldReportRowDto>>(neverSold);
    }
}