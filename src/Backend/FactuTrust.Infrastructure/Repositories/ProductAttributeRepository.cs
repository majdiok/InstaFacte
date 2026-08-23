using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class ProductAttributeRepository : IProductAttributeRepository
{
    private readonly ITenantDbContextFactory _factory;

    public ProductAttributeRepository(ITenantDbContextFactory factory)
    {
        _factory = factory;
    }

    public async Task<ProductAttributeDefinition?> GetByIdWithValuesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _factory.CreateContext();
        return await context.ProductAttributeDefinitions
            .Include(d => d.Values)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<ProductAttributeDefinition>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _factory.CreateContext();
        return await context.ProductAttributeDefinitions
            .Include(d => d.Values)
            .OrderBy(d => d.SortOrder)
            .ThenBy(d => d.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(ProductAttributeDefinition definition, CancellationToken cancellationToken = default)
    {
        await using var context = _factory.CreateContext();
        context.ProductAttributeDefinitions.Add(definition);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(ProductAttributeDefinition definition, CancellationToken cancellationToken = default)
    {
        await using var context = _factory.CreateContext();
        context.ProductAttributeDefinitions.Update(definition);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(ProductAttributeDefinition definition, CancellationToken cancellationToken = default)
    {
        await using var context = _factory.CreateContext();
        context.ProductAttributeDefinitions.Remove(definition);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ProductAttributeDefinition?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var normalized = code.Trim().ToUpperInvariant();
        await using var context = _factory.CreateContext();
        return await context.ProductAttributeDefinitions
            .Include(d => d.Values)
            .FirstOrDefaultAsync(d => d.Code == normalized, cancellationToken);
    }

    public async Task<bool> IsDefinitionInUseAsync(Guid definitionId, CancellationToken cancellationToken = default)
    {
        await using var context = _factory.CreateContext();
        return await context.ProductVariantAxes.AnyAsync(a => a.DefinitionId == definitionId, cancellationToken);
    }

    public async Task<bool> IsValueInUseAsync(Guid valueId, CancellationToken cancellationToken = default)
    {
        await using var context = _factory.CreateContext();
        return await context.ProductVariantAttributeValues.AnyAsync(v => v.AttributeValueId == valueId, cancellationToken);
    }

    public async Task AddAxisAsync(ProductVariantAxis axis, CancellationToken cancellationToken = default)
    {
        await using var context = _factory.CreateContext();
        context.ProductVariantAxes.Add(axis);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddVariantLinkAsync(ProductVariantAttributeValue link, CancellationToken cancellationToken = default)
    {
        await using var context = _factory.CreateContext();
        context.ProductVariantAttributeValues.Add(link);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProductVariantAxis>> ListAxesAsync(Guid parentProductId, CancellationToken cancellationToken = default)
    {
        await using var context = _factory.CreateContext();
        return await context.ProductVariantAxes
            .Where(a => a.ParentProductId == parentProductId)
            .OrderBy(a => a.SortOrder)
            .ToListAsync(cancellationToken);
    }
}
