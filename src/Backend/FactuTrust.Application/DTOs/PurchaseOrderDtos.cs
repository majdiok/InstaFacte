using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.DTOs;

/// <summary>
/// DTO for purchase order list.
/// </summary>
public sealed record PurchaseOrderListDto
{
    public Guid Id { get; init; }
    public string Number { get; init; } = null!;
    public string SupplierName { get; init; } = null!;
    public Guid SupplierId { get; init; }
    public DateTime OrderDate { get; init; }
    public DateTime? ExpectedDeliveryDate { get; init; }
    public string? Reference { get; init; }
    public PurchaseOrderStatus Status { get; init; }
    public string StatusDisplay { get; init; } = null!;
    public string StatusCss { get; init; } = null!;
    public decimal TotalHT { get; init; }
    public decimal TotalTTC { get; init; }
    public int LineCount { get; init; }
    public Guid? WarehouseId { get; init; }
    public string? WarehouseName { get; init; }
}

/// <summary>
/// Aggregated totals for a purchase order list, computed over the ENTIRE filtered set
/// (not just the current page) so the UI "totals zone" reflects the active filters.
/// </summary>
public sealed record PurchaseOrderListSummaryDto
{
    public int Count { get; init; }
    public decimal TotalTtc { get; init; }
    public decimal TotalHt { get; init; }
    public decimal TotalVat { get; init; }
    /// <summary>Fully received orders.</summary>
    public int ReceivedCount { get; init; }
    /// <summary>Orders awaiting receipt (Confirmed or PartiallyReceived).</summary>
    public int PendingCount { get; init; }
    public string Currency { get; init; } = "TND";
}

/// <summary>
/// DTO for purchase order details with lines.
/// </summary>
public sealed record PurchaseOrderDetailDto
{
    public Guid Id { get; init; }
    public string Number { get; init; } = null!;
    public DateTime OrderDate { get; init; }
    public DateTime? ExpectedDeliveryDate { get; init; }
    public PurchaseOrderStatus Status { get; init; }
    public string StatusDisplay { get; init; } = null!;
    public string StatusCss { get; init; } = null!;
    public string? Reference { get; init; }
    public string? Notes { get; init; }

    public SupplierSummaryDto Supplier { get; init; } = null!;

    public IReadOnlyList<PurchaseOrderLineDto> Lines { get; init; } = Array.Empty<PurchaseOrderLineDto>();

    public decimal SubTotal { get; init; }
    public decimal TotalVat { get; init; }
    public decimal TotalTTC { get; init; }

    public DateTime? ConfirmedAt { get; init; }
    public DateTime? ReceivedAt { get; init; }
    public DateTime? InvoicedAt { get; init; }
    public DateTime? CancelledAt { get; init; }
    public string? CancellationReason { get; init; }

    public decimal TotalReceivedNotInvoicedQuantity { get; init; }
    public bool HasReceivedNotInvoiced { get; init; }

    public IReadOnlyList<LinkedSupplierInvoiceSummaryDto> LinkedSupplierInvoices { get; init; }
        = Array.Empty<LinkedSupplierInvoiceSummaryDto>();

    public Guid? WarehouseId { get; init; }
    public string? WarehouseName { get; init; }

    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// DTO for a purchase order line.
/// </summary>
public sealed record PurchaseOrderLineDto
{
    public Guid Id { get; init; }
    public int LineNumber { get; init; }
    public Guid ProductId { get; init; }
    public string ProductCode { get; init; } = null!;
    public string ProductName { get; init; } = null!;
    public string? ProductDescription { get; init; }
    public decimal Quantity { get; init; }
    public decimal ReceivedQuantity { get; init; }
    public decimal InvoicedQuantity { get; init; }
    public decimal ReceivedNotInvoicedQuantity { get; init; }
    public decimal PendingQuantity { get; init; }
    public bool IsFullyReceived { get; init; }
    public string? Unit { get; init; }
    public decimal UnitPriceHT { get; init; }
    public string VatRateDisplay { get; init; } = null!;
    public decimal SubTotal { get; init; }
    public decimal VatAmount { get; init; }
    public decimal Total { get; init; }
}

/// <summary>
/// DTO for creating a purchase order.
/// </summary>
public sealed record CreatePurchaseOrderDto
{
    public Guid SupplierId { get; init; }
    public DateTime OrderDate { get; init; }
    public DateTime? ExpectedDeliveryDate { get; init; }
    public string? Reference { get; init; }
    public string? Notes { get; init; }
    public Guid? WarehouseId { get; init; }
    public IReadOnlyList<CreatePurchaseOrderLineDto> Lines { get; init; } = Array.Empty<CreatePurchaseOrderLineDto>();
}

/// <summary>
/// DTO for a purchase order line in creation.
/// </summary>
public sealed record CreatePurchaseOrderLineDto
{
    public Guid ProductId { get; init; }
    public decimal Quantity { get; init; }
    public decimal? UnitPriceHT { get; init; }
}

/// <summary>
/// DTO for goods reception.
/// </summary>
public sealed record ReceiveGoodsDto
{
    public Guid? WarehouseId { get; init; }
    public IReadOnlyList<ReceiveGoodsLineDto> Lines { get; init; } = Array.Empty<ReceiveGoodsLineDto>();
}

/// <summary>
/// DTO for a line in goods reception.
/// </summary>
public sealed record ReceiveGoodsLineDto
{
    public Guid LineId { get; init; }
    public decimal ReceivedQuantity { get; init; }
}

/// <summary>
/// DTO for updating a draft purchase order.
/// </summary>
public sealed record UpdatePurchaseOrderDto
{
    public DateTime? ExpectedDeliveryDate { get; init; }
    public string? Reference { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<UpdatePurchaseOrderLineDto>? Lines { get; init; }
}

/// <summary>
/// DTO for a line in purchase order update.
/// If Id is null, a new line is added. If present, the existing line is updated.
/// </summary>
public sealed record UpdatePurchaseOrderLineDto
{
    public Guid? Id { get; init; }
    public Guid ProductId { get; init; }
    public decimal Quantity { get; init; }
    public decimal? UnitPriceHT { get; init; }
}
