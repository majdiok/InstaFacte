using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>Lot C1 — Service CRUD plans plateforme (admin /plans).</summary>
public interface IPlanAdminService
{
    Task<IReadOnlyList<PlanDto>> ListAsync(bool includeArchived, CancellationToken cancellationToken = default);

    Task<Result<PlanDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<PlanDto>> CreateAsync(CreatePlanRequest request, CancellationToken cancellationToken = default);

    Task<Result<PlanDto>> UpdateAsync(Guid id, UpdatePlanRequest request, CancellationToken cancellationToken = default);

    /// <summary>Archive le plan (suppression logique). Empêché si tenants l'utilisent.</summary>
    Task<Result> ArchiveAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result> ReactivateAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<PlanDto>> CloneAsync(Guid sourceId, ClonePlanRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Lot C1 — Service de gestion des overrides de modules par tenant.</summary>
public interface ITenantModuleOverrideService
{
    Task<IReadOnlyList<TenantModuleOverrideDto>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<Result<TenantModuleOverrideDto>> SetAsync(
        Guid tenantId,
        SetTenantModuleOverrideRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<bool> RemoveAsync(Guid overrideId, CancellationToken cancellationToken = default);

    /// <summary>Purge les overrides expirés (appelé par le job Hangfire quotidien).</summary>
    Task<int> RemoveExpiredAsync(CancellationToken cancellationToken = default);
}
