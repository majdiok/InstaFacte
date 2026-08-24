using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
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

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<ProductVariantAttributePairDto>>> GetVariantAttributesByProductIdsAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken = default)
    {
        if (productIds.Count == 0)
            return new Dictionary<Guid, IReadOnlyList<ProductVariantAttributePairDto>>();

        await using var context = _factory.CreateContext();

        var rows = await (
            from link in context.ProductVariantAttributeValues
            join value in context.ProductAttributeValues on link.AttributeValueId equals value.Id
            join definition in context.ProductAttributeDefinitions on value.DefinitionId equals definition.Id
            where productIds.Contains(link.ProductId)
            orderby definition.SortOrder, value.SortOrder
            select new
            {
                link.ProductId,
                DefinitionId = definition.Id,
                DefinitionCode = definition.Code,
                DefinitionName = definition.Name,
                DefinitionSort = definition.SortOrder,
                ValueId = value.Id,
                ValueCode = value.Code,
                ValueName = value.Name,
                ValueSort = value.SortOrder
            }).ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.ProductId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<ProductVariantAttributePairDto>)g.Select(r => new ProductVariantAttributePairDto
                {
                    DefinitionId = r.DefinitionId,
                    DefinitionCode = r.DefinitionCode,
                    DefinitionName = r.DefinitionName,
                    ValueId = r.ValueId,
                    ValueCode = r.ValueCode,
                    ValueName = r.ValueName,
                    SortOrder = r.DefinitionSort * 1000 + r.ValueSort
                }).ToList());
    }
}
