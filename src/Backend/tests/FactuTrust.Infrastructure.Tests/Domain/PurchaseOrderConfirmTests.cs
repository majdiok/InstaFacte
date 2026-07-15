using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

public sealed class PurchaseOrderConfirmTests
{
    [Fact]
    public void Confirm_WhenDraftWithLines_ShouldSucceed()
    {
        var purchaseOrder = CreateDraftOrderWithLine();

        var result = purchaseOrder.Confirm();

        Assert.True(result.IsSuccess);
        Assert.Equal(PurchaseOrderStatus.Confirmed, purchaseOrder.Status);
        Assert.NotNull(purchaseOrder.ConfirmedAt);
    }

    [Fact]
    public void Confirm_WhenNotDraft_ShouldFailWithStatusValidation()
    {
        var purchaseOrder = CreateDraftOrderWithLine();
        var firstConfirm = purchaseOrder.Confirm();
        Assert.True(firstConfirm.IsSuccess);

        var result = purchaseOrder.Confirm();

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.Status", result.Error.Code);
    }

    [Fact]
    public void Confirm_WhenNoLines_ShouldFailWithLinesValidation()
    {
        var purchaseOrder = CreateDraftOrderWithoutLine();

        var result = purchaseOrder.Confirm();

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.Lines", result.Error.Code);
    }

    private static PurchaseOrder CreateDraftOrderWithLine()
    {
        var supplier = CreateSupplier();
        var product = CreateProduct();
        var number = PurchaseOrderNumber.Create("BC", 2026, 1);
        var createResult = PurchaseOrder.Create(number, supplier, new DateTime(2026, 4, 1));
        var order = createResult.Value;

        var addLine = order.AddLine(product, 2m);
        Assert.True(addLine.IsSuccess);

        return order;
    }

    private static PurchaseOrder CreateDraftOrderWithoutLine()
    {
        var supplier = CreateSupplier();
        var number = PurchaseOrderNumber.Create("BC", 2026, 2);
        var createResult = PurchaseOrder.Create(number, supplier, new DateTime(2026, 4, 1));
        return createResult.Value;
    }

    private static Supplier CreateSupplier()
    {
        var address = Address.Create("1 rue test", "Tunis", "Tunis").Value;
        var email = Email.Create("supplier.confirm@test.com").Value;
        var nif = NIF.Create("1234567/A/B/C/000").Value;
        return Supplier.Create("Supplier confirm", SupplierType.Business, address, email, nif: nif).Value;
    }

    private static Product CreateProduct()
    {
        var category = ProductCategory.Create("POC", "PO category").Value;
        var unitPrice = Money.Create(100m, Money.DefaultCurrency);
        return Product.Create(
            "PR-PO-1",
            "Produit test",
            ProductType.Product,
            unitPrice,
            VatRate.Standard,
            category.Id,
            purchasePrice: unitPrice).Value;
    }
}
