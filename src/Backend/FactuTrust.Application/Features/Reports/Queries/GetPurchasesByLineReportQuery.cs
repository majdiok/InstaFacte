using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Reports.Queries;

/// <summary>
/// Query to get purchases by line report (détails achats par ligne produit).
/// </summary>
public sealed record GetPurchasesByLineReportQuery(DateTime FromDate, DateTime ToDate)
    : IRequest<Result<IReadOnlyList<PurchasesByLineReportRowDto>>>;

/// <summary>
/// Handler for GetPurchasesByLineReportQuery.
/// </summary>
public sealed class GetPurchasesByLineReportQueryHandler
    : IRequestHandler<GetPurchasesByLineReportQuery, Result<IReadOnlyList<PurchasesByLineReportRowDto>>>
{
    private readonly ISupplierInvoiceRepository _invoiceRepository;

    public GetPurchasesByLineReportQueryHandler(ISupplierInvoiceRepository invoiceRepository)
    {
        _invoiceRepository = invoiceRepository;
    }

    public async Task<Result<IReadOnlyList<PurchasesByLineReportRowDto>>> Handle(
        GetPurchasesByLineReportQuery request,
        CancellationToken cancellationToken)
    {
        var from = request.FromDate.Date;
        var to = request.ToDate.Date;
        var (invoices, _) = await _invoiceRepository.SearchAsync(
            null, null, null, from, to, 1, 10_000, false, cancellationToken);

        var aggregated = new Dictionary<(Guid ProductId, string SupplierName), (string ProductName, string ProductCode, decimal Quantity, decimal AmountTTC, decimal VatAmount, string Currency)>();

        foreach (var inv in invoices)
        {
            var supplierName = inv.Supplier?.Name ?? "";
            foreach (var line in inv.Lines)
            {
                var key = (line.ProductId, supplierName);
                var currency = line.Total.Currency;
                if (aggregated.TryGetValue(key, out var existing))
                {
                    aggregated[key] = (
                        existing.ProductName,
                        existing.ProductCode,
                        existing.Quantity + line.Quantity,
                        existing.AmountTTC + line.Total.Amount,
                        existing.VatAmount + line.VatAmount.Amount,
                        currency
                    );
                }
                else
                {
                    aggregated[key] = (
                        line.ProductName,
                        line.ProductCode,
                        line.Quantity,
                        line.Total.Amount,
                        line.VatAmount.Amount,
                        currency
                    );
                }
            }
        }

        var rows = aggregated.Select(kv => new PurchasesByLineReportRowDto
        {
            ProductId = kv.Key.ProductId,
            ProductName = kv.Value.ProductName,
            ProductCode = kv.Value.ProductCode,
            SupplierName = kv.Key.SupplierName,
            Quantity = kv.Value.Quantity,
            AmountTTC = kv.Value.AmountTTC,
            VatAmount = kv.Value.VatAmount,
            Currency = kv.Value.Currency
        }).OrderByDescending(r => r.AmountTTC).ToList();

        return Result.Success<IReadOnlyList<PurchasesByLineReportRowDto>>(rows);
    }
}
