using FactuTrust.Application.Features.Stock.Queries;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services;

public sealed class EfStockTraceabilityQuery : IStockTraceabilityQuery
{
    private readonly ITenantDbContextFactory _factory;

    public EfStockTraceabilityQuery(ITenantDbContextFactory factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<StockLotBalanceDto>> ListLotsAsync(
        Guid stockItemId,
        CancellationToken cancellationToken = default)
    {
        await using var context = _factory.CreateContext();
        var rows = await (
            from balance in context.StockLotBalances.AsNoTracking()
            join lot in context.ProductLots.AsNoTracking() on balance.ProductLotId equals lot.Id
            where balance.StockItemId == stockItemId
            select new
            {
                lot.Id,
                lot.LotNumber,
                lot.ExpiryDate,
                balance.QuantityOnHand,
                balance.QuantityReserved
            }).ToListAsync(cancellationToken);

        return rows
            .OrderBy(x => x.ExpiryDate ?? DateTime.MaxValue)
            .ThenBy(x => x.LotNumber)
            .Select(x => new StockLotBalanceDto(
                x.Id,
                x.LotNumber,
                x.ExpiryDate,
                x.QuantityOnHand,
                x.QuantityReserved,
                x.QuantityOnHand - x.QuantityReserved))
            .ToList();
    }

    public async Task<IReadOnlyList<StockValuationLayerDto>> ListValuationLayersAsync(
        Guid stockItemId,
        CancellationToken cancellationToken = default)
    {
        await using var context = _factory.CreateContext();
        var layers = await context.StockValuationLayers
            .AsNoTracking()
            .Where(l => l.StockItemId == stockItemId && l.RemainingQuantity > 0)
            .OrderBy(l => l.ReceivedAt)
            .ThenBy(l => l.Id)
            .ToListAsync(cancellationToken);

        var lotIds = layers.Where(l => l.ProductLotId.HasValue).Select(l => l.ProductLotId!.Value).Distinct().ToList();
        var lotNumbers = lotIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await context.ProductLots.AsNoTracking()
                .Where(l => lotIds.Contains(l.Id))
                .ToDictionaryAsync(l => l.Id, l => l.LotNumber, cancellationToken);

        return layers.Select(l => new StockValuationLayerDto(
            l.ReceivedAt,
            l.RemainingQuantity,
            l.OriginalQuantity,
            l.UnitCost,
            l.RemainingValue,
            l.ProductLotId is Guid lotId && lotNumbers.TryGetValue(lotId, out var number) ? number : null,
            l.SourceReference)).ToList();
    }

    public async Task<IReadOnlyList<ExpiryAlertDto>> ListExpiryAlertsAsync(
        Guid? warehouseId,
        CancellationToken cancellationToken = default)
    {
        await using var context = _factory.CreateContext();
        var today = DateTime.UtcNow.Date;

        var query =
            from balance in context.StockLotBalances.AsNoTracking()
            join lot in context.ProductLots.AsNoTracking() on balance.ProductLotId equals lot.Id
            join item in context.StockItems.AsNoTracking() on balance.StockItemId equals item.Id
            join product in context.Products.AsNoTracking() on item.ProductId equals product.Id
            join warehouse in context.Warehouses.AsNoTracking() on item.WarehouseId equals warehouse.Id
            where lot.ExpiryDate != null
                  && balance.QuantityOnHand > 0
                  && (!warehouseId.HasValue || item.WarehouseId == warehouseId)
            select new { balance, lot, item, product, warehouse };

        var rows = await query.ToListAsync(cancellationToken);
        return rows
            .Where(r =>
            {
                var alertDays = r.product.ExpiryAlertDays ?? 30;
                var remaining = (r.lot.ExpiryDate!.Value.Date - today).Days;
                return remaining <= alertDays;
            })
            .Select(r => new ExpiryAlertDto(
                r.product.Id,
                r.product.Code,
                r.product.Name,
                r.warehouse.Id,
                r.warehouse.Name,
                r.lot.LotNumber,
                r.lot.ExpiryDate!.Value,
                r.balance.QuantityOnHand,
                (r.lot.ExpiryDate!.Value.Date - today).Days))
            .OrderBy(a => a.ExpiryDate)
            .ToList();
    }

    public async Task<IReadOnlyList<ProductSerialDto>> ListInStockSerialsAsync(
        Guid productId,
        Guid warehouseId,
        CancellationToken cancellationToken = default)
    {
        await using var context = _factory.CreateContext();
        var serials = await context.ProductSerials
            .AsNoTracking()
            .Where(s => s.ProductId == productId
                        && s.WarehouseId == warehouseId
                        && s.Status == SerialStatus.InStock)
            .OrderBy(s => s.SerialNumber)
            .ToListAsync(cancellationToken);

        var lotIds = serials.Where(s => s.ProductLotId.HasValue).Select(s => s.ProductLotId!.Value).Distinct().ToList();
        var lots = lotIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await context.ProductLots.AsNoTracking()
                .Where(l => lotIds.Contains(l.Id))
                .ToDictionaryAsync(l => l.Id, l => l.LotNumber, cancellationToken);

        return serials.Select(s => new ProductSerialDto(
            s.Id,
            s.SerialNumber,
            s.ProductLotId,
            s.ProductLotId.HasValue ? lots.GetValueOrDefault(s.ProductLotId.Value) : null,
            s.ExpiryDate)).ToList();
    }

    public async Task<IReadOnlyList<ProductTraceabilityContextDto>> ListTraceabilityContextAsync(
        IReadOnlyList<Guid> productIds,
        Guid warehouseId,
        CancellationToken cancellationToken = default)
    {
        if (productIds.Count == 0)
            return Array.Empty<ProductTraceabilityContextDto>();

        await using var context = _factory.CreateContext();
        var ids = productIds.Distinct().ToList();

        var products = await context.Products
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                p.TrackingMode,
                p.PickingPolicy,
                p.HasExpiryTracking
            })
            .ToListAsync(cancellationToken);

