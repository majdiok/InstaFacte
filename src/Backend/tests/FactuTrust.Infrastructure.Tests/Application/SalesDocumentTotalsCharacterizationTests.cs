using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

/// <summary>
/// Tests de caractérisation (vague 0, lot A) — filet de sécurité posé AVANT les correctifs.
///
/// Ils figent le moteur de calcul de la famille documentaire de vente (facture, avoir, devis,
/// bon de livraison) afin que toute dérive involontaire introduite par les lots B à I échoue
/// en CI. Ils sont volontairement écrits pour être <b>additifs</b> : les lots C et D ajoutent
/// le FODEC au devis et au BL sans invalider une seule assertion de ce fichier, car aucune
/// assertion ne porte sur l'absence de FODEC — elles portent sur la remise, la TVA et les
/// quantités, qui ne doivent pas bouger.
///
/// Règle : ne jamais « réparer » un test de ce fichier pour faire passer un correctif. Toute
/// modification doit être accompagnée d'un commentaire nommant le lot qui la justifie.
/// </summary>
public sealed class SalesDocumentTotalsCharacterizationTests
{
    // ─────────────────────────── Facture : moteur de calcul ───────────────────────────

    [Fact]
    public void InvoiceLine_AppliesDiscountBeforeFodec_AndFodecEntersVatBase()
    {
        var invoice = NewInvoice(InvoiceType.Standard);

        // 10 × 100 = 1000 HT brut ; remise 10 % → 900 HT ; FODEC 1 % → 9 ;
        // base TVA = 900 + 9 = 909 ; TVA 19 % = 172,710.
        AddCustomLine(invoice, qty: 10m, unitHt: 100m, fodecApplicable: true, discountPercent: 10m);

        var line = Assert.Single(invoice.Lines);
        Assert.Equal(100m, line.DiscountAmount.Amount);
        Assert.Equal(900m, line.SubTotal.Amount);
        Assert.Equal(9m, line.FodecAmount.Amount);
        Assert.Equal(172.710m, line.VatAmount.Amount);
        Assert.Equal(1081.710m, line.Total.Amount);
    }

    [Fact]
    public void Invoice_HeaderTotal_IsSubTotalPlusFodecPlusVatPlusStamp()
    {
        var invoice = NewInvoice(InvoiceType.Standard);
        AddCustomLine(invoice, qty: 1m, unitHt: 1000m, fodecApplicable: true);
        SetStamp(invoice, 1m);

        Assert.Equal(1000m, invoice.SubTotal.Amount);
        Assert.Equal(10m, invoice.FodecAmount.Amount);
        Assert.Equal(191.900m, invoice.TotalVat.Amount);
        Assert.Equal(1m, invoice.FiscalStampAmount.Amount);

        // C'est cette formule que CheckInvoiceCalculations ignore aujourd'hui (cf. lot F1).
        Assert.Equal(
            invoice.SubTotal.Amount
                + invoice.FodecAmount.Amount
                + invoice.TotalVat.Amount
                + invoice.FiscalStampAmount.Amount,
            invoice.TotalAmount.Amount);
        Assert.Equal(1202.900m, invoice.TotalAmount.Amount);
    }

    [Fact]
    public void CreditNote_KeepsLinesPositive_AndSignsHeaderNegative()
    {
        var invoice = NewInvoice(InvoiceType.CreditNote);
        AddCustomLine(invoice, qty: 2m, unitHt: 250m, fodecApplicable: true);
        SetStamp(invoice, -1m); // le résolveur de timbre signe déjà l'avoir

        var line = Assert.Single(invoice.Lines);
        Assert.Equal(500m, line.SubTotal.Amount);
        Assert.Equal(5m, line.FodecAmount.Amount);

        Assert.Equal(-500m, invoice.SubTotal.Amount);
        Assert.Equal(-5m, invoice.FodecAmount.Amount);
        Assert.Equal(-95.950m, invoice.TotalVat.Amount);
        Assert.Equal(-601.950m, invoice.TotalAmount.Amount);
    }

