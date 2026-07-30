using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

/// <summary>
/// Remise de pied de document (tranche 5B), sur les trois moteurs.
///
/// Décisions actées : la remise réduit la base de TVA (répartie par taux) ET le FODEC, parce
/// qu'elle réduit la base HT réellement facturée. Elle s'applique avant le timbre, qui est un
/// droit fixe.
/// </summary>
public sealed class GlobalDiscountTests
{
    // ─────────────────── Non-régression : sans remise, rien ne bouge ───────────────────

    [Fact]
    public void WithoutGlobalDiscount_InvoiceTotalsAreUnchanged()
    {
        var invoice = NewInvoice();
        AddLine(invoice, qty: 10m, unitHt: 100m, VatRate.Standard, fodec: true);

        // Remise ligne 0, FODEC 1 % : 1000 HT, 10 FODEC, TVA 19 % sur 1010 = 191,900
        Assert.Equal(1000m, invoice.SubTotal.Amount);
        Assert.Equal(10m, invoice.FodecAmount.Amount);
        Assert.Equal(191.9m, invoice.TotalVat.Amount);
        Assert.Equal(0m, invoice.GlobalDiscountAmount.Amount);
        Assert.Equal(0m, Assert.Single(invoice.Lines).AllocatedGlobalDiscount.Amount);
    }

    [Fact]
    public void SettingThenClearingGlobalDiscount_RestoresTheOriginalTotals()
    {
        var invoice = NewInvoice();
        AddLine(invoice, qty: 10m, unitHt: 100m, VatRate.Standard, fodec: true);
        var before = (invoice.SubTotal.Amount, invoice.FodecAmount.Amount, invoice.TotalVat.Amount);

        Assert.True(invoice.SetGlobalDiscount(percent: 10m, amount: null).IsSuccess);
        Assert.True(invoice.SetGlobalDiscount(percent: null, amount: null).IsSuccess);

        Assert.Equal(before.Item1, invoice.SubTotal.Amount);
        Assert.Equal(before.Item2, invoice.FodecAmount.Amount);
        Assert.Equal(before.Item3, invoice.TotalVat.Amount);
    }

    // ─────────────────── La remise réduit HT, FODEC et TVA ───────────────────

    [Fact]
    public void GlobalDiscountPercent_ReducesHtFodecAndVat()
    {
        var invoice = NewInvoice();
        AddLine(invoice, qty: 10m, unitHt: 100m, VatRate.Standard, fodec: true);

        Assert.True(invoice.SetGlobalDiscount(percent: 10m, amount: null).IsSuccess);

        // Base 1000 → remise 100 → HT 900 ; FODEC 1 % = 9 ; TVA 19 % sur 909 = 172,710
        Assert.Equal(100m, invoice.GlobalDiscountAmount.Amount);
        Assert.Equal(900m, invoice.SubTotal.Amount);
        Assert.Equal(9m, invoice.FodecAmount.Amount);
        Assert.Equal(172.71m, invoice.TotalVat.Amount);
        Assert.Equal(1000m, invoice.SubTotalBeforeGlobalDiscount.Amount);
    }

    [Fact]
    public void GlobalDiscountAmount_IsEquivalentToTheSamePercent()
    {
        var byPercent = NewInvoice();
        AddLine(byPercent, qty: 10m, unitHt: 100m, VatRate.Standard, fodec: true);
        byPercent.SetGlobalDiscount(percent: 10m, amount: null);

        var byAmount = NewInvoice();
        AddLine(byAmount, qty: 10m, unitHt: 100m, VatRate.Standard, fodec: true);
        byAmount.SetGlobalDiscount(percent: null, amount: Money.Create(100m));

        Assert.Equal(byPercent.SubTotal.Amount, byAmount.SubTotal.Amount);
        Assert.Equal(byPercent.FodecAmount.Amount, byAmount.FodecAmount.Amount);
        Assert.Equal(byPercent.TotalVat.Amount, byAmount.TotalVat.Amount);
        Assert.Equal(byPercent.TotalAmount.Amount, byAmount.TotalAmount.Amount);
    }