        var stockItems = await context.StockItems
            .AsNoTracking()
            .Where(s => s.WarehouseId == warehouseId && ids.Contains(s.ProductId))
            .Select(s => new { s.Id, s.ProductId })
            .ToListAsync(cancellationToken);

        var stockItemIds = stockItems.Select(s => s.Id).ToList();
        var lotCounts = await context.StockLotBalances
            .AsNoTracking()
            .Where(b => stockItemIds.Contains(b.StockItemId) && b.QuantityOnHand > 0)
            .GroupBy(b => b.StockItemId)
            .Select(g => new { StockItemId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var serialCounts = await context.ProductSerials
            .AsNoTracking()
            .Where(s => ids.Contains(s.ProductId)
                        && s.WarehouseId == warehouseId
                        && s.Status == SerialStatus.InStock)
            .GroupBy(s => s.ProductId)
            .Select(g => new { ProductId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var stockItemByProduct = stockItems.ToDictionary(s => s.ProductId, s => s.Id);
        var lotCountByStockItem = lotCounts.ToDictionary(x => x.StockItemId, x => x.Count);
        var serialCountByProduct = serialCounts.ToDictionary(x => x.ProductId, x => x.Count);

        return products.Select(p =>
        {
            var stockItemId = stockItemByProduct.GetValueOrDefault(p.Id);
            var lotCount = stockItemId != default
                ? lotCountByStockItem.GetValueOrDefault(stockItemId, 0)
                : 0;
            var serialCount = serialCountByProduct.GetValueOrDefault(p.Id, 0);

            return new ProductTraceabilityContextDto(
                p.Id,
                p.TrackingMode,
                p.PickingPolicy,
                p.HasExpiryTracking,
                lotCount,
                serialCount);
        }).ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetLotLabelsAsync(
        StockDocumentKind kind,
        IReadOnlyCollection<Guid> documentLineIds,
        CancellationToken cancellationToken = default)
    {
        if (documentLineIds.Count == 0)
            return new Dictionary<Guid, string>();

        await using var context = _factory.CreateContext();
        var ids = documentLineIds.Distinct().ToList();
        var rows = await (
            from a in context.StockDocumentAllocations.AsNoTracking()
            join lot in context.ProductLots.AsNoTracking() on a.ProductLotId equals lot.Id into lots
            from lot in lots.DefaultIfEmpty()
            join serial in context.ProductSerials.AsNoTracking() on a.SerialId equals serial.Id into serials
            from serial in serials.DefaultIfEmpty()
            where a.DocumentKind == kind && ids.Contains(a.DocumentLineId)
            select new { a.DocumentLineId, Lot = lot != null ? lot.LotNumber : null, Serial = serial != null ? serial.SerialNumber : null, a.Quantity }
        ).ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.DocumentLineId)
            .ToDictionary(
                g => g.Key,
                g => string.Join(", ", g.Select(x =>
                {
                    if (!string.IsNullOrWhiteSpace(x.Lot) && !string.IsNullOrWhiteSpace(x.Serial))
                        return $"Lot {x.Lot} / S/N {x.Serial} ({x.Quantity:N2})";
                    if (!string.IsNullOrWhiteSpace(x.Lot))
                        return $"Lot {x.Lot} ({x.Quantity:N2})";
                    if (!string.IsNullOrWhiteSpace(x.Serial))
                        return $"S/N {x.Serial}";
                    return $"{x.Quantity:N2}";
                })));
    }
}
