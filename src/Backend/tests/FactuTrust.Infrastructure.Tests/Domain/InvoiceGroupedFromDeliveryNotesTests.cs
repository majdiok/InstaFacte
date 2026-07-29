using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

/// <summary>
/// Vague 1, lot 4 — facturation groupée périodique.
///
/// Le garde-fou anti-double-déduction (vague 0, lot B) repose sur
/// <c>Invoice.SourceDeliveryNoteId</c>, qui est SINGULIER, alors qu'une facture groupée a N
/// bons sources. <c>MarkGeneratedFromDeliveryNotes</c> y place le premier : cela suffit au
/// garde-fou, qui ne demande que « le stock est-il déjà sorti ? ». La liste complète reste
/// lisible dans l'autre sens, chaque bon portant <c>DeliveryNote.InvoiceId</c>.
///
/// Ces tests verrouillent ce point, sans quoi une facture mensuelle regroupant vingt bons
/// redéduirait vingt fois du stock déjà sorti.
/// </summary>
public sealed class InvoiceGroupedFromDeliveryNotesTests
{
    [Fact]
    public void GroupedInvoice_IsMarkedAsComingFromDeliveryNotes()
    {
        var first = Guid.NewGuid();
        var ids = new[] { first, Guid.NewGuid(), Guid.NewGuid() };
        var invoice = NewInvoice();

        var result = invoice.MarkGeneratedFromDeliveryNotes(ids);

        Assert.True(result.IsSuccess, result.Error?.Description);

        // C'est ce champ que lit DeductStockOnInvoiceValidatedHandler pour ne rien redéduire.
        Assert.Equal(first, invoice.SourceDeliveryNoteId);
    }

    [Fact]
    public void GroupedInvoice_WithASingleDeliveryNote_BehavesLikeTheDirectCase()
    {
        var only = Guid.NewGuid();
        var invoice = NewInvoice();

        Assert.True(invoice.MarkGeneratedFromDeliveryNotes(new[] { only }).IsSuccess);

        Assert.Equal(only, invoice.SourceDeliveryNoteId);
    }

    [Fact]
    public void GroupedInvoice_WithNoDeliveryNote_IsRejected()
    {
        var invoice = NewInvoice();

        Assert.True(invoice.MarkGeneratedFromDeliveryNotes(Array.Empty<Guid>()).IsFailure);
        Assert.Null(invoice.SourceDeliveryNoteId);
    }

    [Fact]
    public void GroupedInvoice_WithAnInvalidId_IsRejectedWithoutPartialEffect()
    {
        var invoice = NewInvoice();

        var result = invoice.MarkGeneratedFromDeliveryNotes(new[] { Guid.NewGuid(), Guid.Empty });

        Assert.True(result.IsFailure);
        Assert.Null(invoice.SourceDeliveryNoteId); // rien n'a été posé
    }

    [Fact]
    public void GroupedInvoice_CanAlsoCarryItsSalesOrderOrigin()
    {
        // Bons tous issus d'une même commande : la traçabilité remonte à l'engagement
        // sans toucher au garde-fou de stock.
        var deliveryNoteId = Guid.NewGuid();
        var salesOrderId = Guid.NewGuid();
        var invoice = NewInvoice();

        Assert.True(invoice.MarkGeneratedFromDeliveryNotes(new[] { deliveryNoteId }).IsSuccess);
        invoice.AttachSalesOrderOrigin(salesOrderId);

        Assert.Equal(deliveryNoteId, invoice.SourceDeliveryNoteId);
        Assert.Equal(salesOrderId, invoice.SourceSalesOrderId);
    }

    [Fact]
    public void GroupedInvoice_CarriesASingleFiscalStamp()
    {
        // Un timbre pour la facture, pas un par bon regroupé.
        var invoice = NewInvoice();
        Assert.True(invoice.MarkGeneratedFromDeliveryNotes(new[] { Guid.NewGuid(), Guid.NewGuid() }).IsSuccess);

        AddLine(invoice, qty: 3m, unitHt: 100m);
        Assert.True(invoice.SetFiscalStampAmount(Money.Create(1m)).IsSuccess);

        Assert.Equal(1m, invoice.FiscalStampAmount.Amount);
        Assert.Equal(300m + 57m + 1m, invoice.TotalAmount.Amount);
    }

    private static Invoice NewInvoice()
    {
        var address = Address.Create("1 rue de la République", "Tunis", "Tunis").Value;
        var email = Email.Create("client@example.com").Value;
        var client = Client.Create("Client test", ClientType.Individual, address, email).Value;

        return Invoice.Create(
            InvoiceNumber.Create("FAC", 2026, 60),
            client,
            new DateTime(2026, 7, 31)).Value;
    }

    private static void AddLine(Invoice invoice, decimal qty, decimal unitHt)
    {
        var add = invoice.AddCustomLine(
            "Article groupé", null, qty, "Unité", Money.Create(unitHt), VatRate.Standard);
        Assert.True(add.IsSuccess, add.Error?.Description);
    }
}
