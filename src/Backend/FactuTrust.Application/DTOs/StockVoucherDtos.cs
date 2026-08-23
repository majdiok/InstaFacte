using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.DTOs;

public sealed record StockVoucherListDto
{
    public Guid Id { get; init; }
    public string Number { get; init; } = null!;
    public StockVoucherKind Kind { get; init; }
    public string KindDisplay { get; init; } = null!;
    public DateTime VoucherDate { get; init; }
    public StockVoucherStatus Status { get; init; }
    public string StatusDisplay { get; init; } = null!;
    public string StatusCss { get; init; } = null!;
    public MovementReason Reason { get; init; }
    public string ReasonDisplay { get; init; } = null!;
    public Guid WarehouseId { get; init; }
    public string? WarehouseName { get; init; }
    public string? ExternalReference { get; init; }
    public int LineCount { get; init; }
    public decimal TotalQuantity { get; init; }
    public decimal TotalValue { get; init; }
}

public sealed record StockVoucherListSummaryDto
{
    public int Count { get; init; }
    public int DraftCount { get; init; }
    public int ValidatedCount { get; init; }
    public int CancelledCount { get; init; }
    public decimal TotalQuantity { get; init; }
    public decimal TotalValue { get; init; }
}

public sealed record StockVoucherDetailDto
{
    public Guid Id { get; init; }
    public string Number { get; init; } = null!;
    public StockVoucherKind Kind { get; init; }
    public string KindDisplay { get; init; } = null!;
    public DateTime VoucherDate { get; init; }
    public StockVoucherStatus Status { get; init; }
    public string StatusDisplay { get; init; } = null!;
    public string StatusCss { get; init; } = null!;
    public MovementReason Reason { get; init; }
    public string ReasonDisplay { get; init; } = null!;
    public Guid WarehouseId { get; init; }
    public string? WarehouseName { get; init; }
    public string? ExternalReference { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<StockVoucherLineDto> Lines { get; init; } = Array.Empty<StockVoucherLineDto>();
    public decimal TotalQuantity { get; init; }
    public decimal TotalValue { get; init; }
    public DateTime? ValidatedAt { get; init; }
    public DateTime? CancelledAt { get; init; }
    public string? CancellationReason { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed record StockVoucherLineDto
{
    public Guid Id { get; init; }
    public int LineNumber { get; init; }
    public Guid ProductId { get; init; }
    public string ProductCode { get; init; } = null!;
    public string ProductName { get; init; } = null!;
    public string? Unit { get; init; }
    public decimal Quantity { get; init; }
    public decimal UnitCost { get; init; }
    public decimal LineValue { get; init; }
    public string? Notes { get; init; }
}

public sealed record CreateStockVoucherDto
{
    public StockVoucherKind Kind { get; init; }
    public DateTime VoucherDate { get; init; }
    public Guid WarehouseId { get; init; }
    public MovementReason Reason { get; init; }
    public string? ExternalReference { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<CreateStockVoucherLineDto> Lines { get; init; } = Array.Empty<CreateStockVoucherLineDto>();
}

public sealed record UpdateStockVoucherDto
{
    public DateTime VoucherDate { get; init; }
    public Guid WarehouseId { get; init; }
    public MovementReason Reason { get; init; }
    public string? ExternalReference { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<CreateStockVoucherLineDto> Lines { get; init; } = Array.Empty<CreateStockVoucherLineDto>();
}

public sealed record CreateStockVoucherLineDto
{
    public Guid ProductId { get; init; }
    public decimal Quantity { get; init; }
    public decimal? UnitCost { get; init; }
    public string? Notes { get; init; }
}

public sealed record CancelStockVoucherDto
{
    public string Reason { get; init; } = null!;
}
