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

        var lineSelections = po.Lines
            .Where(l => l.ReceivedNotInvoicedQuantity > 0)
            .Select(l => (l.Id, l.ReceivedNotInvoicedQuantity))
            .ToList();

        var invoice = SupplierInvoice.CreateFromPurchaseOrder(
            po,
            "FS-2026-TEST-FF",
            new DateTime(2026, 4, 5),
            lineSelections);

        if (invoice.IsFailure)
            throw new InvalidOperationException(invoice.Error.Description);

        var inv = invoice.Value;
        inv.ApplyLineAssetClassifications(lineSpecs.Select((l, i) => (
            LineNumber: i + 1,
            IsFixedAsset: l.IsAsset,
            AssetAccountNumber: l.IsAsset ? "223" : null,
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
        Assert.Contains(lines, l => l.AccountNumber == "223" && l.Debit == 2000m);
        Assert.Contains(lines, l => l.AccountNumber == "43662" && l.Debit == 380m);
        Assert.Equal(2975m, lines.Single(l => l.AccountNumber == "4011").Credit);
    }

    [Fact]
    public void Build_FixedAssetWithoutAccount_UsesDefault228()
    {
        var invoice = BuildInvoice((true, 2000m));
        invoice.ApplyLineAssetClassifications(
        [
            (LineNumber: 1, IsFixedAsset: true, AssetAccountNumber: null, DepreciationRateCategoryId: (Guid?)null)
        ]);

        var (lines, _) = SupplierInvoiceJournalLineBuilder.Build(invoice);

        Assert.Contains(lines, l => l.AccountNumber == "228" && l.Debit == 2000m);
        Assert.Contains(lines, l => l.AccountNumber == "43662");
    }

    [Fact]
    public void Build_WithFiscalStamp_Debits6654()
    {
        var invoice = BuildInvoice((false, 1000m));
        invoice.SetFiscalStampAmount(1m);

        var (lines, _) = SupplierInvoiceJournalLineBuilder.Build(invoice);

        Assert.Contains(lines, l => l.AccountNumber == "6654" && l.Debit == 1m);
        Assert.Equal(1191m, lines.Single(l => l.AccountNumber == "4011").Credit);
    }

    [Fact]
    public void Build_WithoutProductTypes_Keeps607ForStandaloneServiceSnapshot()
    {
        var invoice = BuildStandaloneInvoice(ProductType.Service, isAsset: false);
        var (lines, totals) = SupplierInvoiceJournalLineBuilder.Build(invoice);

        Assert.Equal(1000m, totals.GoodsHt);
        Assert.Equal(0m, totals.ServicesHt);
        Assert.Contains(lines, l => l.AccountNumber == "607" && l.Debit == 1000m);
        Assert.DoesNotContain(lines, l => l.AccountNumber == "604");
    }

    [Fact]
    public void Build_StandaloneService_WithProductTypes_Posts604()
    {
        var invoice = BuildStandaloneInvoice(ProductType.Service, isAsset: false);
        var types = invoice.Lines.ToDictionary(l => l.ProductId, _ => ProductType.Service);
        var (lines, totals) = SupplierInvoiceJournalLineBuilder.Build(invoice, types);

        Assert.Equal(0m, totals.GoodsHt);
        Assert.Equal(1000m, totals.ServicesHt);
        Assert.Contains(lines, l => l.AccountNumber == "604" && l.Debit == 1000m);
        Assert.Contains(lines, l => l.AccountNumber == "43666" && l.Debit == 190m);
        Assert.Contains(lines, l => l.AccountNumber == "4011" && l.Credit == 1190m);
        Assert.DoesNotContain(lines, l => l.AccountNumber == "607");
    }

    [Fact]
    public void Build_StandaloneNonStockGoods_WithProductTypes_Posts607()
    {
        var invoice = BuildStandaloneInvoice(ProductType.Product, isAsset: false);
        var types = invoice.Lines.ToDictionary(l => l.ProductId, _ => ProductType.Product);
        var (lines, totals) = SupplierInvoiceJournalLineBuilder.Build(invoice, types);

        Assert.Equal(1000m, totals.GoodsHt);
        Assert.Equal(0m, totals.ServicesHt);
        Assert.Contains(lines, l => l.AccountNumber == "607");
        Assert.DoesNotContain(lines, l => l.AccountNumber == "604");
    }

    [Fact]
    public void Build_StandaloneServiceAndAsset_DoesNotMergeAccounts()
    {
        var invoice = BuildStandaloneMixedServiceAndAsset();
        var types = invoice.Lines.ToDictionary(
            l => l.ProductId,
            l => l.IsFixedAsset ? ProductType.Product : ProductType.Service);
        var (lines, totals) = SupplierInvoiceJournalLineBuilder.Build(invoice, types);

        Assert.Equal(0m, totals.GoodsHt);
        Assert.Equal(400m, totals.ServicesHt);
        Assert.Equal(600m, totals.AssetHt);
        Assert.Contains(lines, l => l.AccountNumber == "604" && l.Debit == 400m);
        Assert.Contains(lines, l => l.AccountNumber == "223" && l.Debit == 600m);
        Assert.DoesNotContain(lines, l => l.AccountNumber == "607");
    }

    private static SupplierInvoice BuildStandaloneInvoice(ProductType type, bool isAsset)
    {
        var supplier = BuildSupplier();
        var category = ProductCategory.Create("GEN", "Général").Value;
        var unitPrice = Money.Create(1000m);
        var product = Product.Create(
            "PR-SA-1",
            type == ProductType.Service ? "Loyer" : "Fourniture",
            type,
            unitPrice,
            VatRate.Standard,
            category.Id,
            purchasePrice: unitPrice).Value;

        var invoice = SupplierInvoice.CreateStandalone(
            supplier,
            "FS-2026-SA",
            new DateTime(2026, 8, 17),
            [
                new StandaloneSupplierInvoiceLineInput(
                    product.Id, product.Code, product.Name, null, 1m, product.Unit,
                    unitPrice, product.VatRate)
            ]).Value;

        if (isAsset)
        {
            invoice.ApplyLineAssetClassifications(
            [
                (LineNumber: 1, IsFixedAsset: true, AssetAccountNumber: "223", DepreciationRateCategoryId: (Guid?)null)
            ]);
        }

        return invoice;
    }

    private static SupplierInvoice BuildStandaloneMixedServiceAndAsset()
    {
        var supplier = BuildSupplier();
        var category = ProductCategory.Create("GEN", "Général").Value;
        var service = Product.Create(
            "SVC-1", "Honoraires", ProductType.Service, Money.Create(400m), VatRate.Standard,
            category.Id, purchasePrice: Money.Create(400m)).Value;
        var asset = Product.Create(
            "IMMO-1", "Matériel", ProductType.Product, Money.Create(600m), VatRate.Standard,
            category.Id, purchasePrice: Money.Create(600m)).Value;

        var invoice = SupplierInvoice.CreateStandalone(
            supplier,
            "FS-2026-MIX",
            new DateTime(2026, 8, 17),
            [
                new StandaloneSupplierInvoiceLineInput(
                    service.Id, service.Code, service.Name, null, 1m, service.Unit,
                    Money.Create(400m), service.VatRate),
                new StandaloneSupplierInvoiceLineInput(
                    asset.Id, asset.Code, asset.Name, null, 1m, asset.Unit,
                    Money.Create(600m), asset.VatRate)
            ]).Value;

        invoice.ApplyLineAssetClassifications(
        [
            (LineNumber: 2, IsFixedAsset: true, AssetAccountNumber: "223", DepreciationRateCategoryId: (Guid?)null)
        ]);

        return invoice;
    }

    private static Supplier BuildSupplier()
    {
        var address = Address.Create("1 rue de test", "Tunis", "Tunis").Value;
        var email = Email.Create("supplier@test.com").Value;
        var nif = NIF.Create("1234567/A/B/C/000").Value;
        return Supplier.Create("Ste test FF", SupplierType.Business, address, email, nif: nif).Value;
    }
}