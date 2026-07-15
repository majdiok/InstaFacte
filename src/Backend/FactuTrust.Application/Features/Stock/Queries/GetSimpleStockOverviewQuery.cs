using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using MediatR;

namespace FactuTrust.Application.Features.Stock.Queries;

/// <summary>
/// Query pour obtenir la vue d'ensemble simplifiée du stock.
/// Destinée aux utilisateurs non techniciens.
/// </summary>
public sealed record GetSimpleStockOverviewQuery(
    string? SearchTerm = null,
    bool OnlyWithAlerts = false,
    Guid? WarehouseId = null
) : IRequest<Result<SimpleStockOverviewDto>>;

/// <summary>
/// Handler pour GetSimpleStockOverviewQuery.
/// </summary>
public sealed class GetSimpleStockOverviewQueryHandler 
    : IRequestHandler<GetSimpleStockOverviewQuery, Result<SimpleStockOverviewDto>>
{
    private readonly IStockItemRepository _stockItemRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IProductRepository _productRepository;
    private readonly ITenantContext _tenantContext;

    public GetSimpleStockOverviewQueryHandler(
        IStockItemRepository stockItemRepository,
        IWarehouseRepository warehouseRepository,
        IProductRepository productRepository,
        ITenantContext tenantContext)
    {
        _stockItemRepository = stockItemRepository;
        _warehouseRepository = warehouseRepository;
        _productRepository = productRepository;
        _tenantContext = tenantContext;
    }

    public async Task<Result<SimpleStockOverviewDto>> Handle(
        GetSimpleStockOverviewQuery request, 
        CancellationToken cancellationToken)
    {
        if (_tenantContext.TenantId is null)
            return Result.Failure<SimpleStockOverviewDto>(
                Error.Unauthorized("Aucun contexte d'entreprise disponible."));

        Warehouse? overviewWarehouse;
        if (request.WarehouseId is { } wid)
        {
            var wh = await _warehouseRepository.GetByIdAsync(wid, cancellationToken);
            if (wh is null)
                return Result.Failure<SimpleStockOverviewDto>(Error.NotFound("Warehouse", wid));
            if (!wh.IsActive)
                return Result.Failure<SimpleStockOverviewDto>(
                    Error.Validation("Warehouse", "Cet entrepôt est désactivé."));
            overviewWarehouse = wh;
        }
        else
        {
            overviewWarehouse = await _warehouseRepository.GetDefaultAsync(cancellationToken);
        }

        if (overviewWarehouse == null)
        {
            return Result.Success(new SimpleStockOverviewDto(
                0, 0, 0, 0,
                new List<SimpleStockDto>(),
                new List<StockAlertDto>()));
        }

        // FIXED: Start from stock-managed products, not from StockItems
        // This ensures products with IsStockManaged=true appear even without prior stock entries
        var stockManagedProducts = await _productRepository.GetStockManagedProductsAsync(cancellationToken);

        var items = new List<SimpleStockDto>();
        var alerts = new List<StockAlertDto>();
        int inStock = 0, runningLow = 0, outOfStock = 0;

        foreach (var product in stockManagedProducts)
        {
            // Filtrer par terme de recherche si présent
            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var term = request.SearchTerm.ToLowerInvariant();
                if (!product.Name.ToLowerInvariant().Contains(term) &&
                    !product.Code.ToLowerInvariant().Contains(term))
                    continue;
            }

            // Get stock item if exists - may be null for new products
            var stockItem = await _stockItemRepository.GetByProductAndWarehouseAsync(
                product.Id, overviewWarehouse.Id, cancellationToken);
            
            // Use stock item values or defaults if no stock item exists
            var quantityOnHand = stockItem?.QuantityOnHand ?? 0;
            var minimumStock = stockItem?.MinimumStock ?? 0;

            // Calculer le statut
            var (status, label, icon) = StockStatusHelper.GetStatus(quantityOnHand, minimumStock);
            
            var alertMessage = StockStatusHelper.GetAlertMessage(
                product.Name, quantityOnHand, minimumStock);

            // Compter par statut
            switch (status)
            {
                case StockStatus.InStock: inStock++; break;
                case StockStatus.RunningLow: runningLow++; break;
                case StockStatus.OutOfStock: outOfStock++; break;
            }

            // Si on veut uniquement les alertes, filtrer
            if (request.OnlyWithAlerts && alertMessage == null)
                continue;

            items.Add(new SimpleStockDto(
                product.Id,
                product.Name,
                product.Code,
                null, // ImageUrl - à ajouter si disponible
                (int)quantityOnHand,
                status,
                label,
                icon,
                (int?)minimumStock,
                alertMessage));

            // Créer une alerte si nécessaire
            if (alertMessage != null)
            {
                var severity = status == StockStatus.OutOfStock ? "danger" : "warning";
                alerts.Add(new StockAlertDto(
                    product.Id,
                    product.Name,
                    alertMessage,
                    severity,
                    "Ajuster maintenant",
                    $"/stock/simple?adjust={product.Id}"));
            }
        }

        // Trier : alertes en premier, puis par nom
        items = items
            .OrderByDescending(i => i.Status == StockStatus.OutOfStock)
            .ThenByDescending(i => i.Status == StockStatus.RunningLow)
            .ThenBy(i => i.ProductName)
            .ToList();

        return Result.Success(new SimpleStockOverviewDto(
            items.Count,
            inStock,
            runningLow,
            outOfStock,
            items,
            alerts));
    }
}
