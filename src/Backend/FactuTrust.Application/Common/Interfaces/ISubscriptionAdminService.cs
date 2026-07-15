using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// Changes tenant subscriptions (used by tenant settings API and platform admin API).
/// </summary>
public interface ISubscriptionAdminService
{
    /// <summary>
    /// If no subscription row exists, creates a free subscription first, then applies the plan change.
    /// </summary>
    Task<Result<Subscription>> ChangePlanAsync(Guid tenantId, SubscriptionPlan newPlan, CancellationToken cancellationToken = default);

    Task<Result<Subscription>> CancelAsync(Guid tenantId, string reason, CancellationToken cancellationToken = default);
}
