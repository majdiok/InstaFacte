using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

/// <summary>
/// Vague 1, lot 3 — liens de traçabilité vers la commande client.
///
/// Le test déterminant est <see cref="InvoiceFromSalesOrder_MustStillDeductStock"/> : une
/// facture émise DIRECTEMENT depuis une commande n'a donné lieu à aucune sortie et doit donc
/// déduire le stock, alors qu'une facture issue d'un bon de livraison ne le doit pas. Confondre
/// les deux réintroduirait exactement le défaut corrigé au lot B de la vague 0.
/// </summary>
public sealed class SalesOrderDocumentLinksTests
{
    [Fact]
    public void InvoiceFromSalesOrder_MustStillDeductStock()
    {
        var orderId = Guid.NewGuid();

        var invoice = Invoice.CreateFromSalesOrder(
            InvoiceNumber.Create("FAC", 2026, 1),
            NewClient(),
            new DateTime(2026, 7, 28),
            orderId).Value;

        Assert.Equal(orderId, invoice.SourceSalesOrderId);

        // Le garde-fou anti-double-déduction porte UNIQUEMENT sur SourceDeliveryNoteId.
        // Nul ici : aucune sortie n'a eu lieu, la validation devra bien déduire.
        Assert.Null(invoice.SourceDeliveryNoteId);
    }

    [Fact]
    public void InvoiceFromDeliveryNote_MustNotDeductStock()
    {
        var invoice = Invoice.CreateFromDeliveryNote(
            InvoiceNumber.Create("FAC", 2026, 2),
            NewClient(),
            new DateTime(2026, 7, 28),
            Guid.NewGuid()).Value;

        Assert.NotNull(invoice.SourceDeliveryNoteId);
        Assert.Null(invoice.SourceSalesOrderId);
    }

    [Fact]
    public void AttachSalesOrderOrigin_TracesUpstreamWithoutTouchingTheStockGuard()
    {
        // Facture issue d'un BL lui-même issu d'une commande : la traçabilité remonte
        // jusqu'à l'engagement, mais le garde-fou de stock reste porté par le BL.
        var deliveryNoteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var invoice = Invoice.CreateFromDeliveryNote(
            InvoiceNumber.Create("FAC", 2026, 3),
            NewClient(),
            new DateTime(2026, 7, 28),
            deliveryNoteId).Value;

        invoice.AttachSalesOrderOrigin(orderId);

        Assert.Equal(orderId, invoice.SourceSalesOrderId);
        Assert.Equal(deliveryNoteId, invoice.SourceDeliveryNoteId); // garde-fou intact
    }

    [Fact]
    public void AttachSalesOrderOrigin_IsIdempotent()
    {
        var first = Guid.NewGuid();
        var invoice = Invoice.CreateFromSalesOrder(
            InvoiceNumber.Create("FAC", 2026, 4),
            NewClient(),
            new DateTime(2026, 7, 28),
            first).Value;

        invoice.AttachSalesOrderOrigin(Guid.NewGuid());

        Assert.Equal(first, invoice.SourceSalesOrderId);
    }

    [Fact]
    public void CreateFromSalesOrder_WithEmptyId_IsRejected()
    {
        var result = Invoice.CreateFromSalesOrder(
            InvoiceNumber.Create("FAC", 2026, 5),
            NewClient(),
            new DateTime(2026, 7, 28),
            Guid.Empty);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void DeliveryNote_CarriesItsSalesOrderOrigin()
    {
        var orderId = Guid.NewGuid();
        var note = NewDeliveryNote();

        Assert.Null(note.SourceSalesOrderId);

        note.AttachSalesOrderOrigin(orderId);

        Assert.Equal(orderId, note.SourceSalesOrderId);
    }

    [Fact]
    public void DeliveryNote_SalesOrderOriginIsIdempotent()
    {
        var first = Guid.NewGuid();
        var note = NewDeliveryNote();

        note.AttachSalesOrderOrigin(first);
        note.AttachSalesOrderOrigin(Guid.NewGuid());

        Assert.Equal(first, note.SourceSalesOrderId);
    }

    private static Client NewClient()
    {
        var address = Address.Create("1 rue de la République", "Tunis", "Tunis").Value;
        var email = Email.Create("client@example.com").Value;
        return Client.Create("Client test", ClientType.Individual, address, email).Value;
    }

    private static DeliveryNote NewDeliveryNote() =>
        DeliveryNote.Create(
            DeliveryNoteNumber.Generate(2026, 1).Value,
            NewClient(),
            new DateTime(2026, 7, 28),
            "12 avenue Habib Bourguiba").Value;
}
