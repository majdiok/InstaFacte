using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

public sealed class SupplierInvoiceStandaloneTests
{
    [Fact]
    public void CreateStandalone_ServiceLine_ComputesTotalsAndLeavesSourcesNull()
    {
        var supplier = BuildSupplier();
        var product = BuildServiceProduct(1000m);

        var result = SupplierInvoice.CreateStandalone(
            supplier,
            "FS-2026-000001",
            new DateTime(2026, 8, 17),
            [
                new StandaloneSupplierInvoiceLineInput(
                    product.Id,
                    product.Code,
                    product.Name,
                    product.Description,
                    Quantity: 1m,
                    product.Unit,
                    product.GetPurchasePrice(),
                    product.VatRate)
            ],
            paymentTermDays: 30,
            externalReference: "STEG-08-2026");

        Assert.True(result.IsSuccess);
        var invoice = result.Value;
        Assert.Null(invoice.PurchaseOrderId);
        Assert.Null(invoice.SourcePurchaseReceiptId);
        Assert.Equal(supplier.Id, invoice.SupplierId);
        Assert.Equal(1000m, invoice.SubTotal.Amount);
        Assert.Equal(190m, invoice.TotalVat.Amount);
        Assert.Equal(1190m, invoice.TotalAmount.Amount);
        Assert.Equal(new DateTime(2026, 9, 16), invoice.DueDate);
        Assert.Empty(invoice.GetPurchaseOrderImputations());
        Assert.Empty(invoice.GetPurchaseReceiptImputations());
        Assert.Null(Assert.Single(invoice.Lines).PurchaseOrderLineId);
        Assert.Null(Assert.Single(invoice.Lines).PurchaseReceiptLineId);
        Assert.Equal("STEG-08-2026", invoice.ExternalReference);
    }

    [Fact]
    public void CreateStandalone_AppliesLineDiscountIntoSubTotal()
    {
        var supplier = BuildSupplier();
        var product = BuildServiceProduct(1000m);

        var result = SupplierInvoice.CreateStandalone(
            supplier,
            "FS-2026-000002",
            new DateTime(2026, 8, 17),
            [
                new StandaloneSupplierInvoiceLineInput(
                    product.Id,
                    product.Code,
                    product.Name,
                    null,
                    Quantity: 1m,
                    product.Unit,
                    product.GetPurchasePrice(),
                    product.VatRate,
                    DiscountPercent: 10m)
            ]);

        Assert.True(result.IsSuccess);
        Assert.Equal(900m, result.Value.SubTotal.Amount);
        Assert.Equal(171m, result.Value.TotalVat.Amount);
        Assert.Equal(1071m, result.Value.TotalAmount.Amount);
    }

    [Fact]
    public void CreateStandalone_RejectsEmptyLines()
    {
        var result = SupplierInvoice.CreateStandalone(
            BuildSupplier(),
            "FS-2026-000003",
            new DateTime(2026, 8, 17),
            Array.Empty<StandaloneSupplierInvoiceLineInput>());

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void CreateStandalone_RejectsZeroQuantity()
    {
        var product = BuildServiceProduct(100m);
        var result = SupplierInvoice.CreateStandalone(
            BuildSupplier(),
            "FS-2026-000004",
            new DateTime(2026, 8, 17),
            [
                new StandaloneSupplierInvoiceLineInput(
                    product.Id,
                    product.Code,
                    product.Name,
                    null,
                    Quantity: 0m,
                    product.Unit,
                    product.GetPurchasePrice(),
                    product.VatRate)
            ]);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void CreateStandalone_RejectsEmptyProductId()
    {
        var result = SupplierInvoice.CreateStandalone(
            BuildSupplier(),
            "FS-2026-000005",
            new DateTime(2026, 8, 17),
            [
                new StandaloneSupplierInvoiceLineInput(
                    Guid.Empty,
                    "X",
                    "Charge",
                    null,
                    Quantity: 1m,
                    "Unité",
                    Money.Create(50m),
                    VatRate.Standard)
            ]);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void CreateStandalone_RejectsDiscountOutsideRange()
    {
        var product = BuildServiceProduct(100m);
        var over = SupplierInvoice.CreateStandalone(
            BuildSupplier(),
            "FS-2026-000006",
            new DateTime(2026, 8, 17),
            [
                new StandaloneSupplierInvoiceLineInput(
                    product.Id, product.Code, product.Name, null, 1m, product.Unit,
                    product.GetPurchasePrice(), product.VatRate, DiscountPercent: 101m)
            ]);
        var under = SupplierInvoice.CreateStandalone(
            BuildSupplier(),
            "FS-2026-000007",
            new DateTime(2026, 8, 17),
            [
                new StandaloneSupplierInvoiceLineInput(
                    product.Id, product.Code, product.Name, null, 1m, product.Unit,
                    product.GetPurchasePrice(), product.VatRate, DiscountPercent: -1m)
            ]);

        Assert.True(over.IsFailure);
        Assert.True(under.IsFailure);
    }

    [Fact]
    public void CreateStandalone_RejectsEmptyInvoiceNumber()
    {
        var product = BuildServiceProduct(100m);
        var result = SupplierInvoice.CreateStandalone(
            BuildSupplier(),
            "  ",
            new DateTime(2026, 8, 17),
            [
                new StandaloneSupplierInvoiceLineInput(
                    product.Id, product.Code, product.Name, null, 1m, product.Unit,
                    product.GetPurchasePrice(), product.VatRate)
            ]);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Cancel_StandaloneInvoice_SucceedsWithoutImputations()
    {
        var product = BuildServiceProduct(100m);
        var invoice = SupplierInvoice.CreateStandalone(
            BuildSupplier(),
            "FS-2026-000008",
            new DateTime(2026, 8, 17),
            [
                new StandaloneSupplierInvoiceLineInput(
                    product.Id, product.Code, product.Name, null, 1m, product.Unit,
                    product.GetPurchasePrice(), product.VatRate)
            ]).Value;

        var cancel = invoice.Cancel("Saisie erronée");

        Assert.True(cancel.IsSuccess);
        Assert.Equal(SupplierInvoiceStatus.Cancelled, invoice.Status);
        Assert.Empty(invoice.GetPurchaseOrderImputations());
        Assert.Empty(invoice.GetPurchaseReceiptImputations());
    }

    private static Supplier BuildSupplier()
    {
        var address = Address.Create("1 rue", "Tunis", "Tunis").Value;
        var email = Email.Create("s@test.com").Value;
        var nif = NIF.Create("1234567/A/B/C/000").Value;
        return Supplier.Create("STEG", SupplierType.Business, address, email, nif: nif).Value;
    }

    private static Product BuildServiceProduct(decimal purchasePrice)
    {
        var category = ProductCategory.Create("CHG", "Charges").Value;
        var price = Money.Create(purchasePrice);
        return Product.Create(
            "LOYER",
            "Loyer",
            ProductType.Service,
            price,
            VatRate.Standard,
            category.Id,
            purchasePrice: price).Value;
    }
}
