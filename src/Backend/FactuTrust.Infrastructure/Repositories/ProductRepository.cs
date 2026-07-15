using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for Product aggregate.
/// </summary>
public sealed class ProductRepository : IProductRepository
{
    private readonly TenantDbContextFactory _contextFactory;

    public ProductRepository(TenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.Products
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<Product?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.Products
            .FirstOrDefaultAsync(p => p.Code == code.ToUpperInvariant(), cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.Products
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> GetActiveProductsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.Products
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> GetActiveProductsWithCategoryAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.Products
            .Include(p => p.Category)
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> GetByTypeAsync(ProductType type, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.Products
            .Where(p => p.Type == type && p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Product> Items, int TotalCount)> SearchAsync(
        string? searchTerm,
        ProductType? type,
        bool? isActive,
        Guid? categoryId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();

        var query = context.Products.AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(p => 
                p.Name.Contains(searchTerm) ||
                p.Code.Contains(searchTerm));
        }

        if (type.HasValue)
            query = query.Where(p => p.Type == type.Value);

        if (isActive.HasValue)
            query = query.Where(p => p.IsActive == isActive.Value);

        if (categoryId.HasValue)
            query = query.Where(p => p.CategoryId == categoryId.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Include(p => p.Category)
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<bool> IsUsedInInvoicesAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.InvoiceLines.AnyAsync(l => l.ProductId == productId, cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> GetStockManagedProductsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.Products
            .Where(p => p.IsActive && p.IsStockManaged)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Product> AddAsync(Product entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.Products.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(Product entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.Products.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Product entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.Products.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.Products.AnyAsync(p => p.Id == id, cancellationToken);
    }

    public async Task UpdateImageUrlAsync(Guid productId, string imageUrl, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var product = await context.Products
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);
        if (product is null)
            return;
        product.SetImageUrl(imageUrl);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task ClearImageUrlAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var product = await context.Products
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);
        if (product is null)
            return;
        product.ClearImageUrl();
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetProductNamesByIdsAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken = default)
    {
        if (productIds.Count == 0)
            return new Dictionary<Guid, string>();

        await using var context = _contextFactory.CreateContext();
        var distinct = productIds.Distinct().ToList();
        return await context.Products
            .AsNoTracking()
            .Where(p => distinct.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);
    }
}
