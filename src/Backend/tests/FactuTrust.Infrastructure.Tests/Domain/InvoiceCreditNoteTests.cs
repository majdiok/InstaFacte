using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Events;
using FactuTrust.Domain.ValueObjects;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

/// <summary>
/// Validates the credit-note (facture d'avoir) domain rules introduced to fix the
/// long-standing bug where AVO documents persisted positive totals.
/// </summary>
public sealed class InvoiceCreditNoteTests
{
    [Fact]
    public void Create_DefaultType_IsStandard()
    {
        var invoice = NewInvoice(InvoiceType.Standard);

        Assert.Equal(InvoiceType.Standard, invoice.Type);
        Assert.False(invoice.IsCreditNote);
    }

    [Fact]
    public void Create_CreditNote_PersistsType()
    {
        var invoice = NewInvoice(InvoiceType.CreditNote);

        Assert.Equal(InvoiceType.CreditNote, invoice.Type);
        Assert.True(invoice.IsCreditNote);
    }

    [Fact]
    public void RecalculateTotals_StandardInvoice_HeaderIsPositive()
    {
        var invoice = NewInvoice(InvoiceType.Standard);
        AddCustomLine(invoice, qty: 5m, unitHt: 400m);
        AddCustomLine(invoice, qty: 7m, unitHt: 1.5m);

        // 5×400 + 7×1.5 = 2010.5 HT ; VAT 19% = 381.995 ; rounded TND
        Assert.Equal(2010.500m, invoice.SubTotal.Amount);
        Assert.True(invoice.TotalVat.Amount > 0);
        Assert.True(invoice.TotalAmount.Amount > 0);
    }

    [Fact]
    public void RecalculateTotals_CreditNote_HeaderIsNegative()
    {
        var invoice = NewInvoice(InvoiceType.CreditNote);
        AddCustomLine(invoice, qty: 5m, unitHt: 400m);
        AddCustomLine(invoice, qty: 7m, unitHt: 1.5m);

        // Lines remain positive; header carries the sign.
        Assert.Equal(-2010.500m, invoice.SubTotal.Amount);
        Assert.True(invoice.TotalVat.Amount < 0);
        Assert.True(invoice.TotalAmount.Amount < 0);

        // All line-level scalars stay positive (preserves CMUP, stock, reports).
        foreach (var line in invoice.Lines)
        {
            Assert.True(line.SubTotal.Amount > 0);
            Assert.True(line.VatAmount.Amount > 0);
            Assert.True(line.Total.Amount > 0);
            Assert.True(line.Quantity > 0);
        }
    }

    [Fact]
    public void RecalculateTotals_CreditNoteWithNegativeStamp_DoesNotDoubleSign()
    {
        var invoice = NewInvoice(InvoiceType.CreditNote);
        AddCustomLine(invoice, qty: 1m, unitHt: 100m); // 100 HT ; 19 VAT ; 119 lines TTC

        // Stamp is already signed by the resolver (negative on AVO).
        var signedStamp = Money.FromSignedAmount(-1m, Money.DefaultCurrency);
        invoice.SetFiscalStampAmount(signedStamp);

        // SubTotal = -100, TotalVat = -19, stamp = -1 → TotalAmount = -120
        Assert.Equal(-100.000m, invoice.SubTotal.Amount);
        Assert.Equal(-19.000m, invoice.TotalVat.Amount);
        Assert.Equal(-120.000m, invoice.TotalAmount.Amount);
    }

    [Fact]
    public void Validate_CreditNote_EmitsEventWithIsCreditNoteTrue()
    {
        var invoice = NewInvoice(InvoiceType.CreditNote);
        AddCustomLine(invoice, qty: 1m, unitHt: 50m);

        var validateResult = invoice.Validate();
        Assert.True(validateResult.IsSuccess, validateResult.Error?.Description);

        var evt = Assert.IsType<InvoiceValidatedEvent>(invoice.DomainEvents.Last());
        Assert.True(evt.IsCreditNote);
    }

    [Fact]
    public void Validate_StandardInvoice_EmitsEventWithIsCreditNoteFalse()
    {
        var invoice = NewInvoice(InvoiceType.Standard);
        AddCustomLine(invoice, qty: 1m, unitHt: 50m);

        var validateResult = invoice.Validate();
        Assert.True(validateResult.IsSuccess, validateResult.Error?.Description);

        var evt = Assert.IsType<InvoiceValidatedEvent>(invoice.DomainEvents.Last());
        Assert.False(evt.IsCreditNote);
    }

    [Fact]
    public void ReconcilePaymentStatus_CreditNote_TreatsAbsoluteMagnitudes()
    {
        var invoice = NewInvoice(InvoiceType.CreditNote);
        AddCustomLine(invoice, qty: 1m, unitHt: 100m); // -119 TTC
        invoice.Validate();

        // Refund of 119 (positive scalar) covers the magnitude → status Paid.
        invoice.ReconcilePaymentStatus(totalPaid: 119m, lastPaymentDate: DateTime.UtcNow);

        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
    }

    [Fact]
    public void ReconcilePaymentStatus_CreditNote_PartialRefundIsPartiallyPaid()
    {
        var invoice = NewInvoice(InvoiceType.CreditNote);
        AddCustomLine(invoice, qty: 1m, unitHt: 100m); // -119 TTC
        invoice.Validate();

        invoice.ReconcilePaymentStatus(totalPaid: 50m, lastPaymentDate: DateTime.UtcNow);

        Assert.Equal(InvoiceStatus.PartiallyPaid, invoice.Status);
    }

    private static Invoice NewInvoice(InvoiceType type)
    {
        var address = Address.Create("1 rue de la République", "Tunis", "Tunis").Value;
        var email = Email.Create("client@example.com").Value;
        var client = Client.Create("Client test", ClientType.Individual, address, email).Value;
        var prefix = type == InvoiceType.CreditNote ? "AVO" : "FAC";
        var number = InvoiceNumber.Create(prefix, 2026, 1);

        var result = Invoice.Create(
            number: number,
            client: client,
            issueDate: new DateTime(2026, 5, 9),
            type: type);

        Assert.True(result.IsSuccess, result.Error?.Description);
        return result.Value;
    }

    private static void AddCustomLine(Invoice invoice, decimal qty, decimal unitHt)
    {
        var price = Money.Create(unitHt, Money.DefaultCurrency);
        var add = invoice.AddCustomLine(
            designation: "Article test",
            description: null,
            quantity: qty,
            unit: "Unité",
            unitPrice: price,
            vatRate: VatRate.Standard);
        Assert.True(add.IsSuccess, add.Error?.Description);
    }
}
