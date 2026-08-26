using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

public sealed class PurchaseReceiptDomainTests
{
    [Fact]
    public void Create_WithValidData_ShouldBeDraft()
    {
        var (receipt, _) = BuildDraftReceipt();
        Assert.Equal(PurchaseReceiptStatus.Draft, receipt.Status);
        Assert.Single(receipt.Lines);
        Assert.True(receipt.SubTotal.Amount > 0);
    }

    [Fact]
    public void MarkValidated_WhenDraftWithLines_ShouldSucceed()
    {
        var (receipt, _) = BuildDraftReceipt();
        var result = receipt.MarkValidated();
        Assert.True(result.IsSuccess);
        Assert.Equal(PurchaseReceiptStatus.Validated, receipt.Status);
        Assert.NotNull(receipt.ValidatedAt);
    }

    [Fact]
    public void MarkValidated_WhenNoLines_ShouldFail()
    {
        var supplier = BuildSupplier();
        var warehouse = Warehouse.Create("WH", "Entrepôt").Value;
        var number = PurchaseReceiptNumber.Create("BR", 2026, 1);
        var receipt = PurchaseReceipt.Create(number, supplier, warehouse, DateTime.Today).Value;

        var result = receipt.MarkValidated();
        Assert.True(result.IsFailure);
        Assert.Equal(PurchaseReceiptStatus.Draft, receipt.Status);
    }

    [Fact]
    public void Cancel_Validated_ShouldSucceed()
    {
        var (receipt, _) = BuildDraftReceipt();
        Assert.True(receipt.MarkValidated().IsSuccess);

        var result = receipt.Cancel("Erreur de saisie");
        Assert.True(result.IsSuccess);
        Assert.Equal(PurchaseReceiptStatus.Cancelled, receipt.Status);
        Assert.Equal("Erreur de saisie", receipt.CancellationReason);
    }

    [Fact]
    public void Line_WithDiscount_ShouldReduceSubTotal()
    {
        var (receipt, product) = BuildDraftReceipt(discountPercent: 10m);
        var line = receipt.Lines.First();
        var expectedGross = product.GetPurchasePrice().Amount * 2m;
        Assert.Equal(Math.Round(expectedGross * 0.9m, 3), line.SubTotal.Amount);
    }

    [Fact]
    public void ReceiveGoods_WhenExceedsOrdered_ShouldReportAlreadyReceivedAndThisReceipt()
    {
        var (order, lineId) = BuildConfirmedPoWithReception();
        var result = order.ReceiveGoods([(lineId, 4m)]);

        Assert.True(result.IsFailure);
        Assert.Contains("déjà reçue : 2", result.Error.Description);
        Assert.Contains("cette réception : 4", result.Error.Description);
        Assert.Contains("commandée : 5", result.Error.Description);
    }

    [Fact]
    public void ReverseGoodsReception_ShouldRestoreConfirmedWhenNoRemaining()
    {
        var (order, lineId) = BuildConfirmedPoWithReception();
        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, order.Status);

        var reverse = order.ReverseGoodsReception([(lineId, 2m)]);
        Assert.True(reverse.IsSuccess);
        Assert.Equal(PurchaseOrderStatus.Confirmed, order.Status);
        Assert.Equal(0m, order.Lines.First().ReceivedQuantity);
    }

    private static (PurchaseReceipt Receipt, Product Product) BuildDraftReceipt(decimal? discountPercent = null)
    {
        var supplier = BuildSupplier();
        var warehouse = Warehouse.Create("WH1", "Entrepôt Principal").Value;
        var category = ProductCategory.Create("CAT", "Cat").Value;
        var unitPrice = Money.Create(100m);
        var product = Product.Create(
            "ART-1", "Article test", ProductType.Product, unitPrice, VatRate.Standard,
            category.Id, purchasePrice: unitPrice, isStockManaged: true).Value;

        var number = PurchaseReceiptNumber.Create("BR", 2026, 67);
        var receipt = PurchaseReceipt.Create(
            number, supplier, warehouse, new DateTime(2026, 5, 15),
            supplierReference: "BL-4587",
            transporterName: "MAGHREB TRANS").Value;

        Assert.True(receipt.AddLine(product, 2m, unitPrice, orderedQuantity: 5m, discountPercent: discountPercent).IsSuccess);
        return (receipt, product);
    }

    private static Supplier BuildSupplier()
    {
        var address = Address.Create("ZI", "Monastir", "Monastir").Value;
        var email = Email.Create("fournisseur@test.com").Value;
        var nif = NIF.Create("1234567/A/B/C/000").Value;
        return Supplier.Create("FOURNITURES PLUS", SupplierType.Business, address, email, nif: nif).Value;
    }

    private static (PurchaseOrder Order, Guid LineId) BuildConfirmedPoWithReception()
    {
        var supplier = BuildSupplier();
        var category = ProductCategory.Create("C", "C").Value;
        var price = Money.Create(50m);
        var product = Product.Create("P1", "P1", ProductType.Product, price, VatRate.Standard, category.Id, purchasePrice: price).Value;
        var po = PurchaseOrder.Create(PurchaseOrderNumber.Create("BC", 2026, 1), supplier, DateTime.Today).Value;
        Assert.True(po.AddLine(product, 5m).IsSuccess);
        Assert.True(po.Confirm().IsSuccess);
        var lineId = po.Lines.First().Id;
        Assert.True(po.ReceiveGoods([(lineId, 2m)]).IsSuccess);
        return (po, lineId);
    }
}
