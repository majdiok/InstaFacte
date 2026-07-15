using FactuTrust.Application.DTOs;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// Lecture du résumé société du tenant courant (accessible avec accounting:read, sans settings:read).
/// </summary>
public interface ITenantCompanySummaryProvider
{
    Task<TenantCompanySummaryDto?> GetCurrentTenantSummaryAsync(CancellationToken cancellationToken = default);
}