    [Fact]
    public void Invoice_RemovingLine_RenumbersRemainingLinesAndRecomputesTotals()
    {
        var invoice = NewInvoice(InvoiceType.Standard);
        AddCustomLine(invoice, qty: 1m, unitHt: 100m, fodecApplicable: false, designation: "A");
        AddCustomLine(invoice, qty: 1m, unitHt: 200m, fodecApplicable: false, designation: "B");
        AddCustomLine(invoice, qty: 1m, unitHt: 300m, fodecApplicable: false, designation: "C");

        var toRemove = invoice.Lines.Single(l => l.ProductName == "B");
        var removed = invoice.RemoveLine(toRemove.Id);
        Assert.True(removed.IsSuccess, removed.Error?.Description);

        Assert.Equal(new[] { 1, 2 }, invoice.Lines.Select(l => l.LineNumber).OrderBy(n => n).ToArray());
        Assert.Equal(400m, invoice.SubTotal.Amount);
    }

    [Fact]
    public void Invoice_VatBreakdown_GroupsByRate()
    {
        var invoice = NewInvoice(InvoiceType.Standard);
        AddCustomLine(invoice, qty: 1m, unitHt: 100m, fodecApplicable: false, vatRate: VatRate.Standard);
        AddCustomLine(invoice, qty: 1m, unitHt: 100m, fodecApplicable: false, vatRate: VatRate.Standard);
        AddCustomLine(invoice, qty: 1m, unitHt: 100m, fodecApplicable: false, vatRate: VatRate.Reduced);

        var breakdown = invoice.GetVatBreakdown();

        Assert.Equal(38m, breakdown[VatRate.Standard].Amount);
        Assert.Equal(7m, breakdown[VatRate.Reduced].Amount);
    }

    [Fact]
    public void Invoice_ValidatedThenLocked_RefusesFurtherLineEdits()
    {
        var invoice = NewInvoice(InvoiceType.Standard);
        AddCustomLine(invoice, qty: 1m, unitHt: 100m, fodecApplicable: false);

        Assert.True(invoice.Validate().IsSuccess);

        var add = invoice.AddCustomLine(
            "Trop tard", null, 1m, "Unité", Money.Create(50m), VatRate.Standard);
        Assert.True(add.IsFailure);
        Assert.Equal(100m, invoice.SubTotal.Amount);
    }

    // ─────────────────────────── Devis : moteur de calcul ───────────────────────────

    [Fact]
    public void QuoteLine_AppliesDiscountThenVat()
    {
        var quote = NewQuote();
        var product = NewProduct(unitHt: 100m, vatRate: VatRate.Standard, fodecApplicable: true);

        var add = quote.AddLine(product, quantity: 10m, customUnitPrice: null, discountPercent: 10m);
        Assert.True(add.IsSuccess, add.Error?.Description);

        var line = Assert.Single(quote.Lines);
        Assert.Equal(100m, line.DiscountAmount.Amount);
        Assert.Equal(900m, line.SubTotal.Amount);

        // Invariants qui ne changent pas au lot D : quantité, prix, remise et assiette HT.
        Assert.Equal(10m, line.Quantity);
        Assert.Equal(100m, line.UnitPrice.Amount);
        Assert.Equal(10m, line.DiscountPercent);
        Assert.Equal(900m, quote.SubTotal.Amount);
    }

    [Fact]
    public void Quote_HeaderTotal_IsConsistentWithItsOwnComponents()
    {
        var quote = NewQuote();
        var product = NewProduct(unitHt: 500m, vatRate: VatRate.Standard, fodecApplicable: false);

        Assert.True(quote.AddLine(product, quantity: 2m).IsSuccess);

        Assert.Equal(1000m, quote.SubTotal.Amount);
        Assert.Equal(190m, quote.TotalVat.Amount);
        Assert.Equal(1190m, quote.TotalAmount.Amount);
    }

    // ─────────────────────── Bon de livraison : moteur de calcul ───────────────────────

