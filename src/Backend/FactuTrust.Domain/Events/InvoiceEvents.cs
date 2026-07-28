using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Events;

public sealed class InvoiceCreatedEvent : DomainEvent
{
    public Guid InvoiceId { get; }
    public string InvoiceNumber { get; }
    public Guid ClientId { get; }

    public InvoiceCreatedEvent(Guid invoiceId, string invoiceNumber, Guid clientId)
    {
        InvoiceId = invoiceId;
        InvoiceNumber = invoiceNumber;
        ClientId = clientId;
    }
}

public sealed class InvoiceValidatedEvent : DomainEvent
{
    public Guid InvoiceId { get; }
    public string InvoiceNumber { get; }
    /// <summary>True when the validated document is a credit note (AVO).</summary>
    public bool IsCreditNote { get; }

    public InvoiceValidatedEvent(Guid invoiceId, string invoiceNumber, bool isCreditNote = false)
    {
        InvoiceId = invoiceId;
        InvoiceNumber = invoiceNumber;
        IsCreditNote = isCreditNote;
    }
}

public sealed class InvoiceSignedEvent : DomainEvent
{
    public Guid InvoiceId { get; }
    public string InvoiceNumber { get; }
    public string SignatureHash { get; }

    public InvoiceSignedEvent(Guid invoiceId, string invoiceNumber, string signatureHash)
    {
        InvoiceId = invoiceId;
        InvoiceNumber = invoiceNumber;
        SignatureHash = signatureHash;
    }
}

public sealed class InvoicePaidEvent : DomainEvent
{
    public Guid InvoiceId { get; }
    public string InvoiceNumber { get; }
    public decimal Amount { get; }

    public InvoicePaidEvent(Guid invoiceId, string invoiceNumber, decimal amount)
    {
        InvoiceId = invoiceId;
        InvoiceNumber = invoiceNumber;
        Amount = amount;
    }
}

public sealed class InvoiceCancelledEvent : DomainEvent
{
    public Guid InvoiceId { get; }
    public string InvoiceNumber { get; }
    public string Reason { get; }

    public InvoiceCancelledEvent(Guid invoiceId, string invoiceNumber, string reason)
    {
        InvoiceId = invoiceId;
        InvoiceNumber = invoiceNumber;
        Reason = reason;
    }
}

public sealed class InvoiceArchivedEvent : DomainEvent
{
    public Guid InvoiceId { get; }
    public string InvoiceNumber { get; }

    public InvoiceArchivedEvent(Guid invoiceId, string invoiceNumber)
    {
        InvoiceId = invoiceId;
        InvoiceNumber = invoiceNumber;
    }
}

// InvoiceOverdueEvent retiré (vague 0, correctif 8) : jamais produit, et explicitement ignoré
// par le DbContext. Le dépassement d'échéance est calculé à la volée en requête.
