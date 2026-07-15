using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories.Studio;

public sealed class CustomFieldRepository : ICustomFieldRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public CustomFieldRepository(ITenantDbContextFactory contextFactory) => _contextFactory = contextFactory;

    public async Task<IReadOnlyList<CustomFieldDefinition>> ListByEntityAsync(Guid tenantId, Guid entityDefinitionId, bool includeInactive, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var query = context.CustomFieldDefinitions
            .Where(f => f.TenantId == tenantId && f.EntityDefinitionId == entityDefinitionId);
        if (!includeInactive)
            query = query.Where(f => f.IsActive);
        return await query.OrderBy(f => f.SortOrder).ToListAsync(cancellationToken);
    }

    public async Task<CustomFieldDefinition?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.CustomFieldDefinitions
            .FirstOrDefaultAsync(f => f.TenantId == tenantId && f.Id == id, cancellationToken);
    }

    public async Task<bool> KeyExistsAsync(Guid tenantId, Guid entityDefinitionId, string key, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.CustomFieldDefinitions
            .AnyAsync(f => f.TenantId == tenantId && f.EntityDefinitionId == entityDefinitionId && f.Key == key, cancellationToken);
    }

    public async Task<int> CountByEntityAsync(Guid tenantId, Guid entityDefinitionId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.CustomFieldDefinitions
            .CountAsync(f => f.TenantId == tenantId && f.EntityDefinitionId == entityDefinitionId && f.IsActive, cancellationToken);
    }

    public async Task<int> MaxSortOrderAsync(Guid tenantId, Guid entityDefinitionId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var query = context.CustomFieldDefinitions
            .Where(f => f.TenantId == tenantId && f.EntityDefinitionId == entityDefinitionId);
        if (!await query.AnyAsync(cancellationToken))
            return -1;
        return await query.MaxAsync(f => f.SortOrder, cancellationToken);
    }

    public async Task AddAsync(CustomFieldDefinition field, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.CustomFieldDefinitions.Add(field);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(CustomFieldDefinition field, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.CustomFieldDefinitions.Update(field);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateRangeAsync(IReadOnlyList<CustomFieldDefinition> fields, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.CustomFieldDefinitions.UpdateRange(fields);
        await context.SaveChangesAsync(cancellationToken);
    }
}
