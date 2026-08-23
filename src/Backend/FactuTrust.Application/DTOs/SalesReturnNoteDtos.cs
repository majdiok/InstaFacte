using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.DTOs;

public sealed record SalesReturnNoteListDto
{
    public Guid Id { get; init; }
    public string Number { get; init; } = null!;
    public DateTime ReturnDate { get; init; }
    public Guid ClientId { get; init; }
    public string ClientName { get; init; } = null!;
    public Guid DeliveryNoteId { get; init; }
    public string DeliveryNoteNumber { get; init; } = null!;
    public SalesReturnNoteStatus Status { get; init; }
    public string StatusDisplay { get; init; } = null!;
    public string StatusCss { get; init; } = null!;
    public string Reason { get; init; } = null!;
    public int LineCount { get; init; }
    public decimal TotalReturnedQuantity { get; init; }
    public decimal TotalHT { get; init; }
    public decimal TotalVAT { get; init; }
    public decimal TotalTTC { get; init; }
    public Guid? WarehouseId { get; init; }
    public string? WarehouseName { get; init; }
}

public sealed record SalesReturnNoteListSummaryDto
{
    public int Count { get; init; }
    public decimal TotalTtc { get; init; }
    public decimal TotalHt { get; init; }
    public decimal TotalVat { get; init; }
    public int ConfirmedCount { get; init; }
    public int DraftCount { get; init; }
    public string Currency { get; init; } = "TND";
}

public sealed record SalesReturnNoteDetailDto
{
    public Guid Id { get; init; }
    public string Number { get; init; } = null!;
    public DateTime ReturnDate { get; init; }
    public SalesReturnNoteStatus Status { get; init; }
    public string StatusDisplay { get; init; } = null!;
    public string StatusCss { get; init; } = null!;
    public Guid ClientId { get; init; }
    public string ClientName { get; init; } = null!;
    public Guid DeliveryNoteId { get; init; }
    public string DeliveryNoteNumber { get; init; } = null!;
    public Guid? WarehouseId { get; init; }
    public string? WarehouseName { get; init; }
    public string Reason { get; init; } = null!;
    public string? Notes { get; init; }
    public DateTime? ConfirmedAt { get; init; }
    public decimal TotalHT { get; init; }
    public decimal TotalFodec { get; init; }
    public decimal TotalVAT { get; init; }
    public decimal TotalTTC { get; init; }
    public decimal TotalReturnedQuantity { get; init; }
    public IReadOnlyList<SalesReturnNoteLineDto> Lines { get; init; } = Array.Empty<SalesReturnNoteLineDto>();
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed record SalesReturnNoteLineDto
{
    public Guid Id { get; init; }
    public Guid DeliveryNoteLineId { get; init; }
    public int LineNumber { get; init; }
    public Guid ProductId { get; init; }
    public string ProductCode { get; init; } = null!;
    public string Designation { get; init; } = null!;
    public string? Description { get; init; }
    public string Unit { get; init; } = null!;
    public decimal UnitPriceHT { get; init; }
    public int VatRatePercent { get; init; }
    public decimal? DiscountPercent { get; init; }
    public bool IsFodecApplicable { get; init; }
    public decimal FodecRatePercent { get; init; }
    public decimal ReturnedQuantity { get; init; }
    public string? Notes { get; init; }
    public decimal TotalHT { get; init; }
    public decimal FodecAmount { get; init; }
    public decimal TotalVAT { get; init; }
    public decimal TotalTTC { get; init; }
}

public sealed record CreateSalesReturnNoteDto
{
    public Guid DeliveryNoteId { get; init; }
    public DateTime ReturnDate { get; init; }
    public string Reason { get; init; } = null!;
    public string? Notes { get; init; }
    public IReadOnlyList<CreateSalesReturnNoteLineDto> Lines { get; init; } = Array.Empty<CreateSalesReturnNoteLineDto>();
}

public sealed record CreateSalesReturnNoteLineDto
{
    public Guid DeliveryNoteLineId { get; init; }
    public decimal ReturnedQuantity { get; init; }
    public string? Notes { get; init; }
}

public sealed record UpdateSalesReturnNoteDto
{
    public DateTime ReturnDate { get; init; }
    public string Reason { get; init; } = null!;
    public string? Notes { get; init; }
    public IReadOnlyList<CreateSalesReturnNoteLineDto> Lines { get; init; } = Array.Empty<CreateSalesReturnNoteLineDto>();
}

public sealed record EligibleDeliveryNoteDto
{
    public Guid Id { get; init; }
    public string Number { get; init; } = null!;
    public DateTime IssueDate { get; init; }
    public Guid ClientId { get; init; }
    public string ClientName { get; init; } = null!;
    public string StatusDisplay { get; init; } = null!;
    public decimal TotalInvoiceableQuantity { get; init; }
    public int InvoiceableLineCount { get; init; }
    public Guid? WarehouseId { get; init; }
    public string? WarehouseName { get; init; }
}

public sealed record SalesReturnNotePrefillDto
{
    public Guid DeliveryNoteId { get; init; }
    public string DeliveryNoteNumber { get; init; } = null!;
    public Guid ClientId { get; init; }
    public string ClientName { get; init; } = null!;
    public Guid? WarehouseId { get; init; }
    public string? WarehouseName { get; init; }
    public IReadOnlyList<SalesReturnNotePrefillLineDto> Lines { get; init; } = Array.Empty<SalesReturnNotePrefillLineDto>();
}

public sealed record SalesReturnNotePrefillLineDto
{
    public Guid DeliveryNoteLineId { get; init; }
    public int LineNumber { get; init; }
    public Guid ProductId { get; init; }
    public string ProductCode { get; init; } = null!;
    public string Designation { get; init; } = null!;
    public string Unit { get; init; } = null!;
    public decimal UnitPriceHT { get; init; }
    public decimal DeliveredQuantity { get; init; }
    public decimal AlreadyReturnedQuantity { get; init; }
    public decimal InvoiceableQuantity { get; init; }
}

public sealed record LinkedSalesReturnNoteDto(
    Guid Id,
    string Number,
    SalesReturnNoteStatus Status,
    string StatusDisplay,
    DateTime ReturnDate,
    decimal TotalReturnedQuantity);
