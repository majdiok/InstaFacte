using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.Reports.Queries;

/// <summary>
/// Grouping for commercial profit report.
/// </summary>
public enum CommercialProfitGroupBy
{
    Piece,
    Line,
    Product,
    Month
}

/// <summary>
/// Query to get commercial profit report (revenue - cost using Product.PurchasePrice).
/// </summary>
public sealed record GetCommercialProfitReportQuery(
    DateTime FromDate,
    DateTime ToDate,
    CommercialProfitGroupBy GroupBy) : IRequest<Result<IReadOnlyList<CommercialProfitReportRowDto>>>;

/// <summary>
/// Handler for GetCommercialProfitReportQuery.
/// </summary>
public sealed class GetCommercialProfitReportQueryHandler
    : IRequestHandler<GetCommercialProfitReportQuery, Result<IReadOnlyList<CommercialProfitReportRowDto>>>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IStockMovementRepository _stockMovementRepository;

    public GetCommercialProfitReportQueryHandler(
        IInvoiceRepository invoiceRepository,
        IStockMovementRepository stockMovementRepository)
    {
        _invoiceRepository = invoiceRepository;
        _stockMovementRepository = stockMovementRepository;
    }

    public async Task<Result<IReadOnlyList<CommercialProfitReportRowDto>>> Handle(
        GetCommercialProfitReportQuery request,
        CancellationToken cancellationToken)
    {
        var from = request.FromDate.Date;
        var to = request.ToDate.Date;
        var lineSources = await _invoiceRepository.GetCommercialProfitLineSourcesAsync(from, to, cancellationToken);

        var referenceByInvoiceId = new Dictionary<Guid, string>(capacity: lineSources.Select(l => l.InvoiceId).Distinct().Count());
        var saleReferences = new HashSet<string>(StringComparer.Ordinal);
        var deliveryReferences = new HashSet<string>(StringComparer.Ordinal);

        foreach (var group in lineSources.GroupBy(l => l.InvoiceId))
        {
            var sample = group.First();
            var invoiceId = group.Key;
            var isDelivery = !string.IsNullOrWhiteSpace(sample.Reference)
                && sample.Reference.StartsWith("BL ", StringComparison.Ordinal);
            var stockReference = isDelivery ? sample.Reference! : $"Facture {sample.InvoiceNumber}";
            referenceByInvoiceId[invoiceId] = stockReference;
            if (isDelivery)
                deliveryReferences.Add(stockReference);
            else
                saleReferences.Add(stockReference);
        }

        var saleExitCostsTask = saleReferences.Count > 0
            ? _stockMovementRepository.GetExitMovementsCostByReferencesAsync(
                saleReferences.ToList(),
                MovementReason.Sale,
                cancellationToken)
            : Task.FromResult<IReadOnlyList<StockExitMovementCostRowDto>>(Array.Empty<StockExitMovementCostRowDto>());

        var deliveryExitCostsTask = deliveryReferences.Count > 0
            ? _stockMovementRepository.GetExitMovementsCostByReferencesAsync(
                deliveryReferences.ToList(),
                MovementReason.Delivery,
                cancellationToken)
            : Task.FromResult<IReadOnlyList<StockExitMovementCostRowDto>>(Array.Empty<StockExitMovementCostRowDto>());

        await Task.WhenAll(saleExitCostsTask, deliveryExitCostsTask);

        var unitCostByReferenceProduct = BuildUnitCostByReferenceProduct(
            await saleExitCostsTask,
            await deliveryExitCostsTask);

        var intermediate = new List<CommercialProfitReportRowDto>(capacity: lineSources.Count);

        foreach (var line in lineSources)
        {
            var stockReference = referenceByInvoiceId[line.InvoiceId];
            var period = line.IssueDate.ToString("yyyy-MM");
            var unitCost = line.ProductId is { } productId
                && unitCostByReferenceProduct.TryGetValue((stockReference, productId), out var uc)
                ? uc
                : line.FallbackUnitCost;
            var cost = line.Quantity * unitCost;

            intermediate.Add(new CommercialProfitReportRowDto
            {
                ProductId = line.ProductId,
                ProductName = line.ProductName,
                ProductCode = line.ProductCode,
                InvoiceNumber = line.InvoiceNumber,
                IssueDate = line.IssueDate,
                Period = period,
                Quantity = line.Quantity,
                Revenue = line.Revenue,
                Cost = cost,
                Profit = line.Revenue - cost,
                Currency = line.Currency
            });
        }

        IReadOnlyList<CommercialProfitReportRowDto> rows = request.GroupBy switch
        {
            CommercialProfitGroupBy.Line => intermediate
                .OrderByDescending(r => r.IssueDate)
                .ThenBy(r => r.InvoiceNumber)
                .ToList(),

            CommercialProfitGroupBy.Product => intermediate
                .GroupBy(r => r.ProductId)
                .Select(g =>
                {
                    var first = g.First();
                    var qty = g.Sum(x => x.Quantity);
                    var rev = g.Sum(x => x.Revenue);
                    var cost = g.Sum(x => x.Cost);
                    return new CommercialProfitReportRowDto
                    {
                        ProductId = first.ProductId,
                        ProductName = first.ProductName,
                        ProductCode = first.ProductCode,
                        Quantity = qty,
                        Revenue = rev,
                        Cost = cost,
                        Profit = rev - cost,
                        Currency = first.Currency
                    };
                })
                .OrderByDescending(r => r.Profit)
                .ToList(),

            CommercialProfitGroupBy.Month => intermediate
                .GroupBy(r => r.Period)
                .Select(g =>
                {
                    var first = g.First();
                    var qty = g.Sum(x => x.Quantity);
                    var rev = g.Sum(x => x.Revenue);
                    var cost = g.Sum(x => x.Cost);
                    return new CommercialProfitReportRowDto
                    {
                        Period = g.Key,
                        Quantity = qty,
                        Revenue = rev,
                        Cost = cost,
                        Profit = rev - cost,
                        Currency = first.Currency
                    };
                })
                .OrderBy(r => r.Period)
                .ToList(),

            CommercialProfitGroupBy.Piece => intermediate
                .GroupBy(r => r.InvoiceNumber)
                .Select(g =>
                {
                    var first = g.First();
                    var qty = g.Sum(x => x.Quantity);
                    var rev = g.Sum(x => x.Revenue);
                    var cost = g.Sum(x => x.Cost);
                    return new CommercialProfitReportRowDto
                    {
                        InvoiceNumber = g.Key,
                        IssueDate = first.IssueDate,
                        Quantity = qty,
                        Revenue = rev,
                        Cost = cost,
                        Profit = rev - cost,
                        Currency = first.Currency,
                    };
                })
                .OrderByDescending(r => r.IssueDate)
                .ThenBy(r => r.InvoiceNumber)
                .ToList(),

            _ => intermediate
                .OrderByDescending(r => r.IssueDate)
                .ThenBy(r => r.InvoiceNumber)
                .ToList()
        };

        return Result.Success(rows);
    }

    private static Dictionary<(string Reference, Guid ProductId), decimal> BuildUnitCostByReferenceProduct(
        IReadOnlyList<StockExitMovementCostRowDto> saleExitCosts,
        IReadOnlyList<StockExitMovementCostRowDto> deliveryExitCosts)
    {
        var qtyAndTotalCostByReferenceProduct = new Dictionary<(string Reference, Guid ProductId), (decimal Qty, decimal TotalCost)>();

        void Accumulate(IEnumerable<StockExitMovementCostRowDto> rows)
        {
            foreach (var row in rows)
            {
                var key = (row.Reference, row.ProductId);
                if (!qtyAndTotalCostByReferenceProduct.TryGetValue(key, out var existing))
                    existing = (0m, 0m);

                qtyAndTotalCostByReferenceProduct[key] = (
                    existing.Qty + row.ExitQuantity,
                    existing.TotalCost + (row.ExitQuantity * row.UnitCost));
            }
        }

        Accumulate(saleExitCosts);
        Accumulate(deliveryExitCosts);

        var unitCostByReferenceProduct = new Dictionary<(string Reference, Guid ProductId), decimal>();
        foreach (var kv in qtyAndTotalCostByReferenceProduct)
        {
            unitCostByReferenceProduct[kv.Key] = kv.Value.Qty > 0 ? kv.Value.TotalCost / kv.Value.Qty : 0m;
        }

        return unitCostByReferenceProduct;
    }
}