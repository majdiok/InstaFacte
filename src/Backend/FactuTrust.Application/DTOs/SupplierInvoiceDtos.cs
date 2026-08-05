using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.DTOs;

/// <summary>
/// DTO for supplier invoice list.
/// </summary>
public sealed record SupplierInvoiceListDto
{
    public Guid Id { get; init; }
    public string InvoiceNumber { get; init; } = null!;
    public DateTime InvoiceDate { get; init; }
    public DateTime DueDate { get; init; }
    public string SupplierName { get; init; } = null!;
    public Guid SupplierId { get; init; }
    public string? PurchaseOrderNumber { get; init; }
    public Guid? PurchaseOrderId { get; init; }
    public SupplierInvoiceStatus Status { get; init; }
    public string StatusDisplay { get; init; } = null!;
    public string StatusCss { get; init; } = null!;
    public decimal TotalHT { get; init; }
    public decimal TotalTTC { get; init; }
    public int LineCount { get; init; }
    public decimal TotalPaid { get; init; }
    public decimal RemainingAmount { get; init; }
    public DateTime? PaidAt { get; init; }
    public Guid? WarehouseId { get; init; }
    public string? WarehouseName { get; init; }
    public bool HasFixedAssetLines { get; init; }
}

/// <summary>
/// Aggregated totals for a supplier invoice list, computed over the ENTIRE filtered set
/// (not just the current page) so the UI "totals zone" reflects the active filters.
/// </summary>
public sealed record SupplierInvoiceListSummaryDto
{
    public int Count { get; init; }
    public decimal TotalTtc { get; init; }
    public decimal TotalHt { get; init; }
    public decimal TotalVat { get; init; }
    /// <summary>Sum of supplier payments across the filtered invoices.</summary>
    public decimal TotalPaid { get; init; }
    /// <summary>Sum of per-invoice remaining = TTC - paid (matches the list, not clamped).</summary>
    public decimal TotalRemaining { get; init; }
    /// <summary>Number of filtered invoices overdue (due date passed, not paid/cancelled).</summary>
    public int OverdueCount { get; init; }
    public string Currency { get; init; } = "TND";
}

/// <summary>
/// DTO for supplier invoice details with lines.
/// </summary>
public sealed record SupplierInvoiceDetailDto
{
    public Guid Id { get; init; }
    public string InvoiceNumber { get; init; } = null!;
    public DateTime InvoiceDate { get; init; }
    public DateTime DueDate { get; init; }
    public SupplierInvoiceStatus Status { get; init; }
    public string StatusDisplay { get; init; } = null!;
    public string StatusCss { get; init; } = null!;
    public string? ExternalReference { get; init; }
    public string? Notes { get; init; }

    public SupplierSummaryDto Supplier { get; init; } = null!;
    public Guid? PurchaseOrderId { get; init; }
    public string? PurchaseOrderNumber { get; init; }
    public Guid? SourcePurchaseReceiptId { get; init; }
    public string? SourcePurchaseReceiptNumber { get; init; }

    public IReadOnlyList<SupplierInvoiceLineDto> Lines { get; init; } = Array.Empty<SupplierInvoiceLineDto>();

    public decimal SubTotal { get; init; }
    public decimal TotalVat { get; init; }
    public decimal TotalTTC { get; init; }

    public decimal TotalPaid { get; init; }
    public decimal RemainingAmount { get; init; }
    public IReadOnlyList<SupplierPaymentDto> Payments { get; init; } = Array.Empty<SupplierPaymentDto>();

    public DateTime? PaidAt { get; init; }
    public string? PaymentReference { get; init; }
    /// <summary>Mode de paiement prévu (informatif), ex. « Effet de commerce ».</summary>
    public string? PaymentMethod { get; init; }
    public DateTime? CancelledAt { get; init; }
    public string? CancellationReason { get; init; }

    public Guid? WarehouseId { get; init; }
    public string? WarehouseName { get; init; }

    public DateTime CreatedAt { get; init; }

    /// <summary>Linked withholding (RS) certificate when generated from this invoice.</summary>
    /// <summary>Prévisualisation RS (fournisseur assujetti) — recalculée à la génération du certificat si CNPC/prise en charge.</summary>
    public bool IsSubjectToWithholding { get; init; }
    public decimal? WithholdingRate { get; init; }
    public decimal? WithholdingAmount { get; init; }
    public decimal? NetAmountAfterWithholding { get; init; }
}

