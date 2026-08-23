using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>Platform-admin CRUD for a tenant's Modal (Kimi) endpoint override.</summary>
public interface ITenantModalSettingsService
{
    Task<Result<TenantModalSettingsDto>> GetAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<Result<TenantModalSettingsDto>> SetAsync(
        Guid tenantId,
        UpdateTenantModalSettingsRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes the override so the tenant inherits platform Modal settings again.</summary>
    Task<Result<TenantModalSettingsDto>> DeleteAsync(
        Guid tenantId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);
}
