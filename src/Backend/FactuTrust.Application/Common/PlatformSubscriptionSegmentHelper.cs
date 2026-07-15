using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common;

/// <summary>
/// Paying subscriber: Monthly or Annual plan with Active or Trial status (platform backoffice definition).
/// </summary>
public static class PlatformSubscriptionSegmentHelper
{
    public static bool IsPayingSubscriber(SubscriptionPlan? plan, SubscriptionStatus? status)
    {
        if (plan is null || status is null)
            return false;

        var payingPlan = plan is SubscriptionPlan.Monthly or SubscriptionPlan.Annual;
        var usableStatus = status is SubscriptionStatus.Active or SubscriptionStatus.Trial;
        return payingPlan && usableStatus;
    }
}
