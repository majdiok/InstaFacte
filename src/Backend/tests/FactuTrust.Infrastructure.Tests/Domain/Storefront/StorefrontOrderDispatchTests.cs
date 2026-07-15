using FactuTrust.Domain.Entities.Storefront;
using FactuTrust.Domain.Events;
using FactuTrust.Domain.ValueObjects;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain.Storefront;

/// <summary>
/// Validates the <see cref="StorefrontOrder"/> aggregate: creation invariants, item addition,
/// tenant aggregation and state-machine transitions around the multi-tenant dispatch workflow.
/// </summary>
public sealed class StorefrontOrderDispatchTests
{
    private static Address ValidAddress() => Address.Create(
        street: "12 Avenue Habib Bourguiba",
        city: "Tunis",
        governorate: "Tunis",
        streetLine2: null,
        postalCode: "1000",
        country: "Tunisie").Value;

    private static StorefrontOrder CreateDefaultOrder()
    {
        var result = StorefrontOrder.Create(
            guestFullName: "Jane Doe",
            guestEmail: "jane@example.com",
            guestPhone: "+216 20 000 000",
            guestDeliveryAddress: ValidAddress(),
            guestNotes: "Livraison rapide svp",
            ipAddressHash: "abc",
            userAgentHash: "def");

        Assert.True(result.IsSuccess, result.Error?.Description);
        return result.Value;
    }

    [Fact]
    public void Create_ShouldInitializeInSubmittedState()
    {
        var order = CreateDefaultOrder();

        Assert.Equal(StorefrontOrderStatus.Submitted, order.Status);
        Assert.Equal("jane@example.com", order.GuestEmail);
        Assert.NotEqual(default, order.SubmittedAt);
    }

    [Theory]
    [InlineData("", "jane@example.com", "+216 20 000 000")]
    [InlineData("Jane", "", "+216 20 000 000")]
    [InlineData("Jane", "jane@example.com", "")]
    public void Create_ShouldFail_WhenMandatoryFieldIsMissing(string name, string email, string phone)
    {
        var result = StorefrontOrder.Create(
            guestFullName: name,
            guestEmail: email,
            guestPhone: phone,
            guestDeliveryAddress: ValidAddress(),
            guestNotes: null,
            ipAddressHash: null,
            userAgentHash: null);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void AddItem_ShouldAggregateMultipleTenants()
    {
        var order = CreateDefaultOrder();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        Assert.True(order.AddItem(Guid.NewGuid(), tenantA, Guid.NewGuid(), "Prod A", 1m, 10m, "TND").IsSuccess);
        Assert.True(order.AddItem(Guid.NewGuid(), tenantA, Guid.NewGuid(), "Prod A2", 2m, 5m, "TND").IsSuccess);
        Assert.True(order.AddItem(Guid.NewGuid(), tenantB, Guid.NewGuid(), "Prod B", 1m, 20m, "TND").IsSuccess);

        Assert.Equal(3, order.Items.Count);
        var involvedTenants = order.GetInvolvedTenantIds();
        Assert.Equal(2, involvedTenants.Count);
        Assert.Contains(tenantA, involvedTenants);
        Assert.Contains(tenantB, involvedTenants);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddItem_ShouldReject_NonPositiveQuantity(decimal quantity)
    {
        var order = CreateDefaultOrder();

        var result = order.AddItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Prod", quantity, 10m, "TND");

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void AddItem_ShouldReject_NegativePrice()
    {
        var order = CreateDefaultOrder();

        var result = order.AddItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Prod", 1m, -0.01m, "TND");

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void MarkDispatched_ThenConfirmed_ShouldPopulateTimestamps()
    {
        var order = CreateDefaultOrder();

        order.MarkDispatched("{\"t\":\"ok\"}");
        Assert.Equal(StorefrontOrderStatus.DispatchedToTenants, order.Status);
        Assert.NotNull(order.DispatchedAt);

        order.MarkConfirmed("{\"t\":\"confirmed\"}");
        Assert.Equal(StorefrontOrderStatus.Confirmed, order.Status);
        Assert.NotNull(order.ConfirmedAt);
    }

    [Fact]
    public void MarkPartiallyConfirmed_ShouldNotSetConfirmedAt()
    {
        var order = CreateDefaultOrder();

        order.MarkPartiallyConfirmed("{\"t1\":\"ok\",\"t2\":\"failed\"}");

        Assert.Equal(StorefrontOrderStatus.PartiallyConfirmed, order.Status);
        Assert.Null(order.ConfirmedAt);
    }

    [Fact]
    public void MarkFailed_ShouldRecordPayload()
    {
        var order = CreateDefaultOrder();

        order.MarkFailed("{\"all\":\"failed\"}");

        Assert.Equal(StorefrontOrderStatus.Failed, order.Status);
        Assert.Equal("{\"all\":\"failed\"}", order.TenantDispatchResultsJson);
    }

    [Fact]
    public void MarkCancelled_ShouldSetCancelledAt()
    {
        var order = CreateDefaultOrder();

        order.MarkCancelled();

        Assert.Equal(StorefrontOrderStatus.Cancelled, order.Status);
        Assert.NotNull(order.CancelledAt);
    }

    [Fact]
    public void RaiseSubmittedEvent_ShouldEmitEventWithInvolvedTenants()
    {
        var order = CreateDefaultOrder();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        order.AddItem(Guid.NewGuid(), tenantA, Guid.NewGuid(), "A", 1m, 10m, "TND");
        order.AddItem(Guid.NewGuid(), tenantB, Guid.NewGuid(), "B", 1m, 10m, "TND");

        order.RaiseSubmittedEvent();

        var ev = Assert.Single(order.DomainEvents);
        var submitted = Assert.IsType<PublicOrderSubmittedEvent>(ev);
        Assert.Equal(order.Id, submitted.StorefrontOrderId);
        Assert.Equal(2, submitted.InvolvedTenantIds.Count);
    }
}
