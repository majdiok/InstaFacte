using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.PurchaseReceipts.Queries;

public sealed record GetSupplierInvoicePrefillFromPurchaseReceiptQuery(Guid PurchaseReceiptId)
    : IRequest<Result<SupplierInvoicePrefillDto>>;

public sealed class GetSupplierInvoicePrefillFromPurchaseReceiptQueryHandler
    : IRequestHandler<GetSupplierInvoicePrefillFromPurchaseReceiptQuery, Result<SupplierInvoicePrefillDto>>
{
    private readonly IPurchaseReceiptRepository _purchaseReceiptRepository;
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly ISupplierInvoiceNumberService _supplierInvoiceNumberService;

    public GetSupplierInvoicePrefillFromPurchaseReceiptQueryHandler(
        IPurchaseReceiptRepository purchaseReceiptRepository,
        IPurchaseOrderRepository purchaseOrderRepository,
        ISupplierInvoiceNumberService supplierInvoiceNumberService)
    {
        _purchaseReceiptRepository = purchaseReceiptRepository;
        _purchaseOrderRepository = purchaseOrderRepository;
        _supplierInvoiceNumberService = supplierInvoiceNumberService;
    }

    public async Task<Result<SupplierInvoicePrefillDto>> Handle(
        GetSupplierInvoicePrefillFromPurchaseReceiptQuery request,
        CancellationToken cancellationToken)
    {
        var receipt = await _purchaseReceiptRepository.GetByIdWithLinesAsync(request.PurchaseReceiptId, cancellationToken);
        if (receipt is null)
            return Result.Failure<SupplierInvoicePrefillDto>(Error.NotFound("PurchaseReceipt", request.PurchaseReceiptId));

        if (!receipt.Status.CanBeInvoiced())
            return Result.Failure<SupplierInvoicePrefillDto>(Error.Validation("Status",
                "Ce bon de réception ne peut pas être facturé dans son état actuel"));

        PurchaseOrder? po = null;
        if (receipt.PurchaseOrderId is { } poId)
            po = await _purchaseOrderRepository.GetByIdWithLinesAsync(poId, cancellationToken);

        var lines = receipt.Lines
            .Where(l => l.ReceivedNotInvoicedQuantity > 0)
            .OrderBy(l => l.LineNumber)
            .Select(l =>
            {
                var gross = l.UnitPrice.Multiply(l.ReceivedNotInvoicedQuantity);
                var discountFactor = 1m - ((l.DiscountPercent ?? 0m) / 100m);
                var subTotal = gross.Multiply(discountFactor).Amount;
                return new SupplierInvoicePrefillLineDto
                {
                    SourceLineId = l.Id,
                    PurchaseReceiptLineId = l.Id,
                    PurchaseOrderLineId = l.PurchaseOrderLineId,
                    LineNumber = l.LineNumber,
                    ProductId = l.ProductId,
                    ProductCode = l.ProductCode,
                    ProductName = l.ProductName,
                    Unit = l.Unit,
                    ReceivedQuantity = l.ReceivedQuantity,
                    InvoicedQuantity = l.InvoicedQuantity,
                    QuantityToInvoice = l.ReceivedNotInvoicedQuantity,
                    MaxQuantityToInvoice = l.ReceivedNotInvoicedQuantity,
                    UnitPriceHT = l.UnitPrice.Amount,
                    VatRateDisplay = l.VatRate.ToDisplayString(),
                    SubTotalHT = subTotal
                };
            })
            .ToList();

        if (lines.Count == 0)
            return Result.Failure<SupplierInvoicePrefillDto>(Error.Validation("Lines",
                "Aucune quantité reçue non facturée"));

        var subTotalHt = lines.Sum(l => l.SubTotalHT);
        var totalVat = receipt.Lines
            .Where(l => l.ReceivedNotInvoicedQuantity > 0)
            .Sum(l =>
            {
                var gross = l.UnitPrice.Multiply(l.ReceivedNotInvoicedQuantity);
                var discountFactor = 1m - ((l.DiscountPercent ?? 0m) / 100m);
                return gross.Multiply(discountFactor).ApplyPercentage(l.VatRate.ToDecimal()).Amount;
            });

        var suggestedNumber = await _supplierInvoiceNumberService.PreviewNextAsync(
            receipt.ReceiptDate,
            cancellationToken);

        return Result.Success(new SupplierInvoicePrefillDto
        {
            PurchaseOrderId = receipt.PurchaseOrderId,
            PurchaseOrderNumber = po?.Number.Value,
            PurchaseReceiptId = receipt.Id,
            PurchaseReceiptNumber = receipt.Number.Value,
            SupplierId = receipt.SupplierId,
            SupplierName = receipt.Supplier.Name,
            PaymentTermDays = receipt.Supplier.PaymentTermDays,
            WarehouseId = receipt.WarehouseId,
            Lines = lines,
            SubTotalHT = subTotalHt,
            TotalVat = totalVat,
            TotalTTC = subTotalHt + totalVat,
            Currency = "TND",
            SuggestedInvoiceNumber = suggestedNumber
        });
    }
}
