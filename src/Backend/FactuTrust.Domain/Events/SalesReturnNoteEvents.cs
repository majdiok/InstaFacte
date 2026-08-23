using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Events;

public sealed class SalesReturnNoteCreatedEvent : DomainEvent
{
    public Guid SalesReturnNoteId { get; }
    public string Number { get; }
    public Guid DeliveryNoteId { get; }

    public SalesReturnNoteCreatedEvent(Guid salesReturnNoteId, string number, Guid deliveryNoteId)
    {
        SalesReturnNoteId = salesReturnNoteId;
        Number = number;
        DeliveryNoteId = deliveryNoteId;
    }
}

public sealed class SalesReturnNoteConfirmedEvent : DomainEvent
{
    public Guid SalesReturnNoteId { get; }
    public string Number { get; }
    public Guid DeliveryNoteId { get; }

    public SalesReturnNoteConfirmedEvent(Guid salesReturnNoteId, string number, Guid deliveryNoteId)
    {
        SalesReturnNoteId = salesReturnNoteId;
        Number = number;
        DeliveryNoteId = deliveryNoteId;
    }
}
