using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories.Studio;

public sealed class CustomEntityRepository : ICustomEntityRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public CustomEntityRepository(ITenantDbContextFactory contextFactory) => _contextFactory = contextFactory;

    public async Task<CustomEntityDefinition?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.CustomEntityDefinitions
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == id, cancellationToken);
    }

    public async Task<CustomEntityDefinition?> GetByKeyAsync(Guid tenantId, string key, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.CustomEntityDefinitions
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Key == key, cancellationToken);
    }

    public async Task<IReadOnlyList<CustomEntityDefinition>> ListAsync(Guid tenantId, bool includeInactive, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var query = context.CustomEntityDefinitions.Where(e => e.TenantId == tenantId);
        if (!includeInactive)
            query = query.Where(e => e.IsActive);
        return await query.OrderBy(e => e.DisplayName).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CustomEntityDefinition>> ListBySystemIdAsync(Guid tenantId, Guid systemId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.CustomEntityDefinitions
            .Where(e => e.TenantId == tenantId && e.SystemId == systemId && e.IsActive)
            .OrderBy(e => e.DisplayName)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> KeyExistsAsync(Guid tenantId, string key, bool includeDeleted = false, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        IQueryable<CustomEntityDefinition> query = context.CustomEntityDefinitions;
        if (includeDeleted)
            query = query.IgnoreQueryFilters(); // drops the global !IsDeleted filter only; TenantId stays in the predicate below
        return await query.AnyAsync(e => e.TenantId == tenantId && e.Key == key, cancellationToken);
    }

    public async Task<int> CountAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.CustomEntityDefinitions.CountAsync(e => e.TenantId == tenantId, cancellationToken);
    }

    public async Task AddAsync(CustomEntityDefinition entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.CustomEntityDefinitions.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(CustomEntityDefinition entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.CustomEntityDefinitions.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }
}
