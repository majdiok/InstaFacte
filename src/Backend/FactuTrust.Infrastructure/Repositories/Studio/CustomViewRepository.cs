using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories.Studio;

public sealed class CustomViewRepository : ICustomViewRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public CustomViewRepository(ITenantDbContextFactory contextFactory) => _contextFactory = contextFactory;

    public async Task<IReadOnlyList<CustomViewDefinition>> ListAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.CustomViewDefinitions
            .Where(v => v.TenantId == tenantId && v.IsActive)
            .OrderBy(v => v.DisplayName)
            .ToListAsync(cancellationToken);
    }

    public async Task<CustomViewDefinition?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.CustomViewDefinitions
            .FirstOrDefaultAsync(v => v.TenantId == tenantId && v.Id == id, cancellationToken);
    }

    public async Task<bool> KeyExistsAsync(Guid tenantId, string key, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.CustomViewDefinitions.AnyAsync(v => v.TenantId == tenantId && v.Key == key, cancellationToken);
    }

    public async Task AddAsync(CustomViewDefinition view, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.CustomViewDefinitions.Add(view);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(CustomViewDefinition view, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.CustomViewDefinitions.Update(view);
        await context.SaveChangesAsync(cancellationToken);
    }
}
