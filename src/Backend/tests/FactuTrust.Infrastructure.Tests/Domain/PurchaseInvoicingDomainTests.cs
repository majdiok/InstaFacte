using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

public sealed class PurchaseInvoicingDomainTests
{
    [Fact]
    public void ApplyInvoicing_PartialQuantity_SetsPartiallyInvoicedStatus()
    {
        var (po, lineId) = BuildReceivedPurchaseOrder(receivedQty: 10m);

        var result = po.ApplyInvoicing([(lineId, 4m)], Guid.NewGuid());

        Assert.True(result.IsSuccess);
        Assert.Equal(4m, po.Lines.First().InvoicedQuantity);
        Assert.Equal(6m, po.Lines.First().ReceivedNotInvoicedQuantity);
        Assert.Equal(PurchaseOrderStatus.PartiallyInvoiced, po.Status);
    }

    [Fact]
    public void ApplyInvoicing_FullRemaining_SetsInvoicedStatus()
    {
        var (po, lineId) = BuildReceivedPurchaseOrder(receivedQty: 10m);

        Assert.True(po.ApplyInvoicing([(lineId, 10m)], Guid.NewGuid()).IsSuccess);

        Assert.Equal(PurchaseOrderStatus.Invoiced, po.Status);
        Assert.Equal(0m, po.TotalReceivedNotInvoicedQuantity);
    }

    [Fact]
    public void ReverseInvoicing_RestoresQuantitiesAndStatus()
    {
        var (po, lineId) = BuildReceivedPurchaseOrder(receivedQty: 10m);
        var invoiceId = Guid.NewGuid();
        Assert.True(po.ApplyInvoicing([(lineId, 10m)], invoiceId).IsSuccess);

        var reverse = po.ReverseInvoicing([(lineId, 10m)]);

        Assert.True(reverse.IsSuccess);
        Assert.Equal(0m, po.Lines.First().InvoicedQuantity);
        Assert.Equal(PurchaseOrderStatus.Received, po.Status);
    }

    [Fact]
    public void ApplyInvoicing_CannotExceedReceivedQuantity()
    {
        var (po, lineId) = BuildReceivedPurchaseOrder(receivedQty: 5m);

        var result = po.ApplyInvoicing([(lineId, 6m)], Guid.NewGuid());

        Assert.True(result.IsFailure);
    }

    private static (PurchaseOrder Po, Guid LineId) BuildReceivedPurchaseOrder(decimal receivedQty)
    {
        var supplier = BuildSupplier();
        var category = ProductCategory.Create("C", "Cat").Value;
        var price = Money.Create(100m);
        var product = Product.Create(
            "ART-1", "Article", ProductType.Product, price, VatRate.Standard,
            category.Id, purchasePrice: price).Value;

        var po = PurchaseOrder.Create(PurchaseOrderNumber.Create("BC", 2026, 1), supplier, DateTime.Today).Value;
        Assert.True(po.AddLine(product, 10m).IsSuccess);
        Assert.True(po.Confirm().IsSuccess);
        var lineId = po.Lines.First().Id;
        Assert.True(po.ReceiveGoods([(lineId, receivedQty)]).IsSuccess);

        return (po, lineId);
    }

    private static Supplier BuildSupplier()
    {
        var address = Address.Create("1 rue", "Tunis", "Tunis").Value;
        var email = Email.Create("s@test.com").Value;
        var nif = NIF.Create("1234567/A/B/C/000").Value;
        return Supplier.Create("Fournisseur", SupplierType.Business, address, email, nif: nif).Value;
    }
}
