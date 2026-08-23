using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

public sealed class SalesReturnNoteTests
{
    [Fact]
    public void Number_Generate_UsesBrtPrefix()
    {
        var number = SalesReturnNoteNumber.Generate(2026, 1).Value;
        Assert.Equal("BRT-2026-000001", number.Value);
    }

    [Fact]
    public void Number_Create_RejectsBrPrefix()
    {
        var result = SalesReturnNoteNumber.Create("BR-2026-000001");
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void InvoiceableQuantity_DecreasesAfterPartialReturn()
    {
        var (note, line) = DeliveredNote(10m);
        Assert.True(note.RecordReturn(line.Id, 3m).IsSuccess);

        Assert.Equal(3m, line.ReturnedQuantity);
        Assert.Equal(7m, line.InvoiceableQuantity);
        Assert.Equal(10m, line.DeliveredQuantity);
        Assert.True(note.HasInvoiceableQuantity);
    }

    [Fact]
    public void SecondReturn_IsCappedByRemainingInvoiceable()
    {
        var (note, line) = DeliveredNote(10m);
        Assert.True(note.RecordReturn(line.Id, 6m).IsSuccess);
        Assert.True(note.RecordReturn(line.Id, 4m).IsSuccess);

        var overflow = note.RecordReturn(line.Id, 1m);
        Assert.True(overflow.IsFailure);
        Assert.Equal(10m, line.ReturnedQuantity);
        Assert.Equal(0m, line.InvoiceableQuantity);
        Assert.False(note.HasInvoiceableQuantity);
        Assert.True(line.IsFullyReturned);
    }

    [Fact]
    public void RecordReturn_RejectedWhenQuantityExceedsRemaining()
    {
        var (note, line) = DeliveredNote(5m);
        var result = note.RecordReturn(line.Id, 6m);
        Assert.True(result.IsFailure);
        Assert.Equal(0m, line.ReturnedQuantity);
    }

    [Fact]
    public void RecordReturn_RejectedWhenQuantityIsZeroOrNegative()
    {
        var (note, line) = DeliveredNote(5m);
        Assert.True(note.RecordReturn(line.Id, 0m).IsFailure);
        Assert.True(note.RecordReturn(line.Id, -1m).IsFailure);
    }

    [Fact]
    public void RecordReturn_RejectedOnDraftOrConfirmedBl()
    {
        var draft = NewDraft(5m);
        var line = Assert.Single(draft.Lines);
        Assert.True(draft.RecordReturn(line.Id, 1m).IsFailure);

        Assert.True(draft.Confirm().IsSuccess);
        Assert.True(draft.RecordReturn(line.Id, 1m).IsFailure);
    }

    [Fact]
    public void RecordReturn_RejectedWhenBlAlreadyInvoiced()
    {
        var (note, line) = DeliveredNote(5m);
        var invoice = Invoice.CreateFromDeliveryNote(
            InvoiceNumber.Create("FAC", 2026, 1),
            note.Client,
            new DateTime(2026, 8, 16),
            note.Id).Value;
        Assert.True(note.MarkAsInvoiced(invoice).IsSuccess);

        Assert.True(note.RecordReturn(line.Id, 1m).IsFailure);
        Assert.Equal(0m, line.ReturnedQuantity);
    }

    [Fact]
    public void CreateReturnNote_FromDeliveredBl_Succeeds()
    {
        var (bl, line) = DeliveredNote(8m);
        var number = SalesReturnNoteNumber.Generate(2026, 1).Value;

        var result = SalesReturnNote.Create(number, bl, new DateTime(2026, 8, 16), "Marchandise endommagée");
        Assert.True(result.IsSuccess, result.Error?.Description);

        var ret = result.Value;
        Assert.True(ret.AddLine(line, 3m).IsSuccess);
        Assert.Equal(3m, ret.TotalReturnedQuantity);
        Assert.Equal(SalesReturnNoteStatus.Draft, ret.Status);
    }

    [Fact]
    public void CreateReturnNote_RejectedWhenBlInvoiced()
    {
        var (bl, _) = DeliveredNote(5m);
        var invoice = Invoice.CreateFromDeliveryNote(
            InvoiceNumber.Create("FAC", 2026, 2),
            bl.Client,
            new DateTime(2026, 8, 16),
            bl.Id).Value;
        Assert.True(bl.MarkAsInvoiced(invoice).IsSuccess);

        var result = SalesReturnNote.Create(
            SalesReturnNoteNumber.Generate(2026, 2).Value,
            bl,
            new DateTime(2026, 8, 16),
            "Trop livré");
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void AddLine_RejectedWhenQuantityExceedsInvoiceable()
    {
        var (bl, line) = DeliveredNote(4m);
        var ret = SalesReturnNote.Create(
            SalesReturnNoteNumber.Generate(2026, 3).Value,
            bl,
            new DateTime(2026, 8, 16),
            "Erreur de commande").Value;

        Assert.True(ret.AddLine(line, 5m).IsFailure);
    }

    [Fact]
    public void Confirm_LocksDocument()
    {
        var (bl, line) = DeliveredNote(4m);
        var ret = SalesReturnNote.Create(
            SalesReturnNoteNumber.Generate(2026, 4).Value,
            bl,
            new DateTime(2026, 8, 16),
            "Client a changé d'avis").Value;
        Assert.True(ret.AddLine(line, 2m).IsSuccess);
        Assert.True(ret.Confirm().IsSuccess);

        Assert.Equal(SalesReturnNoteStatus.Confirmed, ret.Status);
        Assert.NotNull(ret.ConfirmedAt);
        Assert.True(ret.AddLine(line, 1m).IsFailure);
        Assert.True(ret.Confirm().IsFailure);
    }

    [Fact]
    public void Confirm_RejectedWithoutLines()
    {
        var (bl, _) = DeliveredNote(4m);
        var ret = SalesReturnNote.Create(
            SalesReturnNoteNumber.Generate(2026, 5).Value,
            bl,
            new DateTime(2026, 8, 16),
            "Motif valide").Value;

        Assert.True(ret.Confirm().IsFailure);
    }

    [Fact]
    public void Create_RejectedWhenReasonTooShort()
    {
        var (bl, _) = DeliveredNote(4m);
        var result = SalesReturnNote.Create(
            SalesReturnNoteNumber.Generate(2026, 6).Value,
            bl,
            new DateTime(2026, 8, 16),
            "ab");
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void SalesOrder_RecordReturns_DoesNotReopenPendingDelivery()
    {
        var order = NewConfirmedOrder(10m);
        var line = Assert.Single(order.Lines);
        Assert.True(order.RecordDeliveries(new[] { (line.Id, 10m) }).IsSuccess);

        Assert.Equal(0m, line.PendingDeliveryQuantity);
        Assert.Equal(10m, line.DeliveredNotInvoicedQuantity);

        Assert.True(order.RecordReturns(new[] { (line.Id, 3m) }).IsSuccess);

        Assert.Equal(0m, line.PendingDeliveryQuantity);
        Assert.Equal(7m, line.DeliveredNotInvoicedQuantity);
        Assert.Equal(7m, line.PendingInvoiceQuantity);
        Assert.Equal(10m, line.DeliveredQuantity);
        Assert.Equal(3m, line.ReturnedQuantity);
    }

    private static (DeliveryNote Note, DeliveryNoteLine Line) DeliveredNote(decimal quantity)
    {
        var note = NewDraft(quantity);
        Assert.True(note.Confirm().IsSuccess);
        var line = Assert.Single(note.Lines);
        Assert.True(line.RecordDelivery(quantity).IsSuccess);
        Assert.True(note.RecordDelivery(new DateTime(2026, 8, 2), "Réceptionnaire").IsSuccess);
        return (note, line);
    }

    private static DeliveryNote NewDraft(decimal quantity)
    {
        var client = NewClient();
        var product = NewProduct();
        var note = DeliveryNote.Create(
            DeliveryNoteNumber.Generate(2026, 40).Value,
            client,
            new DateTime(2026, 8, 1),
            "12 avenue Habib Bourguiba").Value;
        Assert.True(note.AddLine(product, quantity).IsSuccess);
        return note;
    }

    private static Client NewClient()
    {
        var address = Address.Create("1 rue de la République", "Tunis", "Tunis").Value;
        var email = Email.Create("client@example.com").Value;
        return Client.Create("Client test", ClientType.Individual, address, email).Value;
    }

    private static Product NewProduct() =>
        Product.Create(
            "P-RET", "Produit retour", ProductType.Product, Money.Create(20m), VatRate.Standard,
            Guid.NewGuid(), "Unité").Value;

    private static SalesOrder NewConfirmedOrder(decimal quantity)
    {
        var client = NewClient();
        var product = NewProduct();
        var order = SalesOrder.Create(
            SalesOrderNumber.Create("CDE", 2026, 1),
            client,
            new DateTime(2026, 8, 1)).Value;
        Assert.True(order.AddLine(product, quantity, Money.Create(20m)).IsSuccess);
        Assert.True(order.Confirm().IsSuccess);
        return order;
    }
}
