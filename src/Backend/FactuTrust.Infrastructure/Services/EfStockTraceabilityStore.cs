using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services;

public sealed class EfStockTraceabilityStore : IStockTraceabilityStore
{
    private readonly TenantDbContext _context;

    public EfStockTraceabilityStore(TenantDbContext context)
    {
        _context = context;
    }

    public ProductLot? FindLotByNumber(Guid productId, string lotNumber)
    {
        var normalized = lotNumber.Trim().ToUpperInvariant();
        return _context.ProductLots.Local.FirstOrDefault(l => l.ProductId == productId && l.LotNumber == normalized)
            ?? _context.ProductLots.FirstOrDefault(l => l.ProductId == productId && l.LotNumber == normalized);
    }

    public ProductLot? GetLot(Guid lotId) =>
        _context.ProductLots.Local.FirstOrDefault(l => l.Id == lotId)
        ?? _context.ProductLots.FirstOrDefault(l => l.Id == lotId);

    public void AddLot(ProductLot lot) => _context.ProductLots.Add(lot);

    public StockLotBalance? FindBalance(Guid stockItemId, Guid lotId) =>
        _context.StockLotBalances.Local.FirstOrDefault(b => b.StockItemId == stockItemId && b.ProductLotId == lotId)
        ?? _context.StockLotBalances.FirstOrDefault(b => b.StockItemId == stockItemId && b.ProductLotId == lotId);

    public IReadOnlyList<StockLotBalance> ListBalances(Guid stockItemId)
    {
        var fromDb = _context.StockLotBalances.Where(b => b.StockItemId == stockItemId).ToList();
        foreach (var local in _context.StockLotBalances.Local.Where(b => b.StockItemId == stockItemId))
        {
            if (fromDb.All(b => b.Id != local.Id))
                fromDb.Add(local);
        }
        return fromDb;
    }

    public void AddBalance(StockLotBalance balance) => _context.StockLotBalances.Add(balance);

    public IReadOnlyList<StockValuationLayer> ListOpenLayers(Guid stockItemId)
    {
        var fromDb = _context.StockValuationLayers
            .Where(l => l.StockItemId == stockItemId && l.RemainingQuantity > 0)
            .ToList();
        foreach (var local in _context.StockValuationLayers.Local.Where(l => l.StockItemId == stockItemId && l.RemainingQuantity > 0))
        {
            if (fromDb.All(l => l.Id != local.Id))
                fromDb.Add(local);
        }
        return fromDb;
    }

    public StockValuationLayer? GetLayer(Guid layerId) =>
        _context.StockValuationLayers.Local.FirstOrDefault(l => l.Id == layerId)
        ?? _context.StockValuationLayers.FirstOrDefault(l => l.Id == layerId);

    public void AddLayer(StockValuationLayer layer) => _context.StockValuationLayers.Add(layer);

    public ProductSerial? FindSerial(Guid productId, string serialNumber)
    {
        var normalized = serialNumber.Trim().ToUpperInvariant();
        return _context.ProductSerials.Local.FirstOrDefault(s => s.ProductId == productId && s.SerialNumber == normalized)
            ?? _context.ProductSerials.FirstOrDefault(s => s.ProductId == productId && s.SerialNumber == normalized);
    }

    public ProductSerial? GetSerial(Guid serialId) =>
        _context.ProductSerials.Local.FirstOrDefault(s => s.Id == serialId)
        ?? _context.ProductSerials.FirstOrDefault(s => s.Id == serialId);

    public IReadOnlyList<ProductSerial> ListInStockSerials(Guid productId, Guid warehouseId)
    {
        var fromDb = _context.ProductSerials
            .Where(s => s.ProductId == productId && s.WarehouseId == warehouseId && s.Status == SerialStatus.InStock)
            .ToList();
        foreach (var local in _context.ProductSerials.Local.Where(s =>
                     s.ProductId == productId && s.WarehouseId == warehouseId && s.Status == SerialStatus.InStock))
        {
            if (fromDb.All(s => s.Id != local.Id))
                fromDb.Add(local);
        }
        return fromDb;
    }

    public void AddSerial(ProductSerial serial) => _context.ProductSerials.Add(serial);

    public void AddAllocation(StockDocumentAllocation allocation) => _context.StockDocumentAllocations.Add(allocation);

    public IReadOnlyList<StockDocumentAllocation> ListAllocations(StockDocumentKind kind, Guid documentLineId)
    {
        var fromDb = _context.StockDocumentAllocations
            .Where(a => a.DocumentKind == kind && a.DocumentLineId == documentLineId)
            .ToList();
        foreach (var local in _context.StockDocumentAllocations.Local.Where(a =>
                     a.DocumentKind == kind && a.DocumentLineId == documentLineId))
        {
            if (fromDb.All(a => a.Id != local.Id))
                fromDb.Add(local);
        }
        return fromDb;
    }

    public DateTime? GetLotFirstReceivedAt(Guid lotId)
    {
        var movement = _context.StockMovements
            .Where(m => m.ProductLotId == lotId && m.Type == MovementType.Entry)
            .OrderBy(m => m.OccurredAt)
            .FirstOrDefault();
        return movement?.OccurredAt;
    }
}
