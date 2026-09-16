using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.Studio.Workflows;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories.Studio;

/// <summary>
/// Dépôt SQL des workflows Studio (PR 4.1c) — patron <see cref="CustomRecordViewRepository"/>
/// (contexte court par appel via <see cref="ITenantDbContextFactory.CreateContext"/>).
/// Chaque requête filtre sur <c>TenantId</c> ; les définitions supprimées sont masquées par le
/// filtre global <c>!IsDeleted</c> du contexte. Aucune exécution parallèle de requêtes
/// (garde-fou concurrence sur le contexte).
/// </summary>
public sealed class StudioWorkflowRepository : IStudioWorkflowRepository
{
    /// <summary>Statuts « ouverts » d'une instance : en cours ou en attente (job différé / approbation).</summary>
    private static readonly StudioWorkflowInstanceStatus[] OpenStatuses =
    {
        StudioWorkflowInstanceStatus.Running,
        StudioWorkflowInstanceStatus.Waiting,
        StudioWorkflowInstanceStatus.WaitingApproval
    };

    private readonly ITenantDbContextFactory _contextFactory;

    public StudioWorkflowRepository(ITenantDbContextFactory contextFactory) => _contextFactory = contextFactory;

    public async Task<IReadOnlyList<StudioWorkflowDefinition>> ListByEntityAsync(
        Guid tenantId, Guid entityDefinitionId, bool includeInactive = true, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.StudioWorkflowDefinitions
            .Where(d => d.TenantId == tenantId && d.EntityDefinitionId == entityDefinitionId && (includeInactive || d.IsActive))
            .OrderBy(d => d.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StudioWorkflowDefinition>> ListActiveByTriggerAsync(
        Guid tenantId, Guid entityDefinitionId, StudioWorkflowTriggerKind trigger, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.StudioWorkflowDefinitions
            .Where(d => d.TenantId == tenantId && d.EntityDefinitionId == entityDefinitionId && d.IsActive && d.Trigger == trigger)
            .OrderBy(d => d.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<StudioWorkflowDefinition?> GetDefinitionAsync(
        Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.StudioWorkflowDefinitions
            .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Id == id, cancellationToken);
    }

    public async Task<StudioWorkflowDefinition?> GetDefinitionByKeyAsync(
        Guid tenantId, Guid entityDefinitionId, string key, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.StudioWorkflowDefinitions
            .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.EntityDefinitionId == entityDefinitionId && d.Key == key, cancellationToken);
    }

    public async Task<int> CountByEntityAsync(Guid tenantId, Guid entityDefinitionId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.StudioWorkflowDefinitions
            .CountAsync(d => d.TenantId == tenantId && d.EntityDefinitionId == entityDefinitionId, cancellationToken);
    }

    public async Task AddDefinitionAsync(StudioWorkflowDefinition definition, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.StudioWorkflowDefinitions.Add(definition);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateDefinitionWithConcurrencyAsync(
        StudioWorkflowDefinition definition, byte[]? expectedRowVersion, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.Attach(definition);
        context.Entry(definition).State = EntityState.Modified;
        if (expectedRowVersion is { Length: > 0 })
        {
            // Use the client's RowVersion as the concurrency token → EF throws DbUpdateConcurrencyException
            // if the row changed since the client loaded it (mapped to HTTP 409 by the middleware).
            context.Entry(definition).Property(d => d.RowVersion).OriginalValue = expectedRowVersion;
        }
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<StudioWorkflowInstance?> GetInstanceAsync(
        Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.StudioWorkflowInstances
            .FirstOrDefaultAsync(i => i.TenantId == tenantId && i.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<StudioWorkflowInstance>> ListInstancesForRecordAsync(
        Guid tenantId, Guid recordId, int max, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.StudioWorkflowInstances
            .Where(i => i.TenantId == tenantId && i.RecordId == recordId)
            .OrderByDescending(i => i.StartedAt)
            .Take(Math.Clamp(max, 1, 200))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StudioWorkflowInstance>> ListInstancesForDefinitionAsync(
        Guid tenantId, Guid definitionId, int max, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.StudioWorkflowInstances
            .Where(i => i.TenantId == tenantId && i.WorkflowDefinitionId == definitionId)
            .OrderByDescending(i => i.StartedAt)
            .Take(Math.Clamp(max, 1, 200))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StudioWorkflowInstance>> ListOpenInstancesForDefinitionAsync(
        Guid tenantId, Guid definitionId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.StudioWorkflowInstances
            .Where(i => i.TenantId == tenantId && i.WorkflowDefinitionId == definitionId && OpenStatuses.Contains(i.Status))
            .OrderByDescending(i => i.StartedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountInstancesForRecordAsync(
        Guid tenantId, Guid recordId, bool openOnly, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.StudioWorkflowInstances
            .CountAsync(i => i.TenantId == tenantId && i.RecordId == recordId && (!openOnly || OpenStatuses.Contains(i.Status)), cancellationToken);
    }

    public async Task<bool> HasOpenInstanceInChainAsync(
        Guid tenantId, Guid definitionId, Guid recordId, Guid? originInstanceId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.StudioWorkflowInstances
            .AnyAsync(i => i.TenantId == tenantId && i.WorkflowDefinitionId == definitionId && i.RecordId == recordId
                && OpenStatuses.Contains(i.Status)
                && (originInstanceId == null || i.OriginInstanceId == originInstanceId || i.Id == originInstanceId), cancellationToken);
    }

    public async Task AddInstanceAsync(StudioWorkflowInstance instance, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.StudioWorkflowInstances.Add(instance);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateInstanceAsync(StudioWorkflowInstance instance, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.StudioWorkflowInstances.Update(instance);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddStepRunAsync(StudioWorkflowStepRun stepRun, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.StudioWorkflowStepRuns.Add(stepRun);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StudioWorkflowStepRun>> ListStepRunsAsync(
        Guid tenantId, Guid instanceId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.StudioWorkflowStepRuns
            .Where(r => r.TenantId == tenantId && r.InstanceId == instanceId)
            .OrderBy(r => r.StepIndex)
            .ThenBy(r => r.StartedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task AddApprovalAsync(StudioWorkflowApproval approval, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.StudioWorkflowApprovals.Add(approval);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<StudioWorkflowApproval?> GetApprovalAsync(
        Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.StudioWorkflowApprovals
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<StudioWorkflowApproval>> ListPendingApprovalsForInstanceAsync(
        Guid tenantId, Guid instanceId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.StudioWorkflowApprovals
            .Where(a => a.TenantId == tenantId && a.InstanceId == instanceId && a.Status == StudioWorkflowApprovalStatus.Pending)
            .OrderBy(a => a.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateApprovalAsync(StudioWorkflowApproval approval, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.StudioWorkflowApprovals.Update(approval);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> CountOpenInstancesForDefinitionAsync(
        Guid tenantId, Guid definitionId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.StudioWorkflowInstances
            .CountAsync(i => i.TenantId == tenantId && i.WorkflowDefinitionId == definitionId && OpenStatuses.Contains(i.Status), cancellationToken);
    }
}
