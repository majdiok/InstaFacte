using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.Services;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

public sealed class SupplierInvoiceJournalLineBuilderTests
{
    private static SupplierInvoice BuildInvoice(params (bool IsAsset, decimal UnitPriceHt)[] lineSpecs)
    {
        var address = Address.Create("1 rue de test", "Tunis", "Tunis").Value;
        var email = Email.Create("supplier@test.com").Value;
        var nif = NIF.Create("1234567/A/B/C/000").Value;
        var supplier = Supplier.Create("Ste test FF", SupplierType.Business, address, email, nif: nif).Value;

        var category = ProductCategory.Create("GEN", "Général").Value;
        var poNumber = PurchaseOrderNumber.Create("BC", 2026, 500001);
        var po = PurchaseOrder.Create(poNumber, supplier, new DateTime(2026, 4, 1)).Value;

        var lineIndex = 0;
        foreach (var spec in lineSpecs)
        {
            var unitPrice = Money.Create(spec.UnitPriceHt, Money.DefaultCurrency);
            var product = Product.Create(
                $"PR-FF-{lineIndex++}",
                "Article test",
                ProductType.Product,
                unitPrice,
                VatRate.Standard,
                category.Id,
                purchasePrice: unitPrice).Value;

            var addLine = po.AddLine(product, 1m);
            if (addLine.IsFailure)
                throw new InvalidOperationException(addLine.Error.Description);
        }

        var confirm = po.Confirm();
        if (confirm.IsFailure)
            throw new InvalidOperationException(confirm.Error.Description);

        var receiveLines = po.Lines.Select(l => (l.Id, l.Quantity)).ToArray();
        var receive = po.ReceiveGoods(receiveLines);
        if (receive.IsFailure)
            throw new InvalidOperationException(receive.Error.Description);

        var invoice = SupplierInvoice.CreateFromPurchaseOrder(
            po,
            "FS-2026-TEST-FF",
            new DateTime(2026, 4, 5));

        if (invoice.IsFailure)
            throw new InvalidOperationException(invoice.Error.Description);

        var inv = invoice.Value;
        inv.ApplyLineAssetClassifications(lineSpecs.Select((l, i) => (
            LineNumber: i + 1,
            IsFixedAsset: l.IsAsset,
            AssetAccountNumber: l.IsAsset ? "213" : null,
            DepreciationRateCategoryId: (Guid?)null)).ToList());

        return inv;
    }

    [Fact]
    public void Build_AllGoods_ShouldMatchClassic607Pattern()
    {
        var invoice = BuildInvoice((false, 1000m));
        var (lines, totals) = SupplierInvoiceJournalLineBuilder.Build(invoice);

        Assert.Equal(1000m, totals.GoodsHt);
        Assert.Equal(0m, totals.AssetHt);
        Assert.Contains(lines, l => l.AccountNumber == "607" && l.Debit == 1000m);
        Assert.Contains(lines, l => l.AccountNumber == "43666" && l.Debit == 190m);
        Assert.Contains(lines, l => l.AccountNumber == "4011" && l.Credit == 1190m);
        Assert.DoesNotContain(lines, l => l.AccountNumber == "43662");
    }

    [Fact]
    public void Build_MixedLines_ShouldSplitAccounts()
    {
        var invoice = BuildInvoice((false, 500m), (true, 2000m));
        var (lines, totals) = SupplierInvoiceJournalLineBuilder.Build(invoice);

        Assert.Equal(500m, totals.GoodsHt);
        Assert.Equal(2000m, totals.AssetHt);
        Assert.Equal(380m, totals.AssetVat);
        Assert.Contains(lines, l => l.AccountNumber == "607");
        Assert.Contains(lines, l => l.AccountNumber == "43666");
        Assert.Contains(lines, l => l.AccountNumber == "213" && l.Debit == 2000m);
        Assert.Contains(lines, l => l.AccountNumber == "43662" && l.Debit == 380m);
        Assert.Equal(2975m, lines.Single(l => l.AccountNumber == "4011").Credit);
    }
}