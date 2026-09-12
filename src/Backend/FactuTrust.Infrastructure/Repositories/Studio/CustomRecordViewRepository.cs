using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories.Studio;

/// <summary>Vues enregistrées (PR 2.3) — patron <see cref="CustomViewRepository"/> (contexte court par appel).</summary>
public sealed class CustomRecordViewRepository : ICustomRecordViewRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public CustomRecordViewRepository(ITenantDbContextFactory contextFactory) => _contextFactory = contextFactory;

    public async Task<IReadOnlyList<CustomRecordViewDefinition>> ListByEntityAsync(
        Guid tenantId, Guid entityDefinitionId, bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.CustomRecordViewDefinitions
            .Where(v => v.TenantId == tenantId && v.EntityDefinitionId == entityDefinitionId && (includeInactive || v.IsActive))
            .OrderBy(v => v.DisplayName)
            .ToListAsync(cancellationToken);
    }

    public async Task<CustomRecordViewDefinition?> GetByIdAsync(
        Guid tenantId, Guid entityDefinitionId, Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.CustomRecordViewDefinitions
            .FirstOrDefaultAsync(v => v.TenantId == tenantId && v.EntityDefinitionId == entityDefinitionId && v.Id == id, cancellationToken);
    }

    public async Task<bool> KeyExistsAsync(
        Guid tenantId, Guid entityDefinitionId, string key, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.CustomRecordViewDefinitions
            .AnyAsync(v => v.TenantId == tenantId && v.EntityDefinitionId == entityDefinitionId && v.Key == key
                && (excludeId == null || v.Id != excludeId.Value), cancellationToken);
    }

    public async Task<int> CountByEntityAsync(Guid tenantId, Guid entityDefinitionId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.CustomRecordViewDefinitions
            .CountAsync(v => v.TenantId == tenantId && v.EntityDefinitionId == entityDefinitionId, cancellationToken);
    }

    public async Task ClearDefaultAsync(Guid tenantId, Guid entityDefinitionId, CancellationToken cancellationToken = default)
    {
        // Chargement + SaveChanges unique (compatible avec EnableRetryOnFailure ; pas d'ExecuteUpdate isolé).
        await using var context = _contextFactory.CreateContext();
        var defaults = await context.CustomRecordViewDefinitions
            .Where(v => v.TenantId == tenantId && v.EntityDefinitionId == entityDefinitionId && v.IsDefault)
            .ToListAsync(cancellationToken);
        foreach (var view in defaults)
            view.SetDefault(false, null);
        if (defaults.Count > 0)
            await context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddAsync(CustomRecordViewDefinition view, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.CustomRecordViewDefinitions.Add(view);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(CustomRecordViewDefinition view, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.CustomRecordViewDefinitions.Update(view);
        await context.SaveChangesAsync(cancellationToken);
    }
}
