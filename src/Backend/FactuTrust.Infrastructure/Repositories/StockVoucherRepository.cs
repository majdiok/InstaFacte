using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class StockVoucherRepository : IStockVoucherRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public StockVoucherRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<StockVoucher?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.StockVouchers
            .Include(v => v.Warehouse)
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
    }

    public async Task<StockVoucher?> GetByIdWithLinesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.StockVouchers
            .Include(v => v.Warehouse)
            .Include(v => v.Lines)
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<StockVoucher> Items, int TotalCount)> SearchAsync(
        StockVoucherKind? kind,
        string? searchTerm,
        StockVoucherStatus? status,
        Guid? warehouseId,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var query = BuildFilter(context.StockVouchers.Include(v => v.Warehouse).Include(v => v.Lines),
            kind, searchTerm, status, warehouseId, fromDate, toDate);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(v => v.VoucherDate)
            .ThenByDescending(v => v.CreatedAt)
            .Skip((Math.Max(page, 1) - 1) * Math.Max(pageSize, 1))
            .Take(Math.Max(pageSize, 1))
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<StockVoucherListSummaryDto> GetSummaryAsync(
        StockVoucherKind? kind,
        string? searchTerm,
        StockVoucherStatus? status,
        Guid? warehouseId,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var query = BuildFilter(context.StockVouchers.Include(v => v.Lines),
            kind, searchTerm, status, warehouseId, fromDate, toDate);

        var vouchers = await query.AsNoTracking().ToListAsync(cancellationToken);
        return new StockVoucherListSummaryDto
        {
            Count = vouchers.Count,
            DraftCount = vouchers.Count(v => v.Status == StockVoucherStatus.Draft),
            ValidatedCount = vouchers.Count(v => v.Status == StockVoucherStatus.Validated),
            CancelledCount = vouchers.Count(v => v.Status == StockVoucherStatus.Cancelled),
            TotalQuantity = vouchers.Sum(v => v.TotalQuantity),
            TotalValue = vouchers.Sum(v => v.TotalValue)
        };
    }

    public async Task<IReadOnlyList<StockVoucher>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.StockVouchers
            .Include(v => v.Warehouse)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<StockVoucher> AddAsync(StockVoucher entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        AttachWarehouseUnchanged(context, entity);
        context.StockVouchers.Add(entity);
        AttachWarehouseUnchanged(context, entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(StockVoucher entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        AttachWarehouseUnchanged(context, entity);
        context.StockVouchers.Update(entity);
        AttachWarehouseUnchanged(context, entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(StockVoucher entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.StockVouchers.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.StockVouchers.AnyAsync(v => v.Id == id, cancellationToken);
    }

    private static IQueryable<StockVoucher> BuildFilter(
        IQueryable<StockVoucher> query,
        StockVoucherKind? kind,
        string? searchTerm,
        StockVoucherStatus? status,
        Guid? warehouseId,
        DateTime? fromDate,
        DateTime? toDate)
    {
        if (kind.HasValue)
            query = query.Where(v => v.Kind == kind.Value);

        if (status.HasValue)
            query = query.Where(v => v.Status == status.Value);

        if (warehouseId.HasValue)
            query = query.Where(v => v.WarehouseId == warehouseId.Value);

        if (fromDate.HasValue)
            query = query.Where(v => v.VoucherDate >= fromDate.Value.Date);

        if (toDate.HasValue)
            query = query.Where(v => v.VoucherDate <= toDate.Value.Date);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(v =>
                v.Number.Value.Contains(term) ||
                (v.ExternalReference != null && v.ExternalReference.Contains(term)) ||
                v.Lines.Any(l => l.ProductCode.Contains(term) || l.ProductName.Contains(term)));
        }

        return query;
    }

    /// <summary>
    /// Warehouses are loaded from a different DbContext. Without this, Add/Update treat
    /// the navigation as Added and INSERT a duplicate PK.
    /// </summary>
    private static void AttachWarehouseUnchanged(DbContext context, StockVoucher entity)
    {
        if (entity.Warehouse is null)
            return;

        var entry = context.Entry(entity.Warehouse);
        if (entry.State == EntityState.Detached)
            context.Set<Warehouse>().Attach(entity.Warehouse);

        context.Entry(entity.Warehouse).State = EntityState.Unchanged;
    }
}