/// <summary>
/// DTO for a supplier invoice line.
/// </summary>
public sealed record SupplierInvoiceLineDto
{
    public Guid Id { get; init; }
    public int LineNumber { get; init; }
    public Guid ProductId { get; init; }
    public string ProductCode { get; init; } = null!;
    public string ProductName { get; init; } = null!;
    public string? ProductDescription { get; init; }
    public decimal Quantity { get; init; }
    public string? Unit { get; init; }
    public decimal UnitPriceHT { get; init; }
    public string VatRateDisplay { get; init; } = null!;
    public decimal SubTotal { get; init; }
    public decimal VatAmount { get; init; }
    public decimal Total { get; init; }
    public bool IsFixedAsset { get; init; }
    public string? AssetAccountNumber { get; init; }
    public Guid? DepreciationRateCategoryId { get; init; }
    public Guid? FixedAssetId { get; init; }
    public string? FixedAssetInventoryNumber { get; init; }
}

/// <summary>
/// Request body for recording a payment on a supplier invoice.
/// </summary>
public sealed record RecordSupplierPaymentRequest
{
    public DateTime PaymentDate { get; init; }
    public decimal? Amount { get; init; }
    public PaymentMethod? Method { get; init; }
    public string? Reference { get; init; }
    public string? Notes { get; init; }
    /// <summary>Échéance de la traite. Obligatoire lorsque <see cref="Method"/> == Traite.</summary>
    public DateTime? EffetDueDate { get; init; }
}

/// <summary>
/// Requête de paiement d'un effet fournisseur à échéance (action « Payer l'effet »).
/// </summary>
public sealed record SettleSupplierEffetRequest
{
    public DateTime SettlementDate { get; init; }
}

/// <summary>
/// Data returned after recording a supplier invoice payment, for audit purposes.
/// </summary>
public sealed record RecordPaymentAuditData(
    Guid InvoiceId,
    Guid PaymentId,
    string InvoiceNumber,
    decimal Amount,
    DateTime PaymentDate,
    decimal NewTotalPaid,
    bool IsInvoiceNowFullyPaid);

/// <summary>
/// DTO for a supplier payment record.
/// </summary>
public sealed record SupplierPaymentDto
{
    public Guid Id { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = null!;
    public DateTime PaymentDate { get; init; }
    public int Method { get; init; }
    public string MethodDisplay { get; init; } = null!;
    public string? Reference { get; init; }
    public string? Notes { get; init; }
    public DateTime CreatedAt { get; init; }
    /// <summary>Échéance de la traite (effet de commerce), si ce paiement en est une.</summary>
    public DateTime? EffetDueDate { get; init; }
    /// <summary>Statut de l'effet (0 En portefeuille, 1 Encaissé/payé, 2 Impayé) ; null si non-traite.</summary>
    public int? EffetStatus { get; init; }
    public string? EffetStatusDisplay { get; init; }
}

/// <summary>
/// Prefill data for creating a supplier invoice from a PO or BR.
/// </summary>
public sealed record SupplierInvoicePrefillDto
{
    public Guid? PurchaseOrderId { get; init; }
    public string? PurchaseOrderNumber { get; init; }
    public Guid? PurchaseReceiptId { get; init; }
    public string? PurchaseReceiptNumber { get; init; }
    public Guid SupplierId { get; init; }
    public string SupplierName { get; init; } = null!;
    public int PaymentTermDays { get; init; }
    public Guid? WarehouseId { get; init; }
    public IReadOnlyList<SupplierInvoicePrefillLineDto> Lines { get; init; } = Array.Empty<SupplierInvoicePrefillLineDto>();
    public decimal SubTotalHT { get; init; }
    public decimal TotalVat { get; init; }
    public decimal TotalTTC { get; init; }
    public string Currency { get; init; } = "TND";
    /// <summary>Next available internal number (preview; does not consume the sequence).</summary>
    public string? SuggestedInvoiceNumber { get; init; }
}

public sealed record SupplierInvoicePrefillLineDto
{
    public Guid SourceLineId { get; init; }
    public Guid? PurchaseOrderLineId { get; init; }
    public Guid? PurchaseReceiptLineId { get; init; }
    public int LineNumber { get; init; }
    public Guid ProductId { get; init; }
    public string ProductCode { get; init; } = null!;
    public string ProductName { get; init; } = null!;
    public string? Unit { get; init; }
    public decimal ReceivedQuantity { get; init; }
    public decimal InvoicedQuantity { get; init; }
    public decimal QuantityToInvoice { get; init; }
    public decimal MaxQuantityToInvoice { get; init; }
    public decimal UnitPriceHT { get; init; }
    public string VatRateDisplay { get; init; } = null!;
    public decimal SubTotalHT { get; init; }
}

/// <summary>
/// Summary of a supplier invoice linked to a purchase document.
/// </summary>
public sealed record LinkedSupplierInvoiceSummaryDto
{
    public Guid Id { get; init; }
    public string InvoiceNumber { get; init; } = null!;
    public DateTime InvoiceDate { get; init; }
    public SupplierInvoiceStatus Status { get; init; }
    public string StatusDisplay { get; init; } = null!;
    public decimal TotalTTC { get; init; }
}
