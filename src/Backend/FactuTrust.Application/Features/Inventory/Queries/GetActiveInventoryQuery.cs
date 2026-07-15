using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Features.Inventory.Commands;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.Inventory.Queries;

/// <summary>
/// Récupère l'inventaire actif (en cours) pour un entrepôt.
/// </summary>
public sealed record GetActiveInventoryQuery(
    Guid? WarehouseId = null) : IRequest<Result<ActiveInventoryDto?>>;

/// <summary>
/// DTO pour l'inventaire actif avec progression.
/// </summary>
public sealed record ActiveInventoryDto
{
    public Guid InventoryId { get; init; }
    public Guid WarehouseId { get; init; }
    public string WarehouseName { get; init; } = string.Empty;
    public DateTime StartedAt { get; init; }
    public InventoryType Type { get; init; }
    public string TypeLabel { get; init; } = string.Empty;
    public int TotalProducts { get; init; }
    public int CountedProducts { get; init; }
    public int RemainingProducts => TotalProducts - CountedProducts;
    public decimal ProgressPercent => TotalProducts > 0 ? Math.Round((decimal)CountedProducts / TotalProducts * 100, 0) : 0;
    public string ProgressMessage { get; init; } = string.Empty;
    public IReadOnlyList<InventoryProductItem> Products { get; init; } = Array.Empty<InventoryProductItem>();
}

public sealed class GetActiveInventoryQueryHandler : IRequestHandler<GetActiveInventoryQuery, Result<ActiveInventoryDto?>>
{
    private readonly IPhysicalInventoryRepository _inventoryRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly ITenantContext _tenantContext;

    public GetActiveInventoryQueryHandler(
        IPhysicalInventoryRepository inventoryRepository,
        IWarehouseRepository warehouseRepository,
        ITenantContext tenantContext)
    {
        _inventoryRepository = inventoryRepository;
        _warehouseRepository = warehouseRepository;
        _tenantContext = tenantContext;
    }

    public async Task<Result<ActiveInventoryDto?>> Handle(GetActiveInventoryQuery request, CancellationToken cancellationToken)
    {
        if (_tenantContext.TenantId is null)
            return Result.Failure<ActiveInventoryDto?>(Error.Unauthorized("Aucun contexte d'entreprise disponible."));

        // Determine warehouse
        Guid warehouseId;
        if (request.WarehouseId.HasValue)
        {
            warehouseId = request.WarehouseId.Value;
        }
        else
        {
            var defaultWarehouse = await _warehouseRepository.GetDefaultAsync(cancellationToken);
            if (defaultWarehouse == null)
                return Result.Success<ActiveInventoryDto?>(null);
            warehouseId = defaultWarehouse.Id;
        }

        // Get active inventory
        var inventory = await _inventoryRepository.GetActiveAsync(warehouseId, cancellationToken);
        if (inventory == null)
            return Result.Success<ActiveInventoryDto?>(null);

        // Get warehouse name
        var warehouse = await _warehouseRepository.GetByIdAsync(warehouseId, cancellationToken);
        var warehouseName = warehouse?.Name ?? "Entrepôt";

        // Build product list with count status
        var products = inventory.CountLines.Select(line => new InventoryProductItem
        {
            ProductId = line.ProductId,
            ProductName = line.ProductName,
            ProductCode = line.ProductCode,
            TheoreticalQuantity = line.TheoreticalQuantity,
            IsCounted = line.IsCounted,
            CountedQuantity = line.CountedQuantity
        }).ToList();

        // Progress message
        var progressMessage = inventory.IsComplete
            ? "✅ Tous les produits ont été comptés !"
            : $"📊 {inventory.CountedProducts} / {inventory.TotalProducts} produits comptés";

        return Result.Success<ActiveInventoryDto?>(new ActiveInventoryDto
        {
            InventoryId = inventory.Id,
            WarehouseId = warehouseId,
            WarehouseName = warehouseName,
            StartedAt = inventory.StartedAt,
            Type = inventory.Type,
            TypeLabel = inventory.Type == InventoryType.Complete ? "Complet" : "Partiel",
            TotalProducts = inventory.TotalProducts,
            CountedProducts = inventory.CountedProducts,
            ProgressMessage = progressMessage,
            Products = products
        });
    }
}
