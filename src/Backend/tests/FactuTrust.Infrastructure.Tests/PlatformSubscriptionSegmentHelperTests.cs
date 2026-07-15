using FactuTrust.Application.Common;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests;

public sealed class PlatformSubscriptionSegmentHelperTests
{
    [Theory]
    [InlineData(SubscriptionPlan.Monthly, SubscriptionStatus.Active, true)]
    [InlineData(SubscriptionPlan.Annual, SubscriptionStatus.Trial, true)]
    [InlineData(SubscriptionPlan.Monthly, SubscriptionStatus.Cancelled, false)]
    [InlineData(SubscriptionPlan.Free, SubscriptionStatus.Active, false)]
    [InlineData(SubscriptionPlan.Annual, SubscriptionStatus.Expired, false)]
    public void IsPayingSubscriber_matches_platform_rule(SubscriptionPlan plan, SubscriptionStatus status, bool expected)
    {
        var actual = PlatformSubscriptionSegmentHelper.IsPayingSubscriber(plan, status);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void IsPayingSubscriber_null_plan_or_status_is_false()
    {
        Assert.False(PlatformSubscriptionSegmentHelper.IsPayingSubscriber(null, SubscriptionStatus.Active));
        Assert.False(PlatformSubscriptionSegmentHelper.IsPayingSubscriber(SubscriptionPlan.Monthly, null));
        Assert.False(PlatformSubscriptionSegmentHelper.IsPayingSubscriber(null, null));
    }
}
