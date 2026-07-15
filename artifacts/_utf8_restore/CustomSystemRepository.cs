using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories.Studio;

public sealed class CustomSystemRepository : ICustomSystemRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public CustomSystemRepository(ITenantDbContextFactory contextFactory) => _contextFactory = contextFactory;

    public async Task<CustomSystemDefinition?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.CustomSystemDefinitions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == id, cancellationToken);
    }

    public async Task<CustomSystemDefinition?> GetByKeyAsync(Guid tenantId, string key, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.CustomSystemDefinitions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Key == key, cancellationToken);
    }

    public async Task<IReadOnlyList<CustomSystemDefinition>> ListAsync(Guid tenantId, bool includeInactive, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var query = context.CustomSystemDefinitions.Where(s => s.TenantId == tenantId);
        if (!includeInactive)
            query = query.Where(s => s.IsActive);
        return await query.OrderBy(s => s.DisplayName).ToListAsync(cancellationToken);
    }

    public async Task<bool> KeyExistsAsync(Guid tenantId, string key, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.CustomSystemDefinitions.AnyAsync(s => s.TenantId == tenantId && s.Key == key, cancellationToken);
    }

    public async Task AddAsync(CustomSystemDefinition system, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.CustomSystemDefinitions.Add(system);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(CustomSystemDefinition system, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.CustomSystemDefinitions.Update(system);
        await context.SaveChangesAsync(cancellationToken);
    }
}
