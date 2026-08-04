using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Events;

/// <summary>
/// Event raised when a quote is created.
/// </summary>
public sealed class QuoteCreatedEvent : DomainEvent
{
    public Guid QuoteId { get; }
    public string QuoteNumber { get; }
    public Guid ClientId { get; }

    public QuoteCreatedEvent(Guid quoteId, string quoteNumber, Guid clientId)
    {
        QuoteId = quoteId;
        QuoteNumber = quoteNumber;
        ClientId = clientId;
    }
}

/// <summary>
/// Event raised when a quote is sent to the client.
/// </summary>
public sealed class QuoteSentEvent : DomainEvent
{
    public Guid QuoteId { get; }
    public string QuoteNumber { get; }
    public string ClientEmail { get; }

    public QuoteSentEvent(Guid quoteId, string quoteNumber, string clientEmail)
    {
        QuoteId = quoteId;
        QuoteNumber = quoteNumber;
        ClientEmail = clientEmail;
    }
}

/// <summary>
/// Event raised when a quote is accepted by the client.
/// </summary>
public sealed class QuoteAcceptedEvent : DomainEvent
{
    public Guid QuoteId { get; }
    public string QuoteNumber { get; }

    public QuoteAcceptedEvent(Guid quoteId, string quoteNumber)
    {
        QuoteId = quoteId;
        QuoteNumber = quoteNumber;
    }
}

/// <summary>
/// Event raised when a quote is rejected by the client.
/// </summary>
public sealed class QuoteRejectedEvent : DomainEvent
{
    public Guid QuoteId { get; }
    public string QuoteNumber { get; }
    public string? Reason { get; }

    public QuoteRejectedEvent(Guid quoteId, string quoteNumber, string? reason)
    {
        QuoteId = quoteId;
        QuoteNumber = quoteNumber;
        Reason = reason;
    }
}

/// <summary>
/// Event raised when a quote is cancelled.
/// </summary>
public sealed class QuoteCancelledEvent : DomainEvent
{
    public Guid QuoteId { get; }
    public string QuoteNumber { get; }
    public string Reason { get; }

    public QuoteCancelledEvent(Guid quoteId, string quoteNumber, string reason)
    {
        QuoteId = quoteId;
        QuoteNumber = quoteNumber;
        Reason = reason;
    }
}

/// <summary>
/// Event raised when a quote expires.
/// </summary>
public sealed class QuoteExpiredEvent : DomainEvent
{
    public Guid QuoteId { get; }
    public string QuoteNumber { get; }
    public DateTime ExpiryDate { get; }

    public QuoteExpiredEvent(Guid quoteId, string quoteNumber, DateTime expiryDate)
    {
        QuoteId = quoteId;
        QuoteNumber = quoteNumber;
        ExpiryDate = expiryDate;
    }
}

/// <summary>
/// Event raised when a quote is converted to an invoice.
/// </summary>
public sealed class QuoteConvertedToInvoiceEvent : DomainEvent
{
    public Guid QuoteId { get; }
    public string QuoteNumber { get; }
    public Guid InvoiceId { get; }

    public QuoteConvertedToInvoiceEvent(Guid quoteId, string quoteNumber, Guid invoiceId)
    {
        QuoteId = quoteId;
        QuoteNumber = quoteNumber;
        InvoiceId = invoiceId;
    }
}

/// <summary>
/// Event raised when a quote is converted to a sales order (commande client).
/// </summary>
public sealed class QuoteConvertedToSalesOrderEvent : DomainEvent
{
    public Guid QuoteId { get; }
    public string QuoteNumber { get; }
    public Guid SalesOrderId { get; }

    public QuoteConvertedToSalesOrderEvent(Guid quoteId, string quoteNumber, Guid salesOrderId)
    {
        QuoteId = quoteId;
        QuoteNumber = quoteNumber;
        SalesOrderId = salesOrderId;
    }
}
