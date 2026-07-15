using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

public sealed class InvoiceFodecTests
{
    [Fact]
    public void RecalculateTotals_WithFodecLine_IncludesFodecInHeaderAndVatBase()
    {
        var invoice = NewInvoice(InvoiceType.Standard);
        AddCustomLine(invoice, qty: 10m, unitHt: 100m, fodecApplicable: true);

        Assert.Equal(1000m, invoice.SubTotal.Amount);
        Assert.Equal(10m, invoice.FodecAmount.Amount);
        Assert.Equal(191.900m, invoice.TotalVat.Amount);
        Assert.Equal(1201.900m, invoice.TotalAmount.Amount);
    }

    [Fact]
    public void RecalculateTotals_CreditNoteWithFodec_HeaderIsNegative()
    {
        var invoice = NewInvoice(InvoiceType.CreditNote);
        AddCustomLine(invoice, qty: 10m, unitHt: 100m, fodecApplicable: true);

        Assert.Equal(-1000m, invoice.SubTotal.Amount);
        Assert.Equal(-10m, invoice.FodecAmount.Amount);
        Assert.True(invoice.TotalVat.Amount < 0);
        Assert.True(invoice.TotalAmount.Amount < 0);
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

    private static void AddCustomLine(Invoice invoice, decimal qty, decimal unitHt, bool fodecApplicable)
    {
        var price = Money.Create(unitHt, Money.DefaultCurrency);
        var add = invoice.AddCustomLine(
            designation: "Article FODEC",
            description: null,
            quantity: qty,
            unit: "Unité",
            unitPrice: price,
            vatRate: VatRate.Standard,
            isFodecApplicable: fodecApplicable,
            fodecRatePercent: 1m);
        Assert.True(add.IsSuccess, add.Error?.Description);
    }
}
