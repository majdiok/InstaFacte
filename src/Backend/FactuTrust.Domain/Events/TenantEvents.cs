using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Events;

public sealed class TenantCreatedEvent : DomainEvent
{
    public Guid TenantId { get; }
    public string CompanyName { get; }
    public string DatabaseName { get; }

    public TenantCreatedEvent(Guid tenantId, string companyName, string databaseName)
    {
        TenantId = tenantId;
        CompanyName = companyName;
        DatabaseName = databaseName;
    }
}

public sealed class TenantUpdatedEvent : DomainEvent
{
    public Guid TenantId { get; }
    public string CompanyName { get; }

    public TenantUpdatedEvent(Guid tenantId, string companyName)
    {
        TenantId = tenantId;
        CompanyName = companyName;
    }
}

public sealed class TenantDeactivatedEvent : DomainEvent
{
    public Guid TenantId { get; }
    public string CompanyName { get; }

    public TenantDeactivatedEvent(Guid tenantId, string companyName)
    {
        TenantId = tenantId;
        CompanyName = companyName;
    }
}

public sealed class TenantReactivatedEvent : DomainEvent
{
    public Guid TenantId { get; }
    public string CompanyName { get; }

    public TenantReactivatedEvent(Guid tenantId, string companyName)
    {
        TenantId = tenantId;
        CompanyName = companyName;
    }
}
