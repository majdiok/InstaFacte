using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.DTOs;

/// <summary>
/// DTO for quote list/summary.
/// </summary>
public sealed record QuoteListDto
{
    public Guid Id { get; init; }
    public string Number { get; init; } = null!;
    public DateTime IssueDate { get; init; }
    public DateTime ExpiryDate { get; init; }
    public string Status { get; init; } = null!;
    public string StatusCssClass { get; init; } = null!;
    public string ClientName { get; init; } = null!;
    public decimal TotalAmount { get; init; }
    public string Currency { get; init; } = null!;
    public bool IsExpired { get; init; }
    public bool IsConverted { get; init; }
    public Guid? ConvertedInvoiceId { get; init; }
    /// <summary>La commande client créée depuis ce devis, s'il a été transformé en commande.</summary>
    public Guid? ConvertedSalesOrderId { get; init; }
}

/// <summary>
/// Aggregated totals for a quote list, computed over the ENTIRE filtered set
/// (not just the current page) so the UI "totals zone" reflects the active filters.
/// </summary>
public sealed record QuoteListSummaryDto
{
    public int Count { get; init; }
    public decimal TotalTtc { get; init; }
    public decimal TotalHt { get; init; }
    public decimal TotalVat { get; init; }
    /// <summary>Number of accepted quotes in the filtered set.</summary>
    public int AcceptedCount { get; init; }
    /// <summary>Number of lapsed quotes (expiry passed, not expired/converted/cancelled).</summary>
    public int ExpiredCount { get; init; }
    public string Currency { get; init; } = "TND";
}

/// <summary>
/// DTO for quote details.
/// </summary>
public sealed record QuoteDetailDto
{
    public Guid Id { get; init; }
    public string Number { get; init; } = null!;
    public DateTime IssueDate { get; init; }
    public DateTime ExpiryDate { get; init; }
    public QuoteStatus Status { get; init; }
    public string StatusDisplay { get; init; } = null!;
    
    public Guid ClientId { get; init; }
    public ClientSummaryDto Client { get; init; } = null!;
    
    public string? Reference { get; init; }
    public string? Notes { get; init; }
    public string? TermsAndConditions { get; init; }
    
    public IReadOnlyList<QuoteLineDto> Lines { get; init; } = Array.Empty<QuoteLineDto>();
    
    public decimal SubTotal { get; init; }
    /// <summary>FODEC agrégé annoncé au devis — repris tel quel sur la facture.</summary>
    public decimal FodecAmount { get; init; }
    public decimal TotalVat { get; init; }
    /// <summary>Timbre fiscal annoncé au devis — repris tel quel sur la facture.</summary>
    public decimal FiscalStampAmount { get; init; }
    public decimal TotalAmount { get; init; }
    public string Currency { get; init; } = null!;

    public IReadOnlyList<VatBreakdownDto> VatBreakdown { get; init; } = Array.Empty<VatBreakdownDto>();
    
    public DateTime? SentAt { get; init; }
    public DateTime? AcceptedAt { get; init; }
    public DateTime? RejectedAt { get; init; }
    public string? RejectionReason { get; init; }
    public DateTime? CancelledAt { get; init; }
    public string? CancellationReason { get; init; }
    
    public Guid? ConvertedInvoiceId { get; init; }
    /// <summary>La commande client créée depuis ce devis, s'il a été transformé en commande.</summary>
    public Guid? ConvertedSalesOrderId { get; init; }
    public DateTime? ConvertedAt { get; init; }
    
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>
/// DTO for quote line.
/// </summary>
public sealed record QuoteLineDto
{
    public Guid Id { get; init; }
    public int LineNumber { get; init; }
    public Guid ProductId { get; init; }
    public string ProductCode { get; init; } = null!;
    public string ProductName { get; init; } = null!;
    public string? ProductDescription { get; init; }
    public decimal Quantity { get; init; }
    public string? Unit { get; init; }
    public decimal UnitPrice { get; init; }
    public int VatRatePercent { get; init; }
    public decimal? DiscountPercent { get; init; }
    public Guid? AppliedPromotionId { get; init; }
    public string? AppliedPromotionName { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal SubTotal { get; init; }
    public bool IsFodecApplicable { get; init; }
    public decimal FodecRatePercent { get; init; }
    public decimal FodecAmount { get; init; }
    public decimal VatAmount { get; init; }
    public decimal Total { get; init; }
}

/// <summary>
/// DTO for creating a quote.
/// </summary>
public sealed record CreateQuoteDto
{
    public Guid ClientId { get; init; }
    public DateTime IssueDate { get; init; }
    public DateTime ExpiryDate { get; init; }
    public string? Reference { get; init; }
    public string? Notes { get; init; }
    public string? TermsAndConditions { get; init; }
    public IReadOnlyList<CreateQuoteLineDto> Lines { get; init; } = Array.Empty<CreateQuoteLineDto>();
    /// <summary>When set, usage count is incremented on the template after successful quote creation.</summary>
    public Guid? QuoteTemplateId { get; init; }
}

/// <summary>
/// DTO for creating a quote line.
/// </summary>
public sealed record CreateQuoteLineDto
{
    public Guid? ProductId { get; init; }
    public string? Designation { get; init; }
    public string? Description { get; init; }
    public decimal Quantity { get; init; }
    public string? Unit { get; init; }
    public decimal UnitPrice { get; init; }
    public int VatRatePercent { get; init; }
    public decimal? DiscountPercent { get; init; }
}

/// <summary>
/// DTO for updating a quote.
/// </summary>
public sealed record UpdateQuoteDto
{
    public DateTime? ExpiryDate { get; init; }
    public string? Reference { get; init; }
    public string? Notes { get; init; }
    public string? TermsAndConditions { get; init; }
}

/// <summary>
/// DTO for converting a quote to an invoice.
/// </summary>
public sealed record ConvertQuoteToInvoiceDto
{
    public DateTime? IssueDate { get; init; }
    public DateTime? DueDate { get; init; }
    public string? Reference { get; init; }
    public string? Notes { get; init; }
    public string? PaymentTerms { get; init; }
    /// <summary>Optional warehouse for stock deduction when the invoice is validated.</summary>
    public Guid? WarehouseId { get; init; }
}

/// <summary>
/// Options de transformation d'un devis accepté en commande client — miroir de
/// <see cref="ConvertQuoteToInvoiceDto"/>. Tous les champs sont optionnels : par défaut,
/// la commande est datée du jour et sans date de livraison prévue.
/// </summary>
public sealed record ConvertQuoteToSalesOrderDto
{
    /// <summary>Date de la commande (défaut : aujourd'hui).</summary>
    public DateTime? OrderDate { get; init; }

    /// <summary>Date de livraison prévue annoncée au client, si connue.</summary>
    public DateTime? ExpectedDeliveryDate { get; init; }

    /// <summary>Entrepôt de livraison de la commande, pour le suivi des stocks.</summary>
    public Guid? WarehouseId { get; init; }
}
