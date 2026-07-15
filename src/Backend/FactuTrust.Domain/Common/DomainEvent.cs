using MediatR;

namespace FactuTrust.Domain.Common;

/// <summary>
/// Base class for domain events.
/// Domain events are used to communicate changes within the domain.
/// </summary>
public abstract class DomainEvent : INotification
{
    public Guid EventId { get; }
    public DateTime OccurredAt { get; }
    public string EventType => GetType().Name;

    protected DomainEvent()
    {
        EventId = Guid.NewGuid();
        OccurredAt = DateTime.UtcNow;
    }
}
