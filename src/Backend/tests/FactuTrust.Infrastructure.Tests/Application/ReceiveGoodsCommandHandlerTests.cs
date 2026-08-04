using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.PurchaseOrders.Commands;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.Services;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

public sealed class ReceiveGoodsCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenExplicitWarehouseNotFound_ShouldReturnNotFound()
    {
        var order = BuildConfirmedOrderWithLine();
        var line = order.Lines.First();
        var missingWarehouseId = Guid.NewGuid();

        var poRepo = new Mock<IPurchaseOrderRepository>();
        poRepo.Setup(x => x.GetByIdWithLinesAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var whRepo = new Mock<IWarehouseRepository>();
        whRepo.Setup(x => x.GetByIdAsync(missingWarehouseId, It.IsAny<CancellationToken>())).ReturnsAsync((Warehouse?)null);

        var handler = CreateHandler(poRepo, whRepo, new Mock<IStockItemRepository>(), new Mock<IAuditService>());

        var dto = new ReceiveGoodsDto
        {
            WarehouseId = missingWarehouseId,
            Lines = new[] { new ReceiveGoodsLineDto { LineId = line.Id, ReceivedQuantity = 1m } }
        };

        var result = await handler.Handle(new ReceiveGoodsCommand(order.Id, dto), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Warehouse.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task Handle_WhenExplicitWarehouseInactive_ShouldReturnValidation()
    {
        var order = BuildConfirmedOrderWithLine();
        var line = order.Lines.First();
        var wh = Warehouse.Create("WH1", "Warehouse 1").Value;
        wh.Deactivate();

        var poRepo = new Mock<IPurchaseOrderRepository>();
        poRepo.Setup(x => x.GetByIdWithLinesAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var whRepo = new Mock<IWarehouseRepository>();
        whRepo.Setup(x => x.GetByIdAsync(wh.Id, It.IsAny<CancellationToken>())).ReturnsAsync(wh);

        var handler = CreateHandler(poRepo, whRepo, new Mock<IStockItemRepository>(), new Mock<IAuditService>());

        var dto = new ReceiveGoodsDto
        {
            WarehouseId = wh.Id,
            Lines = new[] { new ReceiveGoodsLineDto { LineId = line.Id, ReceivedQuantity = 1m } }
        };

        var result = await handler.Handle(new ReceiveGoodsCommand(order.Id, dto), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.WarehouseId", result.Error.Code);
    }

    [Fact]
    public async Task Handle_WithExplicitActiveWarehouse_ShouldUpdateStockAndPurchaseOrder()
    {
        var (order, product) = BuildConfirmedOrderWithStockManagedLine();
        var line = order.Lines.First();
        var wh = Warehouse.Create("WH1", "Warehouse 1").Value;

        var poRepo = new Mock<IPurchaseOrderRepository>();
        poRepo.Setup(x => x.GetByIdWithLinesAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var whRepo = new Mock<IWarehouseRepository>();
        whRepo.Setup(x => x.GetByIdAsync(wh.Id, It.IsAny<CancellationToken>())).ReturnsAsync(wh);

        var stockRepo = new Mock<IStockItemRepository>();
        stockRepo
            .Setup(x => x.GetByProductAndWarehouseAsync(line.ProductId, wh.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StockItem?)null);

        var productRepo = new Mock<IProductRepository>();
        productRepo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var movementRepo = new Mock<IStockMovementRepository>();
        movementRepo
            .Setup(x => x.GetByReferenceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<StockMovement>());

        var audit = new Mock<IAuditService>();

        var handler = CreateHandler(poRepo, whRepo, stockRepo, audit, productRepo, movementRepo);

        var dto = new ReceiveGoodsDto
        {
            WarehouseId = wh.Id,
            Lines = new[] { new ReceiveGoodsLineDto { LineId = line.Id, ReceivedQuantity = 1m } }
        };

        var result = await handler.Handle(new ReceiveGoodsCommand(order.Id, dto), CancellationToken.None);

        Assert.True(result.IsSuccess);
        stockRepo.Verify(x => x.AddAsync(It.IsAny<StockItem>(), It.IsAny<CancellationToken>()), Times.Once);
        poRepo.Verify(x => x.UpdateAsync(order, It.IsAny<CancellationToken>()), Times.Once);
        audit.Verify(
            x => x.LogAsync(
                It.IsAny<string>(),
                "PurchaseOrder",
                order.Id,
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNoWarehouseResolvable_ShouldReturnValidationWithGuidanceMessage()
    {
        var order = BuildConfirmedOrderWithLine();
        var line = order.Lines.First();

        var poRepo = new Mock<IPurchaseOrderRepository>();
        poRepo.Setup(x => x.GetByIdWithLinesAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var whRepo = new Mock<IWarehouseRepository>();
        whRepo.Setup(x => x.GetDefaultAsync(It.IsAny<CancellationToken>())).ReturnsAsync((Warehouse?)null);

        var handler = CreateHandler(poRepo, whRepo, new Mock<IStockItemRepository>(), new Mock<IAuditService>());

        var dto = new ReceiveGoodsDto
        {
            Lines = new[] { new ReceiveGoodsLineDto { LineId = line.Id, ReceivedQuantity = 1m } }
        };

        var result = await handler.Handle(new ReceiveGoodsCommand(order.Id, dto), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.Warehouse", result.Error.Code);
        Assert.Contains("Paramètres", result.Error.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Handle_WhenPoWarehouseInactive_ShouldFallbackToDefaultWarehouse()
    {
        var inactive = Warehouse.Create("OLD", "Old warehouse").Value;
        inactive.Deactivate();

        var defaultWh = Warehouse.Create("DEF", "Default").Value;
        defaultWh.SetAsDefault();

        var (order, product) = BuildConfirmedOrderWithStockManagedLine(inactive.Id);
        var line = order.Lines.First();

        var poRepo = new Mock<IPurchaseOrderRepository>();
        poRepo.Setup(x => x.GetByIdWithLinesAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var whRepo = new Mock<IWarehouseRepository>();
        whRepo.Setup(x => x.GetByIdAsync(inactive.Id, It.IsAny<CancellationToken>())).ReturnsAsync(inactive);
        whRepo.Setup(x => x.GetDefaultAsync(It.IsAny<CancellationToken>())).ReturnsAsync(defaultWh);

        var stockRepo = new Mock<IStockItemRepository>();
        stockRepo
            .Setup(x => x.GetByProductAndWarehouseAsync(line.ProductId, defaultWh.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StockItem?)null);

        var productRepo = new Mock<IProductRepository>();
        productRepo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var movementRepo = new Mock<IStockMovementRepository>();
        movementRepo
            .Setup(x => x.GetByReferenceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<StockMovement>());

        var handler = CreateHandler(poRepo, whRepo, stockRepo, new Mock<IAuditService>(), productRepo, movementRepo);

        var dto = new ReceiveGoodsDto
        {
            Lines = new[] { new ReceiveGoodsLineDto { LineId = line.Id, ReceivedQuantity = 1m } }
        };

        var result = await handler.Handle(new ReceiveGoodsCommand(order.Id, dto), CancellationToken.None);

        Assert.True(result.IsSuccess);
        stockRepo.Verify(
            x => x.GetByProductAndWarehouseAsync(line.ProductId, defaultWh.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static ReceiveGoodsCommandHandler CreateHandler(
        Mock<IPurchaseOrderRepository> poRepo,
        Mock<IWarehouseRepository> whRepo,
        Mock<IStockItemRepository> stockRepo,
        Mock<IAuditService> audit,
        Mock<IProductRepository>? productRepo = null,
        Mock<IStockMovementRepository>? movementRepo = null)
    {
        productRepo ??= new Mock<IProductRepository>();
        movementRepo ??= new Mock<IStockMovementRepository>();
        movementRepo
            .Setup(x => x.GetByReferenceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<StockMovement>());

        IPurchaseGoodsReceptionService receptionService = new PurchaseGoodsReceptionService(
            stockRepo.Object,
            movementRepo.Object,
            productRepo.Object);

        return new ReceiveGoodsCommandHandler(
            poRepo.Object,
            whRepo.Object,
            receptionService,
            audit.Object);
    }

    private static PurchaseOrder BuildConfirmedOrderWithLine(Guid? warehouseId = null)
    {
        var (order, _) = BuildConfirmedOrderWithStockManagedLine(warehouseId, stockManaged: false);
        return order;
    }

    private static (PurchaseOrder Order, Product Product) BuildConfirmedOrderWithStockManagedLine(
        Guid? warehouseId = null,
        bool stockManaged = true)
    {
        var address = Address.Create("1 rue test", "Tunis", "Tunis").Value;
        var email = Email.Create("supplier.recv@test.com").Value;
        var nif = NIF.Create("1234567/A/B/C/000").Value;
        var supplier = Supplier.Create("Supplier recv", SupplierType.Business, address, email, nif: nif).Value;

        var category = ProductCategory.Create("RCV", "Recv category").Value;
        var unitPrice = Money.Create(50m, Money.DefaultCurrency);
        var product = Product.Create(
            "PR-RCV-1",
            "Produit réception",
            ProductType.Product,
            unitPrice,
            VatRate.Standard,
            category.Id,
            purchasePrice: unitPrice,
            isStockManaged: stockManaged).Value;

        var number = PurchaseOrderNumber.Create("BC", 2026, 200);
        var createResult = PurchaseOrder.Create(number, supplier, new DateTime(2026, 4, 1), warehouseId: warehouseId);
        var order = createResult.Value;
        Assert.True(order.AddLine(product, 5m).IsSuccess);
        Assert.True(order.Confirm().IsSuccess);
        return (order, product);
    }
}
