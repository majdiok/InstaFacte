using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// Resolves the subscription plan for a tenant (Master DB).
/// Used to enforce quote/invoice limits per plan.
/// </summary>
public interface ISubscriptionResolver
{
    /// <summary>
    /// Gets the subscription plan for the given tenant.
    /// Returns <see cref="SubscriptionPlan.Free"/> if no subscription is found.
    /// </summary>
    Task<SubscriptionPlan> GetPlanForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
