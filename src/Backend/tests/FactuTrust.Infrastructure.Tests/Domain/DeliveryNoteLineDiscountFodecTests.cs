using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

/// <summary>
/// Vague 0 — correctif 3 : la ligne de bon de livraison porte désormais la remise et le FODEC,
/// et son moteur de calcul est aligné au millime sur <c>InvoiceLine.Calculate()</c>
/// (remise → FODEC → base TVA), de sorte que le BL et la facture qu'il engendre affichent
/// exactement les mêmes montants.
/// </summary>
public sealed class DeliveryNoteLineDiscountFodecTests
{
    [Fact]
    public void Line_AppliesDiscountBeforeFodec_AndFodecEntersVatBase()
    {
        var deliveryNote = NewDeliveryNote();
        var product = NewProduct(unitHt: 100m, fodecApplicable: true);

        Assert.True(deliveryNote.AddLine(product, orderedQuantity: 10m, discountPercent: 10m).IsSuccess);
        var line = Assert.Single(deliveryNote.Lines);

        // 10 × 100 = 1000 brut ; remise 10 % → 900 HT ; FODEC 1 % → 9 ;
        // base TVA = 909 ; TVA 19 % = 172,710 ; TTC = 1081,710.
        Assert.Equal(100m, line.DiscountAmount);
        Assert.Equal(900m, line.TotalHT);
        Assert.Equal(9m, line.FodecAmount);
        Assert.Equal(172.710m, line.TotalVAT);
        Assert.Equal(1081.710m, line.TotalTTC);
    }

    [Fact]
    public void Line_WithoutDiscountOrFodec_KeepsLegacyTotals()
    {
        // Non-régression : un BL sans remise ni FODEC (cas de tous les BL existants)
        // conserve exactement les totaux d'avant le correctif.
        var deliveryNote = NewDeliveryNote();
        var product = NewProduct(unitHt: 100m, fodecApplicable: false);

        Assert.True(deliveryNote.AddLine(product, orderedQuantity: 10m).IsSuccess);
        var line = Assert.Single(deliveryNote.Lines);

        Assert.Equal(0m, line.DiscountAmount);
        Assert.Equal(0m, line.FodecAmount);
        Assert.Equal(1000m, line.TotalHT);
        Assert.Equal(190m, line.TotalVAT);
        Assert.Equal(1190m, line.TotalTTC);
    }

    [Fact]
    public void DeliveredTotals_ApplyDiscountAndFodecOnDeliveredQuantityOnly()
    {
        var deliveryNote = NewDeliveryNote();
        var product = NewProduct(unitHt: 100m, fodecApplicable: true);

        Assert.True(deliveryNote.AddLine(product, orderedQuantity: 10m, discountPercent: 10m).IsSuccess);
        var line = Assert.Single(deliveryNote.Lines);
        Assert.True(line.RecordDelivery(deliveredQuantity: 6m).IsSuccess);

        // 6 × 100 = 600 brut ; remise 10 % → 540 ; FODEC 1 % → 5,400 ;
        // base TVA = 545,400 ; TVA 19 % = 103,626.
        Assert.Equal(540m, line.DeliveredTotalHT);
        Assert.Equal(5.400m, line.DeliveredFodecAmount);
        Assert.Equal(103.626m, line.DeliveredTotalVAT);
        Assert.Equal(649.026m, line.DeliveredTotalTTC);
    }

