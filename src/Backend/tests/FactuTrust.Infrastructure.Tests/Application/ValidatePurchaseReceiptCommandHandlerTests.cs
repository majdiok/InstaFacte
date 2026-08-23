using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.PurchaseReceipts.Commands;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.Services;
using FactuTrust.Infrastructure.Tests.Stock;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

public sealed class ValidatePurchaseReceiptCommandHandlerTests
{
    [Fact]
    public async Task Handle_ShouldUpdatePoReceivedQuantityAndCreateStock()
    {
        var supplier = BuildSupplier();
        var warehouse = Warehouse.Create("WH1", "Principal").Value;
        var category = ProductCategory.Create("C", "Cat").Value;
        var price = Money.Create(100m);
        var product = Product.Create(
            "ART-1", "Papier", ProductType.Product, price, VatRate.Standard,
            category.Id, purchasePrice: price, isStockManaged: true).Value;

        var po = PurchaseOrder.Create(PurchaseOrderNumber.Create("BC", 2026, 1), supplier, DateTime.Today).Value;
        Assert.True(po.AddLine(product, 10m).IsSuccess);
        Assert.True(po.Confirm().IsSuccess);
        var poLineId = po.Lines.First().Id;

        var receipt = PurchaseReceipt.Create(
            PurchaseReceiptNumber.Create("BR", 2026, 1),
            supplier,
            warehouse,
            DateTime.Today,
            purchaseOrderId: po.Id).Value;
        Assert.True(receipt.AddLine(product, 4m, price, orderedQuantity: 10m, purchaseOrderLineId: poLineId).IsSuccess);

        var receiptRepo = new Mock<IPurchaseReceiptRepository>();
        receiptRepo.Setup(r => r.GetByIdWithLinesAsync(receipt.Id, It.IsAny<CancellationToken>())).ReturnsAsync(receipt);

        var poRepo = new Mock<IPurchaseOrderRepository>();
        poRepo.Setup(r => r.GetByIdWithLinesAsync(po.Id, It.IsAny<CancellationToken>())).ReturnsAsync(po);

        var whRepo = new Mock<IWarehouseRepository>();
        whRepo.Setup(r => r.GetByIdAsync(warehouse.Id, It.IsAny<CancellationToken>())).ReturnsAsync(warehouse);

        var stockRepo = new Mock<IStockItemRepository>();
        stockRepo
            .Setup(r => r.GetByProductAndWarehouseAsync(product.Id, warehouse.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StockItem?)null);

        var productRepo = new Mock<IProductRepository>();
        productRepo.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var movementRepo = new Mock<IStockMovementRepository>();
        movementRepo
            .Setup(r => r.GetByReferenceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<StockMovement>());

        IPurchaseGoodsReceptionService reception = new PurchaseGoodsReceptionService(
            stockRepo.Object, movementRepo.Object, productRepo.Object, StockTestDoubles.Passthrough(stockRepo.Object));

        var handler = new ValidatePurchaseReceiptCommandHandler(
            receiptRepo.Object,
            poRepo.Object,
            whRepo.Object,
            reception,
            new Mock<IAuditService>().Object);

        var result = await handler.Handle(new ValidatePurchaseReceiptCommand(receipt.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PurchaseReceiptStatus.Validated, receipt.Status);
        Assert.Equal(4m, po.Lines.First().ReceivedQuantity);
        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, po.Status);
        stockRepo.Verify(r => r.AddAsync(It.IsAny<StockItem>(), It.IsAny<CancellationToken>()), Times.Once);
        receiptRepo.Verify(r => r.UpdateAsync(receipt, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_FullReceipt_ShouldSetPoStatusReceived()
    {
        var supplier = BuildSupplier();
        var warehouse = Warehouse.Create("WH1", "Principal").Value;
        var category = ProductCategory.Create("C", "Cat").Value;
        var price = Money.Create(100m);
        var product = Product.Create(
            "ART-1", "Papier", ProductType.Product, price, VatRate.Standard,
            category.Id, purchasePrice: price, isStockManaged: true).Value;

        var po = PurchaseOrder.Create(PurchaseOrderNumber.Create("BC", 2026, 2), supplier, DateTime.Today).Value;
        Assert.True(po.AddLine(product, 10m).IsSuccess);
        Assert.True(po.Confirm().IsSuccess);
        var poLineId = po.Lines.First().Id;

        var receipt = PurchaseReceipt.Create(
            PurchaseReceiptNumber.Create("BR", 2026, 2),
            supplier,
            warehouse,
            DateTime.Today,
            purchaseOrderId: po.Id).Value;
        Assert.True(receipt.AddLine(product, 10m, price, orderedQuantity: 10m, purchaseOrderLineId: poLineId).IsSuccess);

        var receiptRepo = new Mock<IPurchaseReceiptRepository>();
        receiptRepo.Setup(r => r.GetByIdWithLinesAsync(receipt.Id, It.IsAny<CancellationToken>())).ReturnsAsync(receipt);

        var poRepo = new Mock<IPurchaseOrderRepository>();
        poRepo.Setup(r => r.GetByIdWithLinesAsync(po.Id, It.IsAny<CancellationToken>())).ReturnsAsync(po);

        var whRepo = new Mock<IWarehouseRepository>();
        whRepo.Setup(r => r.GetByIdAsync(warehouse.Id, It.IsAny<CancellationToken>())).ReturnsAsync(warehouse);

        var stockRepo = new Mock<IStockItemRepository>();
        stockRepo
            .Setup(r => r.GetByProductAndWarehouseAsync(product.Id, warehouse.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StockItem?)null);

        var productRepo = new Mock<IProductRepository>();
        productRepo.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var movementRepo = new Mock<IStockMovementRepository>();
        movementRepo
            .Setup(r => r.GetByReferenceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<StockMovement>());

        IPurchaseGoodsReceptionService reception = new PurchaseGoodsReceptionService(
            stockRepo.Object, movementRepo.Object, productRepo.Object, StockTestDoubles.Passthrough(stockRepo.Object));

        var handler = new ValidatePurchaseReceiptCommandHandler(
            receiptRepo.Object,
            poRepo.Object,
            whRepo.Object,
            reception,
            new Mock<IAuditService>().Object);

        var result = await handler.Handle(new ValidatePurchaseReceiptCommand(receipt.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PurchaseOrderStatus.Received, po.Status);
        Assert.Equal(10m, po.Lines.First().ReceivedQuantity);
    }

    private static Supplier BuildSupplier()
    {
        var address = Address.Create("1 rue", "Tunis", "Tunis").Value;
        var email = Email.Create("s@test.com").Value;
        var nif = NIF.Create("1234567/A/B/C/000").Value;
        return Supplier.Create("Fournisseur", SupplierType.Business, address, email, nif: nif).Value;
    }
}
