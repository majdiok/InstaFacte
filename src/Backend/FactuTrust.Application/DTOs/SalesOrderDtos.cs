using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.DTOs;

/// <summary>Ligne de la liste des commandes clients.</summary>
public sealed record SalesOrderListDto
{
    public Guid Id { get; init; }
    public string Number { get; init; } = null!;
    public DateTime OrderDate { get; init; }
    public DateTime? ExpectedDeliveryDate { get; init; }
    public SalesOrderStatus Status { get; init; }
    public string StatusDisplay { get; init; } = null!;
    public string StatusCssClass { get; init; } = null!;
    public Guid ClientId { get; init; }
    public string ClientName { get; init; } = null!;
    public string? Reference { get; init; }
    public decimal TotalAmount { get; init; }
    public string Currency { get; init; } = null!;
    public int LineCount { get; init; }

    /// <summary>Reste à livrer, toutes lignes confondues.</summary>
    public decimal PendingDeliveryQuantity { get; init; }

    /// <summary>Valeur HT du reste à livrer — la part de cette commande dans le carnet.</summary>
    public decimal BacklogAmountHt { get; init; }

    /// <summary>Vrai tant que la commande pèse sur le carnet de commandes.</summary>
    public bool IsOpen { get; init; }

    public bool IsStockReserved { get; init; }
    public Guid? SourceQuoteId { get; init; }
}

/// <summary>
/// Totaux agrégés de la liste, calculés sur l'ENSEMBLE du jeu filtré et non sur la page
/// courante — même convention que les autres listes documentaires.
/// </summary>
public sealed record SalesOrderListSummaryDto
{
    public int Count { get; init; }
    public decimal TotalHt { get; init; }
    public decimal TotalVat { get; init; }
    public decimal TotalTtc { get; init; }

    /// <summary>Valeur HT totale restant à livrer sur le jeu filtré.</summary>
    public decimal BacklogAmountHt { get; init; }

    public int OpenCount { get; init; }
    public int PartiallyDeliveredCount { get; init; }
    public int CompletedCount { get; init; }
    public string Currency { get; init; } = "TND";
}

/// <summary>Détail d'une commande client.</summary>
public sealed record SalesOrderDetailDto
{
    public Guid Id { get; init; }
    public string Number { get; init; } = null!;
    public DateTime OrderDate { get; init; }
    public DateTime? ExpectedDeliveryDate { get; init; }
    public SalesOrderStatus Status { get; init; }
    public string StatusDisplay { get; init; } = null!;

    public Guid ClientId { get; init; }
    public ClientSummaryDto Client { get; init; } = null!;

    public string? Reference { get; init; }
    public string? Notes { get; init; }
    public string? PaymentTerms { get; init; }

    public Guid? WarehouseId { get; init; }
    public string? WarehouseName { get; init; }
    public Guid? SourceQuoteId { get; init; }
    public string? SourceQuoteNumber { get; init; }

    public IReadOnlyList<SalesOrderLineDto> Lines { get; init; } = Array.Empty<SalesOrderLineDto>();

    public decimal SubTotal { get; init; }
    public decimal FodecAmount { get; init; }
    public decimal TotalVat { get; init; }
    public decimal FiscalStampAmount { get; init; }
    public decimal TotalAmount { get; init; }
    public string Currency { get; init; } = null!;

    public IReadOnlyList<VatBreakdownDto> VatBreakdown { get; init; } = Array.Empty<VatBreakdownDto>();

    public bool IsFullyDelivered { get; init; }
    public bool IsFullyInvoiced { get; init; }
    public decimal TotalPendingDeliveryQuantity { get; init; }
    public decimal TotalPendingInvoiceQuantity { get; init; }
    public decimal BacklogAmountHt { get; init; }
    public bool IsStockReserved { get; init; }

