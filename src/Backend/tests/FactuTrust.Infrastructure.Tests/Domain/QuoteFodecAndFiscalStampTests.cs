using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

/// <summary>
/// Vague 0 — correctif 2 : le devis annonce désormais le FODEC et le timbre fiscal que la
/// facture appliquera. Le test déterminant est <see cref="QuoteTotals_EqualInvoiceTotals_Exactly"/> :
/// un devis accepté et la facture qui en découle doivent porter le même total au millime.
/// </summary>
public sealed class QuoteFodecAndFiscalStampTests
{
    private const decimal StampAmount = 1.000m;

    [Fact]
    public void QuoteLine_AppliesDiscountBeforeFodec_AndFodecEntersVatBase()
    {
        var quote = NewQuote();
        var product = NewProduct(unitHt: 100m, fodecApplicable: true);

        Assert.True(quote.AddLine(product, quantity: 10m, customUnitPrice: null, discountPercent: 10m).IsSuccess);
        var line = Assert.Single(quote.Lines);

        Assert.Equal(100m, line.DiscountAmount.Amount);
        Assert.Equal(900m, line.SubTotal.Amount);
        Assert.Equal(9m, line.FodecAmount.Amount);
        Assert.Equal(172.710m, line.VatAmount.Amount);
        Assert.Equal(1081.710m, line.Total.Amount);
    }

    [Fact]
    public void QuoteHeader_TotalIsSubTotalPlusFodecPlusVatPlusStamp()
    {
        var quote = NewQuote();
        var product = NewProduct(unitHt: 1000m, fodecApplicable: true);

        Assert.True(quote.AddLine(product, quantity: 1m).IsSuccess);
        Assert.True(quote.SetFiscalStampAmount(Money.Create(StampAmount)).IsSuccess);

        Assert.Equal(1000m, quote.SubTotal.Amount);
        Assert.Equal(10m, quote.FodecAmount.Amount);
        Assert.Equal(191.900m, quote.TotalVat.Amount);
        Assert.Equal(StampAmount, quote.FiscalStampAmount.Amount);
        Assert.Equal(1202.900m, quote.TotalAmount.Amount);
    }

    [Fact]
    public void QuoteTotals_EqualInvoiceTotals_Exactly()
    {
        // Cœur du correctif. Avant, le devis ignorait FODEC et timbre : sur ce jeu de données
        // le client acceptait 1190,000 TND puis recevait une facture de 1202,900 TND.
        var product = NewProduct(unitHt: 1000m, fodecApplicable: true);

        var quote = NewQuote();
        Assert.True(quote.AddLine(product, quantity: 1m).IsSuccess);
        Assert.True(quote.SetFiscalStampAmount(Money.Create(StampAmount)).IsSuccess);

        // Reproduit exactement ce que fait QuoteToInvoiceConversionService.
        var invoice = NewInvoice();
        var quoteLine = quote.Lines.Single();
        Assert.True(invoice.AddLine(
            product,
            quoteLine.Quantity,
            quoteLine.UnitPrice,
            quoteLine.DiscountPercent,
            quoteLine.FodecRatePercent).IsSuccess);
        Assert.True(invoice.SetFiscalStampAmount(Money.Create(StampAmount)).IsSuccess);

        Assert.Equal(invoice.SubTotal.Amount, quote.SubTotal.Amount);
        Assert.Equal(invoice.FodecAmount.Amount, quote.FodecAmount.Amount);
        Assert.Equal(invoice.TotalVat.Amount, quote.TotalVat.Amount);
        Assert.Equal(invoice.FiscalStampAmount.Amount, quote.FiscalStampAmount.Amount);
        Assert.Equal(invoice.TotalAmount.Amount, quote.TotalAmount.Amount);
    }

