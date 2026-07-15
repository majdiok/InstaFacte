using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.Storefront;

/// <summary>
/// Deduplication marker on the Master DB side, used by the projection sync service
/// to guarantee idempotent upserts. Each (TenantId, AggregateType, AggregateId, SourceVersion)
/// is consumed at most once.
/// </summary>
public sealed class StorefrontOutboxInboxEntry : Entity
{
    public Guid TenantId { get; private set; }
    public string AggregateType { get; private set; } = null!;
    public Guid AggregateId { get; private set; }
    public int SourceVersion { get; private set; }
    public DateTime ConsumedAt { get; private set; }

    private StorefrontOutboxInboxEntry() { }

    public static StorefrontOutboxInboxEntry Mark(Guid tenantId, string aggregateType, Guid aggregateId, int sourceVersion)
    {
        return new StorefrontOutboxInboxEntry
        {
            TenantId = tenantId,
            AggregateType = aggregateType,
            AggregateId = aggregateId,
            SourceVersion = sourceVersion,
            ConsumedAt = DateTime.UtcNow
        };
    }
}
