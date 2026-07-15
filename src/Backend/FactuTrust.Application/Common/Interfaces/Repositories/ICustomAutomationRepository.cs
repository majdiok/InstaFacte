using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

/// <summary>Persistence for Studio ERP-bridge automations and their execution log.</summary>
public interface ICustomAutomationRepository
{
    Task<IReadOnlyList<CustomEntityAutomation>> ListByEntityAsync(Guid tenantId, Guid entityDefinitionId, bool includeInactive, CancellationToken cancellationToken = default);

    /// <summary>Active automations for an entity matching a lifecycle trigger (used by the bridge handler).</summary>
    Task<IReadOnlyList<CustomEntityAutomation>> ListActiveByTriggerAsync(Guid tenantId, Guid entityDefinitionId, StudioAutomationTrigger trigger, CancellationToken cancellationToken = default);

    Task<CustomEntityAutomation?> GetAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(CustomEntityAutomation automation, CancellationToken cancellationToken = default);
    Task UpdateAsync(CustomEntityAutomation automation, CancellationToken cancellationToken = default);
    Task DeleteAsync(CustomEntityAutomation automation, CancellationToken cancellationToken = default);

    /// <summary>True if this automation already ran successfully for the record (RunOnce idempotency guard).</summary>
    Task<bool> HasSuccessfulRunAsync(Guid tenantId, Guid automationId, Guid recordId, CancellationToken cancellationToken = default);

    Task AddRunAsync(CustomAutomationRun run, CancellationToken cancellationToken = default);

    /// <summary>The most recent runs for a record (for link-back / status display).</summary>
    Task<IReadOnlyList<CustomAutomationRun>> ListRunsForRecordAsync(Guid tenantId, Guid recordId, int max, CancellationToken cancellationToken = default);
}
