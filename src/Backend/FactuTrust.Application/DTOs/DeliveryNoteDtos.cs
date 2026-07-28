using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.DTOs;

/// <summary>
/// Summary DTO for delivery note list views.
/// </summary>
public record DeliveryNoteListDto(
    Guid Id,
    string Number,
    DateTime IssueDate,
    DateTime? DeliveryDate,
    Guid ClientId,
    string ClientName,
    DeliveryNoteStatus Status,
    string StatusDisplay,
    string DeliveryAddress,
    int LineCount,
    decimal TotalOrderedQuantity,
    decimal TotalDeliveredQuantity,
    decimal TotalHT,
    decimal TotalVAT,
    decimal TotalTTC,
    bool IsSigned,
    Guid? InvoiceId,
    string? InvoiceNumber,
    Guid? WarehouseId = null,
    string? WarehouseName = null);

/// <summary>
/// Aggregated totals for a delivery note list, computed over the ENTIRE filtered set
/// (not just the current page) so the UI "totals zone" reflects the active filters.
/// </summary>
public sealed record DeliveryNoteListSummaryDto
{
    public int Count { get; init; }
    public decimal TotalTtc { get; init; }
    public decimal TotalHt { get; init; }
    public decimal TotalVat { get; init; }
    /// <summary>Number of delivered notes.</summary>
    public int DeliveredCount { get; init; }
    /// <summary>Number of notes already converted to an invoice.</summary>
    public int InvoicedCount { get; init; }
    public string Currency { get; init; } = "TND";
}

/// <summary>
/// Detailed DTO for delivery note detail view.
/// </summary>
public record DeliveryNoteDetailDto(
    Guid Id,
    string Number,
    DateTime IssueDate,
    DateTime? DeliveryDate,
    DeliveryNoteStatus Status,
    string StatusDisplay,
    Guid ClientId,
    string ClientName,
    string? ClientEmail,
    string? Reference,
    string? Notes,
    string DeliveryAddress,
    string? DeliveryCity,
    string? DeliveryPostalCode,
    string? RecipientName,
    DateTime? SignedAt,
    string? FailureReason,
    DateTime? FailedAt,
    string? CancellationReason,
    DateTime? CancelledAt,
    bool AllowGroupInvoicing,
    Guid? InvoiceId,
    string? InvoiceNumber,
    DateTime? InvoicedAt,
    decimal TotalHT,
    decimal TotalVAT,
    decimal TotalTTC,
    IReadOnlyList<DeliveryNoteLineDto> Lines,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    Guid? WarehouseId = null,
    string? WarehouseName = null);

/// <summary>
/// DTO for delivery note lines — includes product snapshot for display and invoicing.
/// </summary>
public record DeliveryNoteLineDto(
    Guid Id,
    int LineNumber,
    Guid ProductId,
    string ProductCode,
    string Designation,
    string? Description,
    string Unit,
    decimal UnitPriceHT,
    int VatRatePercent,
    decimal OrderedQuantity,
    decimal DeliveredQuantity,
    decimal RejectedQuantity,
    string? RejectionReason,
    decimal PendingQuantity,
    decimal TotalHT,
    decimal TotalVAT,
    decimal TotalTTC,
    bool IsFullyDelivered,
    string? Notes,
    decimal? DiscountPercent = null,
    decimal DiscountAmount = 0m,
    bool IsFodecApplicable = false,
    decimal FodecRatePercent = 0m,
    decimal FodecAmount = 0m);

/// <summary>
/// DTO for creating a new delivery note.
/// </summary>
public record CreateDeliveryNoteDto(
    Guid ClientId,
    DateTime IssueDate,
    string DeliveryAddress,
    string? DeliveryCity,
    string? DeliveryPostalCode,
    string? Reference,
    string? Notes,
    bool AllowGroupInvoicing,
    IReadOnlyList<CreateDeliveryNoteLineDto> Lines,
    Guid? WarehouseId = null);

/// <summary>
/// DTO for creating a delivery note line.
/// Only ProductId and OrderedQuantity are required — all other fields are
/// fetched from the product catalog by the backend (snapshot pattern).
/// </summary>
public record CreateDeliveryNoteLineDto(
    Guid ProductId,
    decimal OrderedQuantity,
    string? Notes,
    decimal? DiscountPercent = null);

/// <summary>
/// DTO for recording delivery completion.
/// </summary>
public record RecordDeliveryDto(
    DateTime DeliveryDate,
    string RecipientName,
    string? RecipientSignature,
    IReadOnlyList<RecordDeliveryLineDto>? Lines);

/// <summary>
/// DTO for recording delivery quantities per line.
/// </summary>
public record RecordDeliveryLineDto(
    Guid LineId,
    decimal DeliveredQuantity,
    decimal RejectedQuantity,
    string? RejectionReason);

/// <summary>
/// DTO for generating invoice from a delivery note.
/// </summary>
public record GenerateInvoiceFromDeliveryNoteDto(
    DateTime? IssueDate,
    DateTime? DueDate,
    string? Reference,
    string? Notes);

/// <summary>
/// DTO for generating invoice from multiple delivery notes (group invoicing).
/// </summary>
public record GenerateInvoiceFromDeliveryNotesDto(
    IReadOnlyList<Guid> DeliveryNoteIds,
    DateTime? IssueDate,
    DateTime? DueDate,
    string? Reference,
    string? Notes);

