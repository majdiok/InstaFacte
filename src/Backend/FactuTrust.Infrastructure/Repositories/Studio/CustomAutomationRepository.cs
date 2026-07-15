using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories.Studio;

public sealed class CustomAutomationRepository : ICustomAutomationRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public CustomAutomationRepository(ITenantDbContextFactory contextFactory) => _contextFactory = contextFactory;

    public async Task<IReadOnlyList<CustomEntityAutomation>> ListByEntityAsync(
        Guid tenantId, Guid entityDefinitionId, bool includeInactive, CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();
        var query = ctx.CustomEntityAutomations
            .Where(a => a.TenantId == tenantId && a.EntityDefinitionId == entityDefinitionId);
        if (!includeInactive)
            query = query.Where(a => a.IsActive);
        return await query.OrderBy(a => a.Name).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CustomEntityAutomation>> ListActiveByTriggerAsync(
        Guid tenantId, Guid entityDefinitionId, StudioAutomationTrigger trigger, CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();
        return await ctx.CustomEntityAutomations.AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.EntityDefinitionId == entityDefinitionId && a.IsActive && a.Trigger == trigger)
            .ToListAsync(cancellationToken);
    }

    public async Task<CustomEntityAutomation?> GetAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();
        return await ctx.CustomEntityAutomations
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == id, cancellationToken);
    }

    public async Task AddAsync(CustomEntityAutomation automation, CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();
        ctx.CustomEntityAutomations.Add(automation);
        await ctx.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(CustomEntityAutomation automation, CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();
        ctx.CustomEntityAutomations.Update(automation);
        await ctx.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(CustomEntityAutomation automation, CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();
        ctx.CustomEntityAutomations.Remove(automation);
        await ctx.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> HasSuccessfulRunAsync(Guid tenantId, Guid automationId, Guid recordId, CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();
        return await ctx.CustomAutomationRuns.AsNoTracking()
            .AnyAsync(r => r.TenantId == tenantId && r.AutomationId == automationId
                && r.RecordId == recordId && r.Status == StudioAutomationRunStatus.Success, cancellationToken);
    }

    public async Task AddRunAsync(CustomAutomationRun run, CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();
        ctx.CustomAutomationRuns.Add(run);
        await ctx.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CustomAutomationRun>> ListRunsForRecordAsync(Guid tenantId, Guid recordId, int max, CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();
        return await ctx.CustomAutomationRuns.AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.RecordId == recordId)
            .OrderByDescending(r => r.RunAt)
            .Take(max)
            .ToListAsync(cancellationToken);
    }
}
