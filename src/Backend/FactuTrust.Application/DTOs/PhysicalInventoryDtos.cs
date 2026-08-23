using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.DTOs;

/// <summary>
/// DTO for physical inventory list views.
/// </summary>
public sealed record PhysicalInventoryListDto(
    Guid Id,
    string Reference,
    DateTime StartedAt,
    DateTime? CompletedAt,
    Guid WarehouseId,
    string WarehouseName,
    InventoryType Type,
    string TypeLabel,
    InventoryStatus Status,
    string StatusDisplay,
    int TotalProducts,
    int CountedProducts,
    int ProgressPercent);

/// <summary>
/// Aggregated totals for an inventory list, computed over the ENTIRE filtered set
/// (not just the current page) so the UI "totals zone" reflects the active filters.
/// </summary>
public sealed record InventoryListSummaryDto
{
    public int Count { get; init; }
    public int InProgressCount { get; init; }
    public int ValidatedCount { get; init; }
    public int CancelledCount { get; init; }
    /// <summary>Total products in scope across the filtered inventories.</summary>
    public int TotalProducts { get; init; }
}

/// <summary>
/// Line item for physical inventory detail.
/// </summary>
public sealed record PhysicalInventoryDetailLineDto(
    Guid ProductId,
    string? ProductCode,
    string ProductName,
    decimal TheoreticalQuantity,
    decimal? CountedQuantity,
    decimal Difference,
    bool IsCounted,
    Guid? ProductLotId = null,
    string? LotNumber = null);

/// <summary>
/// DTO for physical inventory detail (consultation).
/// </summary>
public sealed record PhysicalInventoryDetailDto(
    Guid Id,
    string Reference,
    DateTime StartedAt,
    DateTime? CompletedAt,
    Guid WarehouseId,
    string WarehouseName,
    InventoryType Type,
    string TypeLabel,
    InventoryStatus Status,
    string StatusDisplay,
    string? Notes,
    int TotalProducts,
    int CountedProducts,
    IReadOnlyList<PhysicalInventoryDetailLineDto> Lines);