    public DateTime? ConfirmedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime? CancelledAt { get; init; }
    public string? CancellationReason { get; init; }
    public DateTime? ClosedAt { get; init; }
    public string? ClosureReason { get; init; }

    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>
/// Ligne de commande. Expose les TROIS quantités et les reliquats qui en découlent —
/// c'est ce que ni le devis, ni le BL, ni la facture ne savent porter.
/// </summary>
public sealed record SalesOrderLineDto
{
    public Guid Id { get; init; }
    public int LineNumber { get; init; }
    public Guid ProductId { get; init; }
    public string ProductCode { get; init; } = null!;
    public string ProductName { get; init; } = null!;
    public string? ProductDescription { get; init; }
    public string? Unit { get; init; }

    public decimal Quantity { get; init; }
    public decimal DeliveredQuantity { get; init; }
    public decimal InvoicedQuantity { get; init; }
    public decimal PendingDeliveryQuantity { get; init; }
    public decimal PendingInvoiceQuantity { get; init; }

    /// <summary>Livré mais pas encore facturé — assiette de la facturation périodique.</summary>
    public decimal DeliveredNotInvoicedQuantity { get; init; }

    public decimal UnitPrice { get; init; }
    public int VatRatePercent { get; init; }
    public decimal? DiscountPercent { get; init; }
    public decimal DiscountAmount { get; init; }
    public bool IsFodecApplicable { get; init; }
    public decimal FodecRatePercent { get; init; }
    public decimal FodecAmount { get; init; }
    public decimal SubTotal { get; init; }
    public decimal VatAmount { get; init; }
    public decimal Total { get; init; }

    public bool IsFullyDelivered { get; init; }
    public bool IsFullyInvoiced { get; init; }
    public string? Notes { get; init; }
}

/// <summary>Une ligne du carnet de commandes : ce qui reste à livrer, et pour quelle valeur.</summary>
public sealed record SalesOrderBacklogRowDto
{
    public Guid SalesOrderId { get; init; }
    public string OrderNumber { get; init; } = null!;
    public DateTime OrderDate { get; init; }
    public DateTime? ExpectedDeliveryDate { get; init; }
    public Guid ClientId { get; init; }
    public string ClientName { get; init; } = null!;
    public Guid ProductId { get; init; }
    public string ProductCode { get; init; } = null!;
    public string ProductName { get; init; } = null!;
    public decimal OrderedQuantity { get; init; }
    public decimal DeliveredQuantity { get; init; }
    public decimal PendingQuantity { get; init; }
    public decimal PendingAmountHt { get; init; }

    /// <summary>Vrai quand la date de livraison prévue est dépassée et qu'il reste à livrer.</summary>
    public bool IsLate { get; init; }
}

// ─────────────────────────── DTO d'écriture ───────────────────────────

public sealed record CreateSalesOrderDto
{
    public Guid ClientId { get; init; }
    public DateTime OrderDate { get; init; }
    public DateTime? ExpectedDeliveryDate { get; init; }
    public string? Reference { get; init; }
    public string? Notes { get; init; }
    public string? PaymentTerms { get; init; }
    public Guid? WarehouseId { get; init; }
    public Guid? SourceQuoteId { get; init; }
    public IReadOnlyList<CreateSalesOrderLineDto> Lines { get; init; } = Array.Empty<CreateSalesOrderLineDto>();
}

public sealed record CreateSalesOrderLineDto
{
    public Guid ProductId { get; init; }
    public decimal Quantity { get; init; }
    /// <summary>Prix négocié. 0 ou absent = prix catalogue.</summary>
    public decimal UnitPrice { get; init; }
    public decimal? DiscountPercent { get; init; }
    public string? Notes { get; init; }
}

public sealed record UpdateSalesOrderDto
{
    public DateTime? ExpectedDeliveryDate { get; init; }
    public string? Reference { get; init; }
    public string? Notes { get; init; }
    public string? PaymentTerms { get; init; }
}

public sealed record CancelSalesOrderDto
{
    public string Reason { get; init; } = null!;
}

public sealed record CloseSalesOrderDto
{
    public string Reason { get; init; } = null!;
}
