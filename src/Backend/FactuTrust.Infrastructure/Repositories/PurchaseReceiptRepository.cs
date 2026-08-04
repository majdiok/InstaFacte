using System.Reflection;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class PurchaseReceiptRepository : IPurchaseReceiptRepository
{
    private readonly TenantDbContextFactory _contextFactory;

    public PurchaseReceiptRepository(TenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<PurchaseReceipt?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PurchaseReceipts
            .Include(r => r.Supplier)
            .Include(r => r.Warehouse)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<PurchaseReceipt?> GetByIdWithLinesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PurchaseReceipts
            .Include(r => r.Supplier)
            .Include(r => r.Warehouse)
            .Include(r => r.PurchaseOrder)
            .Include(r => r.Lines)
                .ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<PurchaseReceipt?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PurchaseReceipts
            .Include(r => r.Supplier)
            .Include(r => r.Warehouse)
            .Include(r => r.PurchaseOrder)
            .Include(r => r.Lines)
                .ThenInclude(l => l.Product)
            .Include(r => r.Attachments)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<PurchaseReceipt>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PurchaseReceipts
            .Include(r => r.Supplier)
            .OrderByDescending(r => r.ReceiptDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<PurchaseReceipt> Items, int TotalCount)> SearchAsync(
        string? searchTerm,
        PurchaseReceiptStatus? status,
        Guid? supplierId,
        Guid? purchaseOrderId,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();

        var query = ApplyFilters(
            context.PurchaseReceipts
                .Include(r => r.Supplier)
                .Include(r => r.Warehouse)
                .Include(r => r.PurchaseOrder)
                .Include(r => r.Lines)
                .AsQueryable(),
            searchTerm, status, supplierId, purchaseOrderId, fromDate, toDate);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(r => r.ReceiptDate)
            .ThenByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<PurchaseReceiptListSummaryDto> GetSummaryAsync(
        string? searchTerm,
        PurchaseReceiptStatus? status,
        Guid? supplierId,
        Guid? purchaseOrderId,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();

        var query = ApplyFilters(
            context.PurchaseReceipts.AsQueryable(),
            searchTerm, status, supplierId, purchaseOrderId, fromDate, toDate);

        var count = await query.CountAsync(cancellationToken);
        var totalHt = await query.SumAsync(r => r.SubTotal.Amount, cancellationToken);
        var totalVat = await query.SumAsync(r => r.TotalVat.Amount, cancellationToken);
        var totalTtc = await query.SumAsync(r => r.TotalAmount.Amount, cancellationToken);
        var validatedCount = await query.CountAsync(r => r.Status == PurchaseReceiptStatus.Validated, cancellationToken);
        var draftCount = await query.CountAsync(r => r.Status == PurchaseReceiptStatus.Draft, cancellationToken);

        return new PurchaseReceiptListSummaryDto
        {
            Count = count,
            TotalHt = totalHt,
            TotalVat = totalVat,
            TotalTtc = totalTtc,
            ValidatedCount = validatedCount,
            DraftCount = draftCount,
            Currency = "TND"
        };
    }

    public async Task<IReadOnlyList<PurchaseReceipt>> GetByPurchaseOrderIdAsync(
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PurchaseReceipts
            .Include(r => r.Lines)
            .Where(r => r.PurchaseOrderId == purchaseOrderId)
            .OrderByDescending(r => r.ReceiptDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsNumberAsync(string number, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PurchaseReceipts
            .AnyAsync(r => r.Number.Value == number, cancellationToken);
    }

    public async Task<PurchaseReceipt> AddAsync(PurchaseReceipt entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();

        if (entity.Supplier != null)
            AttachUnchanged(context, entity.Supplier);

        if (entity.Warehouse != null)
            AttachUnchanged(context, entity.Warehouse);

        foreach (var line in entity.Lines)
        {
            if (line.Product != null)
            {
                EnsureProductCategoryTrackedOnce(context, line.Product);
                var tracked = context.ChangeTracker.Entries<Product>()
                    .FirstOrDefault(e => e.Entity.Id == line.Product.Id)?.Entity;
                if (tracked != null)
                {
                    typeof(PurchaseReceiptLine).GetProperty("Product", BindingFlags.Public | BindingFlags.Instance)
                        ?.SetValue(line, tracked);
                }
                else
                {
                    context.Products.Attach(line.Product);
                    context.Entry(line.Product).State = EntityState.Unchanged;
                }
            }
        }

        context.PurchaseReceipts.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(PurchaseReceipt entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.PurchaseReceipts.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(PurchaseReceipt entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.PurchaseReceipts.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PurchaseReceipts.AnyAsync(r => r.Id == id, cancellationToken);
    }

    private static IQueryable<PurchaseReceipt> ApplyFilters(
        IQueryable<PurchaseReceipt> query,
        string? searchTerm,
        PurchaseReceiptStatus? status,
        Guid? supplierId,
        Guid? purchaseOrderId,
        DateTime? fromDate,
        DateTime? toDate)
    {
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(r =>
                r.Number.Value.Contains(term) ||
                r.Supplier.Name.Contains(term) ||
                (r.SupplierReference != null && r.SupplierReference.Contains(term)) ||
                (r.DeliveryNoteNumber != null && r.DeliveryNoteNumber.Contains(term)));
        }

        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        if (supplierId.HasValue)
            query = query.Where(r => r.SupplierId == supplierId.Value);

        if (purchaseOrderId.HasValue)
            query = query.Where(r => r.PurchaseOrderId == purchaseOrderId.Value);

        if (fromDate.HasValue)
            query = query.Where(r => r.ReceiptDate >= fromDate.Value.Date);

        if (toDate.HasValue)
            query = query.Where(r => r.ReceiptDate <= toDate.Value.Date);

        return query;
    }

    private static void AttachUnchanged<TEntity>(DbContext context, TEntity entity) where TEntity : class
    {
        var entry = context.Entry(entity);
        if (entry.State == EntityState.Detached)
        {
            context.Set<TEntity>().Attach(entity);
            context.Entry(entity).State = EntityState.Unchanged;
        }
    }

    private static void EnsureProductCategoryTrackedOnce(DbContext context, Product product)
    {
        if (product.Category == null) return;

        var trackedCategory = context.ChangeTracker.Entries<ProductCategory>()
            .FirstOrDefault(e => e.Entity.Id == product.Category.Id)?.Entity;

        if (trackedCategory != null)
        {
            typeof(Product).GetProperty(nameof(Product.Category), BindingFlags.Public | BindingFlags.Instance)
                ?.SetValue(product, trackedCategory);
        }
    }
}
