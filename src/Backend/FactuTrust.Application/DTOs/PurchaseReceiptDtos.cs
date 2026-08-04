using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.DTOs;

/// <summary>
/// DTO for purchase receipt list.
/// </summary>
public sealed record PurchaseReceiptListDto
{
    public Guid Id { get; init; }
    public string Number { get; init; } = null!;
    public string SupplierName { get; init; } = null!;
    public Guid SupplierId { get; init; }
    public DateTime ReceiptDate { get; init; }
    public string? SupplierReference { get; init; }
    public PurchaseReceiptStatus Status { get; init; }
    public string StatusDisplay { get; init; } = null!;
    public string StatusCss { get; init; } = null!;
    public decimal TotalHT { get; init; }
    public decimal TotalTTC { get; init; }
    public int LineCount { get; init; }
    public Guid WarehouseId { get; init; }
    public string? WarehouseName { get; init; }
    public Guid? PurchaseOrderId { get; init; }
    public string? PurchaseOrderNumber { get; init; }
    public bool IsPartialRelativeToOrdered { get; init; }
}

/// <summary>
/// Aggregated totals for a purchase receipt list, computed over the ENTIRE filtered set
/// (not just the current page) so the UI "totals zone" reflects the active filters.
/// </summary>
public sealed record PurchaseReceiptListSummaryDto
{
    public int Count { get; init; }
    public decimal TotalTtc { get; init; }
    public decimal TotalHt { get; init; }
    public decimal TotalVat { get; init; }
    /// <summary>Validated receipts.</summary>
    public int ValidatedCount { get; init; }
    /// <summary>Draft receipts.</summary>
    public int DraftCount { get; init; }
    public string Currency { get; init; } = "TND";
}

/// <summary>
/// DTO for purchase receipt details with lines and attachments.
/// </summary>
public sealed record PurchaseReceiptDetailDto
{
    public Guid Id { get; init; }
    public string Number { get; init; } = null!;
    public DateTime ReceiptDate { get; init; }
    public PurchaseReceiptStatus Status { get; init; }
    public string StatusDisplay { get; init; } = null!;
    public string StatusCss { get; init; } = null!;
    public string? SupplierReference { get; init; }
    public string? TransporterName { get; init; }
    public string? DeliveryNoteNumber { get; init; }
    public string? Notes { get; init; }

    public SupplierSummaryDto Supplier { get; init; } = null!;

    public Guid WarehouseId { get; init; }
    public string? WarehouseName { get; init; }

    public Guid? PurchaseOrderId { get; init; }
    public string? PurchaseOrderNumber { get; init; }

    public IReadOnlyList<PurchaseReceiptLineDto> Lines { get; init; } = Array.Empty<PurchaseReceiptLineDto>();
    public IReadOnlyList<PurchaseReceiptAttachmentDto> Attachments { get; init; } = Array.Empty<PurchaseReceiptAttachmentDto>();

    public decimal SubTotal { get; init; }
    public decimal TotalVat { get; init; }
    public decimal TotalTTC { get; init; }

    public bool IsPartialRelativeToOrdered { get; init; }

    public DateTime? ValidatedAt { get; init; }
    public DateTime? CancelledAt { get; init; }
    public string? CancellationReason { get; init; }