    [Fact]
    public void DeliveryNoteLine_DeliveredTotals_UseDeliveredQuantityNotOrdered()
    {
        var deliveryNote = NewDeliveryNote();
        var product = NewProduct(unitHt: 100m, vatRate: VatRate.Standard, fodecApplicable: false);

        Assert.True(deliveryNote.AddLine(product, orderedQuantity: 10m).IsSuccess);
        var line = Assert.Single(deliveryNote.Lines);

        Assert.True(line.RecordDelivery(deliveredQuantity: 6m).IsSuccess);

        Assert.Equal(1000m, line.TotalHT);          // sur quantité commandée
        Assert.Equal(600m, line.DeliveredTotalHT);  // sur quantité livrée
        Assert.Equal(4m, line.PendingQuantity);
        Assert.False(line.IsFullyDelivered);
    }

    [Fact]
    public void DeliveryNote_PartialDelivery_YieldsPartiallyDeliveredStatus()
    {
        var deliveryNote = NewDeliveryNote();
        var product = NewProduct(unitHt: 100m, vatRate: VatRate.Standard, fodecApplicable: false);

        Assert.True(deliveryNote.AddLine(product, orderedQuantity: 10m).IsSuccess);
        Assert.True(deliveryNote.Confirm().IsSuccess);
        Assert.True(deliveryNote.Lines.Single().RecordDelivery(deliveredQuantity: 6m).IsSuccess);

        var recorded = deliveryNote.RecordDelivery(
            new DateTime(2026, 7, 20), "Réceptionnaire test");
        Assert.True(recorded.IsSuccess, recorded.Error?.Description);

        Assert.Equal(DeliveryNoteStatus.PartiallyDelivered, deliveryNote.Status);
        Assert.True(deliveryNote.Status.CanBeInvoiced());
    }

    // ─────────────────────────────── Fabriques de test ───────────────────────────────

    private static Client NewClient()
    {
        var address = Address.Create("1 rue de la République", "Tunis", "Tunis").Value;
        var email = Email.Create("client@example.com").Value;
        return Client.Create("Client test", ClientType.Individual, address, email).Value;
    }

    private static Product NewProduct(decimal unitHt, VatRate vatRate, bool fodecApplicable) =>
        Product.Create(
            code: "P-CARAC",
            name: "Produit caractérisation",
            type: ProductType.Product,
            unitPrice: Money.Create(unitHt),
            vatRate: vatRate,
            categoryId: Guid.NewGuid(),
            unit: "Unité",
            isFodecApplicable: fodecApplicable).Value;

    private static Invoice NewInvoice(InvoiceType type)
    {
        var prefix = type == InvoiceType.CreditNote ? "AVO" : "FAC";
        var result = Invoice.Create(
            InvoiceNumber.Create(prefix, 2026, 1),
            NewClient(),
            new DateTime(2026, 7, 20),
            type: type);

        Assert.True(result.IsSuccess, result.Error?.Description);
        return result.Value;
    }

    private static Quote NewQuote()
    {
        var result = Quote.Create(
            QuoteNumber.Create("DEV", 2026, 1),
            NewClient(),
            new DateTime(2026, 7, 20),
            new DateTime(2026, 8, 20));

        Assert.True(result.IsSuccess, result.Error?.Description);
        return result.Value;
    }

    private static DeliveryNote NewDeliveryNote()
    {
        var result = DeliveryNote.Create(
            DeliveryNoteNumber.Generate(2026, 1).Value,
            NewClient(),
            new DateTime(2026, 7, 20),
            "12 avenue Habib Bourguiba");

        Assert.True(result.IsSuccess, result.Error?.Description);
        return result.Value;
    }

    private static void AddCustomLine(
        Invoice invoice,
        decimal qty,
        decimal unitHt,
        bool fodecApplicable,
        decimal? discountPercent = null,
        VatRate vatRate = VatRate.Standard,
        string designation = "Article caractérisation")
    {
        var add = invoice.AddCustomLine(
            designation,
            null,
            qty,
            "Unité",
            Money.Create(unitHt),
            vatRate,
            discountPercent,
            isFodecApplicable: fodecApplicable,
            fodecRatePercent: 1m);

        Assert.True(add.IsSuccess, add.Error?.Description);
    }

    private static void SetStamp(Invoice invoice, decimal signedAmount)
    {
        var set = invoice.SetFiscalStampAmount(Money.FromSignedAmount(signedAmount));
        Assert.True(set.IsSuccess, set.Error?.Description);
    }
}