    [Fact]
    public void QuoteTotals_EqualInvoiceTotals_WithDiscountAndAwkwardPrice()
    {
        const decimal unitHt = 33.333m;
        const decimal quantity = 7m;
        const decimal discount = 12.5m;
        var product = NewProduct(unitHt: unitHt, fodecApplicable: true);

        var quote = NewQuote();
        Assert.True(quote.AddLine(product, quantity, Money.Create(unitHt), discount).IsSuccess);
        Assert.True(quote.SetFiscalStampAmount(Money.Create(StampAmount)).IsSuccess);

        var invoice = NewInvoice();
        Assert.True(invoice.AddLine(product, quantity, Money.Create(unitHt), discount).IsSuccess);
        Assert.True(invoice.SetFiscalStampAmount(Money.Create(StampAmount)).IsSuccess);

        Assert.Equal(invoice.TotalAmount.Amount, quote.TotalAmount.Amount);
    }

    [Fact]
    public void QuoteWithoutFodecProduct_KeepsLegacyTotals()
    {
        // Non-régression : sans produit assujetti ni timbre, les totaux sont ceux d'avant.
        var quote = NewQuote();
        var product = NewProduct(unitHt: 500m, fodecApplicable: false);

        Assert.True(quote.AddLine(product, quantity: 2m).IsSuccess);

        Assert.Equal(1000m, quote.SubTotal.Amount);
        Assert.Equal(0m, quote.FodecAmount.Amount);
        Assert.Equal(190m, quote.TotalVat.Amount);
        Assert.Equal(1190m, quote.TotalAmount.Amount);
    }

    [Fact]
    public void SetFiscalStamp_OnNonEditableQuote_IsRejected()
    {
        var quote = NewQuote();
        var product = NewProduct(unitHt: 100m, fodecApplicable: false);
        Assert.True(quote.AddLine(product, quantity: 1m).IsSuccess);
        Assert.True(quote.Send().IsSuccess);
        Assert.True(quote.Accept().IsSuccess);

        var result = quote.SetFiscalStampAmount(Money.Create(StampAmount));

        Assert.True(result.IsFailure);
        Assert.Equal(0m, quote.FiscalStampAmount.Amount);
    }

    [Fact]
    public void RemovingLine_RecomputesFodecAndKeepsStamp()
    {
        var quote = NewQuote();
        var fodecProduct = NewProduct(unitHt: 100m, fodecApplicable: true);
        var plainProduct = NewProduct(unitHt: 100m, fodecApplicable: false, code: "P-PLAIN");

        Assert.True(quote.AddLine(fodecProduct, quantity: 1m).IsSuccess);
        Assert.True(quote.AddLine(plainProduct, quantity: 1m).IsSuccess);
        Assert.True(quote.SetFiscalStampAmount(Money.Create(StampAmount)).IsSuccess);
        Assert.Equal(1m, quote.FodecAmount.Amount);

        var fodecLine = quote.Lines.Single(l => l.ProductCode == "P-FODEC");
        Assert.True(quote.RemoveLine(fodecLine.Id).IsSuccess);

        Assert.Equal(0m, quote.FodecAmount.Amount);
        Assert.Equal(StampAmount, quote.FiscalStampAmount.Amount);
        Assert.Equal(120.000m, quote.TotalAmount.Amount); // 100 + 19 TVA + 1 timbre
    }

    private static Client NewClient()
    {
        var address = Address.Create("1 rue de la République", "Tunis", "Tunis").Value;
        var email = Email.Create("client@example.com").Value;
        return Client.Create("Client test", ClientType.Individual, address, email).Value;
    }

    private static Product NewProduct(decimal unitHt, bool fodecApplicable, string code = "P-FODEC") =>
        Product.Create(
            code: code,
            name: "Produit devis",
            type: ProductType.Product,
            unitPrice: Money.Create(unitHt),
            vatRate: VatRate.Standard,
            categoryId: Guid.NewGuid(),
            unit: "Unité",
            isFodecApplicable: fodecApplicable).Value;

    private static Quote NewQuote() =>
        Quote.Create(
            QuoteNumber.Create("DEV", 2026, 1),
            NewClient(),
            new DateTime(2026, 7, 20),
            new DateTime(2026, 8, 20)).Value;

    private static Invoice NewInvoice() =>
        Invoice.Create(
            InvoiceNumber.Create("FAC", 2026, 1),
            NewClient(),
            new DateTime(2026, 7, 20)).Value;
}
