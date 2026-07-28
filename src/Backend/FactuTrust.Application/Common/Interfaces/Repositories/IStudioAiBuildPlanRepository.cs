using FactuTrust.Domain.Entities.Studio;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface IStudioAiBuildPlanRepository
{
    Task<StudioAiBuildPlan?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(StudioAiBuildPlan plan, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persiste les changements du plan. Retourne <c>false</c> sur un conflit de concurrence
    /// optimiste (RowVersion) — c'est le verrou anti double-confirmation, jamais une exception.
    /// </summary>
    Task<bool> TryUpdateAsync(StudioAiBuildPlan plan, CancellationToken cancellationToken = default);
}