    [Fact]
    public void DeliveryNoteLineTotals_MatchInvoiceLineTotals_ToTheMillime()
    {
        // Cœur du correctif : le BL et la facture doivent produire les MÊMES montants.
        // Jeu de valeurs choisi pour exercer les arrondis (prix non rond, remise à décimale).
        const decimal unitHt = 33.333m;
        const decimal quantity = 7m;
        const decimal discount = 12.5m;

        var product = NewProduct(unitHt: unitHt, fodecApplicable: true);

        var deliveryNote = NewDeliveryNote();
        Assert.True(deliveryNote.AddLine(product, quantity, discountPercent: discount).IsSuccess);
        var blLine = Assert.Single(deliveryNote.Lines);

        var invoice = NewInvoice();
        Assert.True(invoice.AddLine(product, quantity, Money.Create(unitHt), discount).IsSuccess);
        var invoiceLine = Assert.Single(invoice.Lines);

        Assert.Equal(invoiceLine.DiscountAmount.Amount, blLine.DiscountAmount);
        Assert.Equal(invoiceLine.SubTotal.Amount, blLine.TotalHT);
        Assert.Equal(invoiceLine.FodecAmount.Amount, blLine.FodecAmount);
        Assert.Equal(invoiceLine.VatAmount.Amount, blLine.TotalVAT);
        Assert.Equal(invoiceLine.Total.Amount, blLine.TotalTTC);
    }

    [Fact]
    public void AggregateTotals_IncludeFodec()
    {
        var deliveryNote = NewDeliveryNote();
        var fodecProduct = NewProduct(unitHt: 100m, fodecApplicable: true);
        var plainProduct = NewProduct(unitHt: 50m, fodecApplicable: false, code: "P-PLAIN");

        Assert.True(deliveryNote.AddLine(fodecProduct, orderedQuantity: 1m).IsSuccess);
        Assert.True(deliveryNote.AddLine(plainProduct, orderedQuantity: 1m).IsSuccess);

        Assert.Equal(150m, deliveryNote.TotalHT);
        Assert.Equal(1m, deliveryNote.TotalFodec);
        // (100 + 1) × 19 % = 19,190 ; 50 × 19 % = 9,500.
        Assert.Equal(28.690m, deliveryNote.TotalVAT);
        Assert.Equal(179.690m, deliveryNote.TotalTTC);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void AddLine_WithDiscountOutOfRange_IsRejected(decimal discount)
    {
        var deliveryNote = NewDeliveryNote();
        var product = NewProduct(unitHt: 100m, fodecApplicable: false);

        var result = deliveryNote.AddLine(product, orderedQuantity: 1m, discountPercent: discount);

        Assert.True(result.IsFailure);
        Assert.Empty(deliveryNote.Lines);
    }

    [Fact]
    public void UpdateLine_ChangingQuantity_RecomputesDiscountAndFodec()
    {
        // Les montants étant dérivés, un changement de quantité ne peut pas laisser
        // de total périmé en base.
        var deliveryNote = NewDeliveryNote();
        var product = NewProduct(unitHt: 100m, fodecApplicable: true);

        Assert.True(deliveryNote.AddLine(product, orderedQuantity: 10m, discountPercent: 10m).IsSuccess);
        var line = Assert.Single(deliveryNote.Lines);
        Assert.Equal(900m, line.TotalHT);

        Assert.True(deliveryNote.UpdateLine(line.Id, orderedQuantity: 5m, discountPercent: 10m).IsSuccess);

        Assert.Equal(450m, line.TotalHT);
        Assert.Equal(4.500m, line.FodecAmount);
    }

    private static Client NewClient()
    {
        var address = Address.Create("1 rue de la République", "Tunis", "Tunis").Value;
        var email = Email.Create("client@example.com").Value;
        return Client.Create("Client test", ClientType.Individual, address, email).Value;
    }

    private static Product NewProduct(decimal unitHt, bool fodecApplicable, string code = "P-BL") =>
        Product.Create(
            code: code,
            name: "Produit BL",
            type: ProductType.Product,
            unitPrice: Money.Create(unitHt),
            vatRate: VatRate.Standard,
            categoryId: Guid.NewGuid(),
            unit: "Unité",
            isFodecApplicable: fodecApplicable).Value;

    private static DeliveryNote NewDeliveryNote() =>
        DeliveryNote.Create(
            DeliveryNoteNumber.Generate(2026, 1).Value,
            NewClient(),
            new DateTime(2026, 7, 20),
            "12 avenue Habib Bourguiba").Value;

    private static Invoice NewInvoice() =>
        Invoice.Create(
            InvoiceNumber.Create("FAC", 2026, 1),
            NewClient(),
            new DateTime(2026, 7, 20)).Value;
}
