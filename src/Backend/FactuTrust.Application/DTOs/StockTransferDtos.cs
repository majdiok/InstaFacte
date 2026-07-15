using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.DTOs;

public sealed record StockTransferListDto
{
    public Guid Id { get; init; }
    public string Number { get; init; } = null!;
    public DateTime TransferDate { get; init; }
    public StockTransferStatus Status { get; init; }
    public string StatusDisplay { get; init; } = null!;
    public string StatusCss { get; init; } = null!;
    public string? Reference { get; init; }
    public Guid SourceWarehouseId { get; init; }
    public string SourceWarehouseName { get; init; } = null!;
    public Guid DestinationWarehouseId { get; init; }
    public string DestinationWarehouseName { get; init; } = null!;
    public int LineCount { get; init; }
    public decimal TotalRequestedQuantity { get; init; }
    public decimal TotalTransferredQuantity { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed record StockTransferDetailDto
{
    public Guid Id { get; init; }
    public string Number { get; init; } = null!;
    public DateTime TransferDate { get; init; }
    public StockTransferStatus Status { get; init; }
    public string StatusDisplay { get; init; } = null!;
    public string StatusCss { get; init; } = null!;
    public string? Reference { get; init; }
    public string? Notes { get; init; }

    public Guid SourceWarehouseId { get; init; }
    public string SourceWarehouseName { get; init; } = null!;
    public string? SourceWarehouseAddress { get; init; }

    public Guid DestinationWarehouseId { get; init; }
    public string DestinationWarehouseName { get; init; } = null!;
    public string? DestinationWarehouseAddress { get; init; }

    public IReadOnlyList<StockTransferLineDto> Lines { get; init; } = Array.Empty<StockTransferLineDto>();

    public decimal TotalRequestedQuantity { get; init; }
    public decimal TotalTransferredQuantity { get; init; }

    public DateTime? ConfirmedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime? CancelledAt { get; init; }
    public string? CancellationReason { get; init; }

    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed record StockTransferLineDto
{
    public Guid Id { get; init; }
    public int LineNumber { get; init; }
    public Guid ProductId { get; init; }
    public string ProductCode { get; init; } = null!;
    public string ProductName { get; init; } = null!;
    public decimal RequestedQuantity { get; init; }
    public decimal TransferredQuantity { get; init; }
    public bool IsFullyTransferred { get; init; }
    public string? Notes { get; init; }
}

public sealed record CreateStockTransferDto
{
    public Guid SourceWarehouseId { get; init; }
    public Guid DestinationWarehouseId { get; init; }
    public DateTime TransferDate { get; init; }
    public string? Reference { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<CreateStockTransferLineDto> Lines { get; init; } = Array.Empty<CreateStockTransferLineDto>();
}

public sealed record CreateStockTransferLineDto
{
    public Guid ProductId { get; init; }
    public decimal RequestedQuantity { get; init; }
    public string? Notes { get; init; }
}
