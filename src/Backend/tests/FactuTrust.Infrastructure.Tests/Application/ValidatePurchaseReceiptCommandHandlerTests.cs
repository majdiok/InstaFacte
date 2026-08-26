using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.PurchaseReceipts.Commands;
using FactuTrust.Domain.Common;
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
        var ctx = BuildContext(orderedQty: 10m, receiveQty: 4m, poNumber: 1, receiptNumber: 1);

        var result = await ctx.Handler.Handle(new ValidatePurchaseReceiptCommand(ctx.Receipt.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PurchaseReceiptStatus.Validated, ctx.Receipt.Status);
        Assert.Equal(4m, ctx.Po.Lines.First().ReceivedQuantity);
        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, ctx.Po.Status);
        ctx.StockRepo.Verify(r => r.AddAsync(It.IsAny<StockItem>(), It.IsAny<CancellationToken>()), Times.Once);
        ctx.ReceiptRepo.Verify(r => r.UpdateAsync(ctx.Receipt, It.IsAny<CancellationToken>()), Times.Once);
        ctx.PoRepo.Verify(r => r.UpdateAsync(ctx.Po, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_FullReceipt_ShouldSetPoStatusReceived()
    {
        var ctx = BuildContext(orderedQty: 10m, receiveQty: 10m, poNumber: 2, receiptNumber: 2);

        var result = await ctx.Handler.Handle(new ValidatePurchaseReceiptCommand(ctx.Receipt.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PurchaseOrderStatus.Received, ctx.Po.Status);
        Assert.Equal(10m, ctx.Po.Lines.First().ReceivedQuantity);
    }

    [Fact]
    public async Task Handle_WhenStockFails_ShouldNotPersistPoOrReceipt()
    {
        var ctx = BuildContext(orderedQty: 4m, receiveQty: 4m, poNumber: 3, receiptNumber: 3, useRealStock: false);
        ctx.Reception.Setup(r => r.ApplyStockEntriesAsync(
                It.IsAny<Warehouse>(),
                It.IsAny<IReadOnlyList<PurchaseReceptionStockLine>>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Error.Validation("Allocations", "Le numéro de lot est obligatoire")));

        var result = await ctx.Handler.Handle(new ValidatePurchaseReceiptCommand(ctx.Receipt.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(0m, ctx.Po.Lines.First().ReceivedQuantity);
        ctx.PoRepo.Verify(r => r.UpdateAsync(It.IsAny<PurchaseOrder>(), It.IsAny<CancellationToken>()), Times.Never);
        ctx.ReceiptRepo.Verify(r => r.UpdateAsync(It.IsAny<PurchaseReceipt>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenPoAlreadyHasUnexplainedQty_ShouldNotDoubleCount()
    {
        var ctx = BuildContext(orderedQty: 4m, receiveQty: 4m, poNumber: 4, receiptNumber: 4);
        Assert.True(ctx.Po.ReceiveGoods([(ctx.PoLineId, 4m)]).IsSuccess);
        Assert.Equal(4m, ctx.Po.Lines.First().ReceivedQuantity);

        var result = await ctx.Handler.Handle(new ValidatePurchaseReceiptCommand(ctx.Receipt.Id), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(4m, ctx.Po.Lines.First().ReceivedQuantity);
        Assert.Equal(PurchaseReceiptStatus.Validated, ctx.Receipt.Status);
        ctx.PoRepo.Verify(r => r.UpdateAsync(It.IsAny<PurchaseOrder>(), It.IsAny<CancellationToken>()), Times.Never);
        ctx.ReceiptRepo.Verify(r => r.UpdateAsync(ctx.Receipt, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAnotherValidatedReceiptAlreadyReceived_ShouldFailOverReceipt()
    {
        var ctx = BuildContext(orderedQty: 4m, receiveQty: 4m, poNumber: 5, receiptNumber: 5);
        Assert.True(ctx.Po.ReceiveGoods([(ctx.PoLineId, 4m)]).IsSuccess);

        var other = PurchaseReceipt.Create(
            PurchaseReceiptNumber.Create("BR", 2026, 50),
            ctx.Supplier,
            ctx.Warehouse,
            DateTime.Today,
            purchaseOrderId: ctx.Po.Id).Value;
        Assert.True(other.AddLine(ctx.Product, 4m, ctx.Price, orderedQuantity: 4m, purchaseOrderLineId: ctx.PoLineId).IsSuccess);
        Assert.True(other.MarkValidated().IsSuccess);

        ctx.ReceiptRepo
            .Setup(r => r.GetByPurchaseOrderIdAsync(ctx.Po.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { other, ctx.Receipt });

        var result = await ctx.Handler.Handle(new ValidatePurchaseReceiptCommand(ctx.Receipt.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("déjà reçue : 4", result.Error.Description);
        Assert.Contains("cette réception : 4", result.Error.Description);
        Assert.Contains("commandée : 4", result.Error.Description);
        Assert.Equal(4m, ctx.Po.Lines.First().ReceivedQuantity);
        ctx.PoRepo.Verify(r => r.UpdateAsync(It.IsAny<PurchaseOrder>(), It.IsAny<CancellationToken>()), Times.Never);
        ctx.ReceiptRepo.Verify(r => r.UpdateAsync(It.IsAny<PurchaseReceipt>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static TestContext BuildContext(
        decimal orderedQty,
        decimal receiveQty,
        int poNumber,
        int receiptNumber,
        bool useRealStock = true)
    {
        var supplier = BuildSupplier();
        var warehouse = Warehouse.Create("WH1", "Principal").Value;
        var category = ProductCategory.Create("C", "Cat").Value;
        var price = Money.Create(100m);
        var product = Product.Create(
            "ART-1", "Papier", ProductType.Product, price, VatRate.Standard,
            category.Id, purchasePrice: price, isStockManaged: true).Value;

        var po = PurchaseOrder.Create(PurchaseOrderNumber.Create("BC", 2026, poNumber), supplier, DateTime.Today).Value;
        Assert.True(po.AddLine(product, orderedQty).IsSuccess);
        Assert.True(po.Confirm().IsSuccess);
        var poLineId = po.Lines.First().Id;

        var receipt = PurchaseReceipt.Create(
            PurchaseReceiptNumber.Create("BR", 2026, receiptNumber),
            supplier,
            warehouse,
            DateTime.Today,
            purchaseOrderId: po.Id).Value;
        Assert.True(receipt.AddLine(product, receiveQty, price, orderedQuantity: orderedQty, purchaseOrderLineId: poLineId).IsSuccess);

        var receiptRepo = new Mock<IPurchaseReceiptRepository>();
        receiptRepo.Setup(r => r.GetByIdWithLinesAsync(receipt.Id, It.IsAny<CancellationToken>())).ReturnsAsync(receipt);
        receiptRepo.Setup(r => r.GetByPurchaseOrderIdAsync(po.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { receipt });

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

        var receptionMock = new Mock<IPurchaseGoodsReceptionService>();
        IPurchaseGoodsReceptionService reception = useRealStock
            ? new PurchaseGoodsReceptionService(
                stockRepo.Object, movementRepo.Object, productRepo.Object, StockTestDoubles.Passthrough(stockRepo.Object))
            : receptionMock.Object;

        if (!useRealStock)
        {
            receptionMock
                .Setup(r => r.ApplyStockEntriesAsync(
                    It.IsAny<Warehouse>(),
                    It.IsAny<IReadOnlyList<PurchaseReceptionStockLine>>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Success());
        }

        var handler = new ValidatePurchaseReceiptCommandHandler(
            receiptRepo.Object,
            poRepo.Object,
            whRepo.Object,
            reception,
            new PassthroughTenantUnitOfWork(),
            new Mock<IAuditService>().Object);

        return new TestContext(handler, receipt, po, poLineId, supplier, warehouse, product, price, receiptRepo, poRepo, stockRepo, receptionMock);
    }

    private sealed record TestContext(
        ValidatePurchaseReceiptCommandHandler Handler,
        PurchaseReceipt Receipt,
        PurchaseOrder Po,
        Guid PoLineId,
        Supplier Supplier,
        Warehouse Warehouse,
        Product Product,
        Money Price,
        Mock<IPurchaseReceiptRepository> ReceiptRepo,
        Mock<IPurchaseOrderRepository> PoRepo,
        Mock<IStockItemRepository> StockRepo,
        Mock<IPurchaseGoodsReceptionService> Reception);

    private sealed class PassthroughTenantUnitOfWork : ITenantUnitOfWork
    {
        public Task<Result> ExecuteAsync(
            Func<CancellationToken, Task<Result>> action,
            CancellationToken cancellationToken = default)
            => action(cancellationToken);

        public Task<Result<T>> ExecuteAsync<T>(
            Func<CancellationToken, Task<Result<T>>> action,
            CancellationToken cancellationToken = default)
            => action(cancellationToken);
    }

    private static Supplier BuildSupplier()
    {
        var address = Address.Create("1 rue", "Tunis", "Tunis").Value;
        var email = Email.Create("s@test.com").Value;
        var nif = NIF.Create("1234567/A/B/C/000").Value;
        return Supplier.Create("Fournisseur", SupplierType.Business, address, email, nif: nif).Value;
    }
}
