using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class StockTransferRepository : IStockTransferRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public StockTransferRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<StockTransfer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.StockTransfers
            .Include(t => t.SourceWarehouse)
            .Include(t => t.DestinationWarehouse)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<StockTransfer?> GetByIdWithLinesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.StockTransfers
            .Include(t => t.SourceWarehouse)
            .Include(t => t.DestinationWarehouse)
            .Include(t => t.Lines)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<string?> GetLatestNumberAsync(int year, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.StockTransfers
            .Where(t => t.Number.Year == year)
            .OrderByDescending(t => t.Number.Sequence)
            .Select(t => t.Number.Value)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StockTransfer>> GetFilteredAsync(
        StockTransferStatus? status = null,
        Guid? warehouseId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var query = context.StockTransfers
            .Include(t => t.SourceWarehouse)
            .Include(t => t.DestinationWarehouse)
            .Include(t => t.Lines)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);

        if (warehouseId.HasValue)
            query = query.Where(t => t.SourceWarehouseId == warehouseId.Value || t.DestinationWarehouseId == warehouseId.Value);

        if (fromDate.HasValue)
            query = query.Where(t => t.TransferDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(t => t.TransferDate <= toDate.Value);

        return await query
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StockTransfer>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.StockTransfers
            .Include(t => t.SourceWarehouse)
            .Include(t => t.DestinationWarehouse)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<StockTransfer> AddAsync(StockTransfer entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.StockTransfers.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(StockTransfer entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.StockTransfers.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(StockTransfer entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.StockTransfers.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.StockTransfers.AnyAsync(t => t.Id == id, cancellationToken);
    }
}
