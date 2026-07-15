using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// Enforces per-tenant plan limits for Studio resources (custom entities/fields/records).
/// Mirrors <c>IPlanQuotaService</c>: tenants without a subscription pass through.
/// </summary>
public interface IStudioQuotaService
{
    /// <summary>
    /// Returns failure if <paramref name="currentCount"/> has reached the plan limit for
    /// <paramref name="limitKey"/> (resolved via the Plans table, falling back to <paramref name="fallback"/>).
    /// </summary>
    Task<Result> EnsureUnderLimitAsync(
        Guid tenantId, string limitKey, int currentCount, int fallback, string label, CancellationToken cancellationToken = default);
}
