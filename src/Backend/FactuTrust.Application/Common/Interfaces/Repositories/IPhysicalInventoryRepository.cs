using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

/// <summary>
/// Repository interface for PhysicalInventory aggregate.
/// </summary>
public interface IPhysicalInventoryRepository : IRepository<PhysicalInventory>
{
    /// <summary>
    /// Gets the currently active (InProgress) inventory for a warehouse.
    /// Only one inventory can be active at a time per warehouse.
    /// </summary>
    Task<PhysicalInventory?> GetActiveAsync(
        Guid warehouseId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets an inventory with all its count lines loaded.
    /// </summary>
    Task<PhysicalInventory?> GetWithLinesAsync(
        Guid inventoryId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets recent inventories for a warehouse (validated or cancelled).
    /// </summary>
    Task<IReadOnlyList<PhysicalInventory>> GetRecentAsync(
        Guid warehouseId,
        int limit = 10,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if there's an active inventory for the warehouse.
    /// </summary>
    Task<bool> HasActiveInventoryAsync(
        Guid warehouseId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Paginated search with optional filters (reference, status, date range, warehouse).
    /// </summary>
    Task<(IReadOnlyList<PhysicalInventory> Items, int TotalCount)> SearchAsync(
        string? search,
        InventoryStatus? status,
        DateTime? fromDate,
        DateTime? toDate,
        Guid? warehouseId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Aggregated totals over the ENTIRE filtered set (same filters as <see cref="SearchAsync"/>,
    /// without pagination). Powers the inventory list "totals zone".
    /// </summary>
    Task<InventoryListSummaryDto> GetSummaryAsync(
        string? search,
        InventoryStatus? status,
        DateTime? fromDate,
        DateTime? toDate,
        Guid? warehouseId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the next reference for a new inventory (e.g. INVE-000001).
    /// Uses InventoryNumberSequences for the current year.
    /// </summary>
    Task<string> GetNextReferenceAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an inventory by id with count lines and warehouse loaded (for detail view).
    /// </summary>
    Task<PhysicalInventory?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
}
