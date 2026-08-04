using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.PurchaseOrders.Queries;

public sealed record GetSupplierInvoicePrefillFromPOQuery(Guid PurchaseOrderId)
    : IRequest<Result<SupplierInvoicePrefillDto>>;

public sealed class GetSupplierInvoicePrefillFromPOQueryHandler
    : IRequestHandler<GetSupplierInvoicePrefillFromPOQuery, Result<SupplierInvoicePrefillDto>>
{
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly ISupplierInvoiceNumberService _supplierInvoiceNumberService;

    public GetSupplierInvoicePrefillFromPOQueryHandler(
        IPurchaseOrderRepository purchaseOrderRepository,
        ISupplierInvoiceNumberService supplierInvoiceNumberService)
    {
        _purchaseOrderRepository = purchaseOrderRepository;
        _supplierInvoiceNumberService = supplierInvoiceNumberService;
    }

    public async Task<Result<SupplierInvoicePrefillDto>> Handle(
        GetSupplierInvoicePrefillFromPOQuery request,
        CancellationToken cancellationToken)
    {
        var po = await _purchaseOrderRepository.GetByIdWithLinesAsync(request.PurchaseOrderId, cancellationToken);
        if (po is null)
            return Result.Failure<SupplierInvoicePrefillDto>(Error.NotFound("PurchaseOrder", request.PurchaseOrderId));

        if (!po.Status.CanBeInvoiced())
            return Result.Failure<SupplierInvoicePrefillDto>(Error.Validation("Status",
                "Ce bon de commande ne peut pas être facturé dans son état actuel"));

        var lines = po.Lines
            .Where(l => l.ReceivedNotInvoicedQuantity > 0)
            .OrderBy(l => l.LineNumber)
            .Select(l => new SupplierInvoicePrefillLineDto
            {
                SourceLineId = l.Id,
                PurchaseOrderLineId = l.Id,
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
                SubTotalHT = l.UnitPrice.Multiply(l.ReceivedNotInvoicedQuantity).Amount
            })
            .ToList();

        if (lines.Count == 0)
            return Result.Failure<SupplierInvoicePrefillDto>(Error.Validation("Lines",
                "Aucune quantité reçue non facturée"));

        var subTotal = lines.Sum(l => l.SubTotalHT);
        var vat = po.Lines
            .Where(l => l.ReceivedNotInvoicedQuantity > 0)
            .Sum(l => l.UnitPrice.Multiply(l.ReceivedNotInvoicedQuantity).ApplyPercentage(l.VatRate.ToDecimal()).Amount);

        var suggestedNumber = await _supplierInvoiceNumberService.PreviewNextAsync(
            po.OrderDate,
            cancellationToken);

        return Result.Success(new SupplierInvoicePrefillDto
        {
            PurchaseOrderId = po.Id,
            PurchaseOrderNumber = po.Number.Value,
            SupplierId = po.SupplierId,
            SupplierName = po.Supplier.Name,
            PaymentTermDays = po.Supplier.PaymentTermDays,
            WarehouseId = po.WarehouseId,
            Lines = lines,
            SubTotalHT = subTotal,
            TotalVat = vat,
            TotalTTC = subTotal + vat,
            Currency = "TND",
            SuggestedInvoiceNumber = suggestedNumber
        });
    }
}