    /// <summary>
    /// Le cœur de la décision : avec des taux mêlés, la remise doit réduire CHAQUE base de taux
    /// au prorata. Sans cela, la ventilation de TVA serait fausse en contrôle.
    /// </summary>
    [Fact]
    public void WithMixedVatRates_EachRateBaseIsReducedProportionally()
    {
        var invoice = NewInvoice();
        AddLine(invoice, qty: 1m, unitHt: 1000m, VatRate.Standard, fodec: false);
        AddLine(invoice, qty: 1m, unitHt: 1000m, VatRate.Reduced, fodec: false);

        invoice.SetGlobalDiscount(percent: 10m, amount: null);

        var breakdown = invoice.GetVatBreakdown();

        // Chaque base passe de 1000 à 900 : TVA 19 % = 171 et TVA 7 % = 63.
        Assert.Equal(171m, breakdown[VatRate.Standard].Amount);
        Assert.Equal(63m, breakdown[VatRate.Reduced].Amount);
        Assert.Equal(1800m, invoice.SubTotal.Amount);
    }

    [Fact]
    public void TheAllocatedPartsSumExactlyToTheAnnouncedDiscount()
    {
        var invoice = NewInvoice();
        AddLine(invoice, qty: 3m, unitHt: 33.333m, VatRate.Standard, fodec: false);
        AddLine(invoice, qty: 7m, unitHt: 11.111m, VatRate.Reduced, fodec: false);
        AddLine(invoice, qty: 1m, unitHt: 0.555m, VatRate.Intermediate, fodec: false);

        invoice.SetGlobalDiscount(percent: 7.5m, amount: null);

        var allocated = invoice.Lines.Sum(l => l.AllocatedGlobalDiscount.Amount);
        Assert.Equal(invoice.GlobalDiscountAmount.Amount, allocated);
        Assert.Equal(
            invoice.SubTotalBeforeGlobalDiscount.Amount - invoice.GlobalDiscountAmount.Amount,
            invoice.SubTotal.Amount);
    }

    [Fact]
    public void TheDiscountAppliesBeforeTheFiscalStamp()
    {
        var invoice = NewInvoice();
        AddLine(invoice, qty: 10m, unitHt: 100m, VatRate.Standard, fodec: false);
        invoice.SetFiscalStampAmount(Money.Create(1m));

        invoice.SetGlobalDiscount(percent: 10m, amount: null);

        // Le timbre est un droit fixe : il ne subit pas la remise.
        Assert.Equal(1m, invoice.FiscalStampAmount.Amount);
        Assert.Equal(900m + 171m + 1m, invoice.TotalAmount.Amount);
    }

    // ─────────────────── Le recalcul suit les lignes ───────────────────

    [Fact]
    public void AddingALineAfterwards_ReallocatesThePercentDiscount()
    {
        var invoice = NewInvoice();
        AddLine(invoice, qty: 1m, unitHt: 1000m, VatRate.Standard, fodec: false);
        invoice.SetGlobalDiscount(percent: 10m, amount: null);
        Assert.Equal(100m, invoice.GlobalDiscountAmount.Amount);

        AddLine(invoice, qty: 1m, unitHt: 1000m, VatRate.Standard, fodec: false);

        // La base a doublé : 10 % en font autant.
        Assert.Equal(200m, invoice.GlobalDiscountAmount.Amount);
        Assert.Equal(1800m, invoice.SubTotal.Amount);
    }

    [Fact]
    public void ADiscountLargerThanTheBase_IsCappedRatherThanGoingNegative()
    {
        var invoice = NewInvoice();
        AddLine(invoice, qty: 1m, unitHt: 100m, VatRate.Standard, fodec: false);

        invoice.SetGlobalDiscount(percent: null, amount: Money.Create(500m));

        Assert.Equal(100m, invoice.GlobalDiscountAmount.Amount);
        Assert.Equal(0m, invoice.SubTotal.Amount);
        Assert.Equal(0m, invoice.TotalVat.Amount);
    }

    // ─────────────────── Garde-fous de saisie ───────────────────

    [Fact]
    public void SettingBothPercentAndAmount_IsRefused()
    {
        var invoice = NewInvoice();
        AddLine(invoice, qty: 1m, unitHt: 100m, VatRate.Standard, fodec: false);

        var result = invoice.SetGlobalDiscount(percent: 10m, amount: Money.Create(50m));

        Assert.True(result.IsFailure);
        Assert.Equal(0m, invoice.GlobalDiscountAmount.Amount);
    }

