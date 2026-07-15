using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.Storefront;

/// <summary>
/// Outbox message persisted in the tenant database in the same transaction as
/// the business change. Consumed asynchronously by the projection sync service.
/// </summary>
public sealed class StorefrontOutboxMessage : Entity
{
    public string AggregateType { get; private set; } = null!;
    public Guid AggregateId { get; private set; }
    public int SourceVersion { get; private set; }
    public string EventType { get; private set; } = null!;
    public string PayloadJson { get; private set; } = "{}";
    public DateTime OccurredAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public int AttemptCount { get; private set; }
    public string? LastError { get; private set; }

    private StorefrontOutboxMessage() { }

    public static StorefrontOutboxMessage Create(
        string aggregateType,
        Guid aggregateId,
        int sourceVersion,
        string eventType,
        string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(aggregateType))
            throw new ArgumentException("AggregateType obligatoire", nameof(aggregateType));
        if (aggregateId == Guid.Empty)
            throw new ArgumentException("AggregateId obligatoire", nameof(aggregateId));
        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException("EventType obligatoire", nameof(eventType));

        return new StorefrontOutboxMessage
        {
            AggregateType = aggregateType.Trim(),
            AggregateId = aggregateId,
            SourceVersion = sourceVersion,
            EventType = eventType.Trim(),
            PayloadJson = payloadJson ?? "{}",
            OccurredAt = DateTime.UtcNow,
            AttemptCount = 0
        };
    }

    public void MarkProcessed()
    {
        ProcessedAt = DateTime.UtcNow;
        LastError = null;
    }

    public void RecordAttemptFailure(string error)
    {
        AttemptCount++;
        LastError = error?[..Math.Min(error?.Length ?? 0, 500)];
    }
}