    public decimal TotalReceivedNotInvoicedQuantity { get; init; }
    public bool HasReceivedNotInvoiced { get; init; }
    public IReadOnlyList<LinkedSupplierInvoiceSummaryDto> LinkedSupplierInvoices { get; init; } =
        Array.Empty<LinkedSupplierInvoiceSummaryDto>();

    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// DTO for a purchase receipt line.
/// </summary>
public sealed record PurchaseReceiptLineDto
{
    public Guid Id { get; init; }
    public int LineNumber { get; init; }
    public Guid? PurchaseOrderLineId { get; init; }
    public Guid ProductId { get; init; }
    public string ProductCode { get; init; } = null!;
    public string ProductName { get; init; } = null!;
    public string? ProductDescription { get; init; }
    public decimal OrderedQuantity { get; init; }
    public decimal ReceivedQuantity { get; init; }
    public decimal InvoicedQuantity { get; init; }
    public decimal ReceivedNotInvoicedQuantity { get; init; }
    public string? Unit { get; init; }
    public decimal UnitPriceHT { get; init; }
    public decimal? DiscountPercent { get; init; }
    public string VatRateDisplay { get; init; } = null!;
    public decimal SubTotal { get; init; }
    public decimal VatAmount { get; init; }
    public decimal Total { get; init; }
}

/// <summary>
/// DTO for a purchase receipt attachment.
/// </summary>
public sealed record PurchaseReceiptAttachmentDto
{
    public Guid Id { get; init; }
    public string FileName { get; init; } = null!;
    public string ContentType { get; init; } = null!;
    public long SizeBytes { get; init; }
    public DateTime UploadedAt { get; init; }
    public string? UploadedBy { get; init; }
}

/// <summary>
/// DTO for creating a purchase receipt.
/// </summary>
public sealed record CreatePurchaseReceiptDto
{
    public Guid SupplierId { get; init; }
    public Guid WarehouseId { get; init; }
    public DateTime ReceiptDate { get; init; }
    public Guid? PurchaseOrderId { get; init; }
    public string? SupplierReference { get; init; }
    public string? TransporterName { get; init; }
    public string? DeliveryNoteNumber { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<CreatePurchaseReceiptLineDto> Lines { get; init; } = Array.Empty<CreatePurchaseReceiptLineDto>();
}

/// <summary>
/// DTO for a purchase receipt line in creation.
/// </summary>
public sealed record CreatePurchaseReceiptLineDto
{
    public Guid ProductId { get; init; }
    public decimal ReceivedQuantity { get; init; }
    public decimal? UnitPriceHT { get; init; }
    public decimal OrderedQuantity { get; init; }
    public Guid? PurchaseOrderLineId { get; init; }
    public decimal? DiscountPercent { get; init; }
}

/// <summary>
/// DTO for updating a draft purchase receipt.
/// </summary>
public sealed record UpdatePurchaseReceiptDto
{
    public DateTime ReceiptDate { get; init; }
    public Guid WarehouseId { get; init; }
    public string? SupplierReference { get; init; }
    public string? TransporterName { get; init; }
    public string? DeliveryNoteNumber { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<CreatePurchaseReceiptLineDto> Lines { get; init; } = Array.Empty<CreatePurchaseReceiptLineDto>();
}

/// <summary>
/// Prefill payload when creating a BR from a purchase order.
/// </summary>
public sealed record PurchaseReceiptPrefillDto
{
    public Guid PurchaseOrderId { get; init; }
    public string PurchaseOrderNumber { get; init; } = null!;
    public Guid SupplierId { get; init; }
    public string SupplierName { get; init; } = null!;
    public Guid? WarehouseId { get; init; }
    public string? WarehouseName { get; init; }
    public IReadOnlyList<PurchaseReceiptPrefillLineDto> Lines { get; init; } = Array.Empty<PurchaseReceiptPrefillLineDto>();
}

/// <summary>
/// Prefill line with pending quantity from the purchase order.
/// </summary>
public sealed record PurchaseReceiptPrefillLineDto
{
    public Guid PurchaseOrderLineId { get; init; }
    public Guid ProductId { get; init; }
    public string ProductCode { get; init; } = null!;
    public string ProductName { get; init; } = null!;
    public string? ProductDescription { get; init; }
    public string? Unit { get; init; }
    public decimal OrderedQuantity { get; init; }
    public decimal AlreadyReceivedQuantity { get; init; }
    public decimal PendingQuantity { get; init; }
    public decimal UnitPriceHT { get; init; }
}