    [Fact]
    public void APercentAbove100_IsRefused()
    {
        var invoice = NewInvoice();
        AddLine(invoice, qty: 1m, unitHt: 100m, VatRate.Standard, fodec: false);

        Assert.True(invoice.SetGlobalDiscount(percent: 101m, amount: null).IsFailure);
    }

    // ─────────────────── Les trois moteurs restent alignés ───────────────────

    [Fact]
    public void QuoteAndSalesOrderAndInvoice_AgreeToTheMillime()
    {
        var invoice = NewInvoice();
        AddLine(invoice, qty: 7m, unitHt: 123.456m, VatRate.Standard, fodec: true);
        invoice.SetGlobalDiscount(percent: 12.5m, amount: null);

        var quote = NewQuote();
        AddQuoteLine(quote, qty: 7m, unitHt: 123.456m, VatRate.Standard, fodec: true);
        quote.SetGlobalDiscount(percent: 12.5m, amount: null);

        var order = NewSalesOrder();
        AddOrderLine(order, qty: 7m, unitHt: 123.456m, VatRate.Standard, fodec: true);
        order.SetGlobalDiscount(percent: 12.5m, amount: null);

        Assert.Equal(invoice.SubTotal.Amount, quote.SubTotal.Amount);
        Assert.Equal(invoice.SubTotal.Amount, order.SubTotal.Amount);
        Assert.Equal(invoice.FodecAmount.Amount, quote.FodecAmount.Amount);
        Assert.Equal(invoice.FodecAmount.Amount, order.FodecAmount.Amount);
        Assert.Equal(invoice.TotalVat.Amount, quote.TotalVat.Amount);
        Assert.Equal(invoice.TotalVat.Amount, order.TotalVat.Amount);
        Assert.Equal(invoice.GlobalDiscountAmount.Amount, quote.GlobalDiscountAmount.Amount);
        Assert.Equal(invoice.GlobalDiscountAmount.Amount, order.GlobalDiscountAmount.Amount);
    }

    // ─────────────────────────────── Montage ───────────────────────────────

    private static void AddLine(Invoice invoice, decimal qty, decimal unitHt, VatRate rate, bool fodec) =>
        Assert.True(invoice.AddCustomLine(
            "Article", null, qty, "Unité", Money.Create(unitHt), rate, null, fodec, 1m).IsSuccess);

    private static void AddQuoteLine(Quote quote, decimal qty, decimal unitHt, VatRate rate, bool fodec) =>
        Assert.True(quote.AddCustomLine(
            "Article", null, qty, "Unité", Money.Create(unitHt), rate, null, fodec, 1m).IsSuccess);

    /// <summary>
    /// La commande n'a pas de ligne libre : elle part toujours d'un produit. On en fabrique un
    /// portant le même taux et le même FODEC, pour comparer les trois moteurs à données égales.
    /// </summary>
    private static void AddOrderLine(SalesOrder order, decimal qty, decimal unitHt, VatRate rate, bool fodec)
    {
        var product = Product.Create(
            code: "P-REMISE",
            name: "Article",
            type: ProductType.Product,
            unitPrice: Money.Create(unitHt),
            vatRate: rate,
            categoryId: Guid.NewGuid(),
            unit: "Unité",
            isFodecApplicable: fodec).Value;

        Assert.True(order.AddLine(product, qty, null, null, 1m).IsSuccess);
    }

    private static Client NewClient() =>
        Client.Create(
            "Client test", ClientType.Business,
            Address.Create("Rue 1", "Tunis", "Tunis").Value,
            Email.Create("client@test.tn").Value,
            NIF.Create("1234567/A/B/C/000").Value).Value;

    private static Invoice NewInvoice() =>
        Invoice.Create(
            InvoiceNumber.Create("FAC", 2026, 1),
            NewClient(),
            new DateTime(2026, 7, 20)).Value;

    private static Quote NewQuote() =>
        Quote.Create(
            QuoteNumber.Create("DEV", 2026, 1),
            NewClient(),
            new DateTime(2026, 7, 20),
            new DateTime(2026, 8, 20)).Value;

    private static SalesOrder NewSalesOrder() =>
        SalesOrder.Create(
            SalesOrderNumber.Create("CDE", 2026, 1),
            NewClient(),
            new DateTime(2026, 7, 20)).Value;
}
