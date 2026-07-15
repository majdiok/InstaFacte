using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Events;

public sealed class StorefrontOptInCreatedEvent : DomainEvent
{
    public Guid StorefrontProfileId { get; }
    public Guid TenantId { get; }
    public string Slug { get; }

    public StorefrontOptInCreatedEvent(Guid storefrontProfileId, Guid tenantId, string slug)
    {
        StorefrontProfileId = storefrontProfileId;
        TenantId = tenantId;
        Slug = slug;
    }
}

public sealed class StorefrontSubmittedForReviewEvent : DomainEvent
{
    public Guid StorefrontProfileId { get; }
    public Guid TenantId { get; }

    public StorefrontSubmittedForReviewEvent(Guid storefrontProfileId, Guid tenantId)
    {
        StorefrontProfileId = storefrontProfileId;
        TenantId = tenantId;
    }
}

public sealed class StorefrontPublishedEvent : DomainEvent
{
    public Guid StorefrontProfileId { get; }
    public Guid TenantId { get; }
    public string Slug { get; }

    public StorefrontPublishedEvent(Guid storefrontProfileId, Guid tenantId, string slug)
    {
        StorefrontProfileId = storefrontProfileId;
        TenantId = tenantId;
        Slug = slug;
    }
}

public sealed class StorefrontRejectedEvent : DomainEvent
{
    public Guid StorefrontProfileId { get; }
    public Guid TenantId { get; }
    public string Reason { get; }

    public StorefrontRejectedEvent(Guid storefrontProfileId, Guid tenantId, string reason)
    {
        StorefrontProfileId = storefrontProfileId;
        TenantId = tenantId;
        Reason = reason;
    }
}

public sealed class StorefrontSuspendedEvent : DomainEvent
{
    public Guid StorefrontProfileId { get; }
    public Guid TenantId { get; }
    public string Reason { get; }

    public StorefrontSuspendedEvent(Guid storefrontProfileId, Guid tenantId, string reason)
    {
        StorefrontProfileId = storefrontProfileId;
        TenantId = tenantId;
        Reason = reason;
    }
}

public sealed class StorefrontUnpublishedEvent : DomainEvent
{
    public Guid StorefrontProfileId { get; }
    public Guid TenantId { get; }

    public StorefrontUnpublishedEvent(Guid storefrontProfileId, Guid tenantId)
    {
        StorefrontProfileId = storefrontProfileId;
        TenantId = tenantId;
    }
}

public sealed class StorefrontProfileUpdatedEvent : DomainEvent
{
    public Guid StorefrontProfileId { get; }
    public Guid TenantId { get; }

    public StorefrontProfileUpdatedEvent(Guid storefrontProfileId, Guid tenantId)
    {
        StorefrontProfileId = storefrontProfileId;
        TenantId = tenantId;
    }
}

public sealed class PublicOrderSubmittedEvent : DomainEvent
{
    public Guid StorefrontOrderId { get; }
    public IReadOnlyCollection<Guid> InvolvedTenantIds { get; }

    public PublicOrderSubmittedEvent(Guid storefrontOrderId, IReadOnlyCollection<Guid> involvedTenantIds)
    {
        StorefrontOrderId = storefrontOrderId;
        InvolvedTenantIds = involvedTenantIds;
    }
}
