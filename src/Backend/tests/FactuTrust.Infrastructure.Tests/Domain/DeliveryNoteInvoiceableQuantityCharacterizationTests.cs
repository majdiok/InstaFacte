using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

/// <summary>
/// Filet de caractérisation : sans retour confirmé, la quantité facturable d'une ligne
/// livrée est exactement <see cref="DeliveryNoteLine.DeliveredQuantity"/>. C'est ce que
/// la facturation depuis BL consommait avant le bon de retour.
/// </summary>
public sealed class DeliveryNoteInvoiceableQuantityCharacterizationTests
{
    [Fact]
    public void InvoiceableQuantity_EqualsDeliveredQuantity_WhenNothingReturned()
    {
        var deliveryNote = DeliveredNote(ordered: 10m, delivered: 10m);
        var line = Assert.Single(deliveryNote.Lines);

        Assert.Equal(0m, line.ReturnedQuantity);
        Assert.Equal(line.DeliveredQuantity, line.InvoiceableQuantity);
        Assert.True(deliveryNote.HasInvoiceableQuantity);
    }

    [Fact]
    public void InvoiceableQuantity_UsesDeliveredNotOrdered_OnPartialDelivery()
    {
        var deliveryNote = DeliveredNote(ordered: 10m, delivered: 6m);
        var line = Assert.Single(deliveryNote.Lines);

        Assert.Equal(6m, line.InvoiceableQuantity);
        Assert.Equal(6m, deliveryNote.TotalInvoiceableQuantity);
    }

    [Fact]
    public void CreditNote_RemainsADistinctInvoiceType()
    {
        var address = Address.Create("1 rue de la République", "Tunis", "Tunis").Value;
        var email = Email.Create("client@example.com").Value;
        var client = Client.Create("Client test", ClientType.Individual, address, email).Value;

        var invoice = Invoice.CreateCreditNote(
            InvoiceNumber.Create("AVO", 2026, 1),
            client,
            new DateTime(2026, 8, 16),
            Guid.NewGuid()).Value;

        Assert.True(invoice.IsCreditNote);
        Assert.Equal(InvoiceType.CreditNote, invoice.Type);
        Assert.Null(invoice.SourceDeliveryNoteId);
    }

    private static DeliveryNote DeliveredNote(decimal ordered, decimal delivered)
    {
        var address = Address.Create("1 rue de la République", "Tunis", "Tunis").Value;
        var email = Email.Create("client@example.com").Value;
        var client = Client.Create("Client test", ClientType.Individual, address, email).Value;
        var product = Product.Create(
            "P-BL", "Produit BL", ProductType.Product, Money.Create(10m), VatRate.Standard,
            Guid.NewGuid(), "Unité").Value;

        var note = DeliveryNote.Create(
            DeliveryNoteNumber.Generate(2026, 1).Value,
            client,
            new DateTime(2026, 8, 1),
            "12 avenue Habib Bourguiba").Value;

        Assert.True(note.AddLine(product, ordered).IsSuccess);
        Assert.True(note.Confirm().IsSuccess);
        var line = Assert.Single(note.Lines);
        Assert.True(line.RecordDelivery(delivered).IsSuccess);
        Assert.True(note.RecordDelivery(new DateTime(2026, 8, 2), "Réceptionnaire").IsSuccess);
        return note;
    }
}
