using FactuTrust.Application.DTOs;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>Read-only tenant data for platform (master DB) operators.</summary>
public interface IPlatformTenantQueryService
{
    Task<PlatformTenantListPageDto> ListAsync(PlatformTenantListQuery query, CancellationToken cancellationToken = default);

    Task<PlatformTenantStatsDto> GetStatsAsync(CancellationToken cancellationToken = default);

    Task<PlatformTenantDetailDto?> GetDetailAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
