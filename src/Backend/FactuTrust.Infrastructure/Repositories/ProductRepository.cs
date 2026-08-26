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

    public async Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            return null;

        var normalized = barcode.Trim();

        await using var context = _contextFactory.CreateContext();
        return await context.Products
            .Include(p => p.Category)
            .FirstOrDefaultAsync(
                p => p.Barcode!.Value == normalized && !p.IsVariantTemplate,
                cancellationToken);
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
        CancellationToken cancellationToken = default,
        bool excludeVariantTemplates = false,
        Guid? parentProductId = null,
        bool? isVariantTemplate = null,
        bool? hasParentProduct = null)
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

        if (excludeVariantTemplates)
            query = query.Where(p => !p.IsVariantTemplate);

        if (parentProductId.HasValue)
            query = query.Where(p => p.ParentProductId == parentProductId.Value);

        if (isVariantTemplate.HasValue)
            query = query.Where(p => p.IsVariantTemplate == isVariantTemplate.Value);

        if (hasParentProduct == true)
            query = query.Where(p => p.ParentProductId != null);

        if (hasParentProduct == false)
            query = query.Where(p => p.ParentProductId == null);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Include(p => p.Category)
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<Product> Items, int TotalCount)> GetChildrenByParentIdAsync(
        Guid parentProductId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();

        var query = context.Products
            .Where(p => p.ParentProductId == parentProductId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Include(p => p.Category)
            .OrderBy(p => p.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<Product>> SearchTemplatesForSelectAsync(
        string? searchTerm,
        bool? isActive,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();

        var query = context.Products.AsNoTracking()
            .Where(p => p.IsVariantTemplate)
            .AsQueryable();

        if (isActive.HasValue)
            query = query.Where(p => p.IsActive == isActive.Value);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(p =>
                p.Code.StartsWith(term) ||
                p.Name.Contains(term));
        }

        return await query
            .OrderBy(p => p.Name)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> SearchForSelectAsync(
        string? searchTerm,
        bool? isActive,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();

        var query = context.Products.AsNoTracking().Where(p => !p.IsVariantTemplate).AsQueryable();

        if (isActive.HasValue)
            query = query.Where(p => p.IsActive == isActive.Value);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            // Prefix on Code uses the unique index; Contains on Name for free-text matching.
            query = query.Where(p =>
                p.Code.StartsWith(term) ||
                p.Name.Contains(term));
        }

        return await query
            .OrderBy(p => p.Name)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, bool>> GetFodecFlagsByIdsAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken = default)
    {
        if (productIds.Count == 0)
            return new Dictionary<Guid, bool>();

        await using var context = _contextFactory.CreateContext();
        var distinct = productIds.Distinct().ToList();
        return await context.Products
            .AsNoTracking()
            .Where(p => distinct.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.IsFodecApplicable, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, ProductTrackingInfo>> GetTrackingInfoByIdsAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken = default)
    {
        if (productIds.Count == 0)
            return new Dictionary<Guid, ProductTrackingInfo>();

        await using var context = _contextFactory.CreateContext();
        var distinct = productIds.Distinct().ToList();
        return await context.Products
            .AsNoTracking()
            .Where(p => distinct.Contains(p.Id))
            .ToDictionaryAsync(
                p => p.Id,
                p => new ProductTrackingInfo(p.TrackingMode, p.HasExpiryTracking),
                cancellationToken);
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
