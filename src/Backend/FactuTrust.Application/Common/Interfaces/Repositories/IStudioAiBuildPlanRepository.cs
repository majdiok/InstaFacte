using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface IStudioAiBuildPlanRepository
{
    Task<StudioAiBuildPlan?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(StudioAiBuildPlan plan, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persiste les changements du plan. Retourne <c>false</c> sur un conflit de concurrence
    /// optimiste (RowVersion) — c'est le verrou anti double-confirmation, jamais une exception.
    /// <paramref name="expectedRowVersion"/> : jeton lu par le client (édition de la spec) ;
    /// quand il est fourni, c'est lui qui sert de jeton de concurrence à la place de celui rechargé.
    /// </summary>
    Task<bool> TryUpdateAsync(StudioAiBuildPlan plan, CancellationToken cancellationToken = default, byte[]? expectedRowVersion = null);

    /// <summary>
    /// Liste paginée des plans d'un utilisateur dans un tenant (historique des générations),
    /// triée par <c>CreatedAt</c> décroissant. Ne fuit jamais un plan d'un autre utilisateur
    /// ou d'un autre tenant, y compris avec <paramref name="status"/>/<paramref name="kind"/> nuls.
    /// </summary>
    Task<(IReadOnlyList<StudioAiBuildPlan> Items, int TotalCount)> ListByOwnerAsync(
        Guid tenantId, string userId, StudioAiPlanStatus? status, StudioAiPlanKind? kind,
        int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Plans <c>Pending</c> NON expirés du couple (tenant, utilisateur) — purge « Réinitialiser la conversation ».</summary>
    Task<IReadOnlyList<StudioAiBuildPlan>> ListPendingByOwnerAsync(Guid tenantId, string userId, CancellationToken cancellationToken = default);
}
