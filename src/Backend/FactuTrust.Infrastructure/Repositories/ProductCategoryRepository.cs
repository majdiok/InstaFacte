using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Constants;
using FactuTrust.Domain.Entities;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for ProductCategory.
/// </summary>
public sealed class ProductCategoryRepository : IProductCategoryRepository
{
    private readonly TenantDbContextFactory _contextFactory;

    public ProductCategoryRepository(TenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<ProductCategory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.ProductCategories
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<ProductCategory?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.ProductCategories
            .FirstOrDefaultAsync(c => c.Code == code.ToUpperInvariant(), cancellationToken);
    }

    public async Task<IReadOnlyList<ProductCategory>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.ProductCategories
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProductCategory>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.ProductCategories
            .Where(c => c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Guid> GetDefaultCategoryIdAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var category = await context.ProductCategories
            .FirstOrDefaultAsync(c => c.Code == ProductCategory.DefaultCode, cancellationToken);

        if (category is not null)
            return category.Id;

        return ProductCategoryConstants.DefaultCategoryId;
    }

    public async Task<ProductCategory> AddAsync(ProductCategory entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.ProductCategories.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(ProductCategory entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.ProductCategories.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.ProductCategories.AnyAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.ProductCategories.AnyAsync(c => c.Code == code.ToUpperInvariant(), cancellationToken);
    }
}
