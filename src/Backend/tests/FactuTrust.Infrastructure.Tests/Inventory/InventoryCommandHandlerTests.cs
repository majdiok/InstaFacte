using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Features.Inventory.Commands;
using FactuTrust.Infrastructure.Tests.Stock;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;
using FactuTrust.Domain.ValueObjects;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Inventory;

public sealed class InventoryCommandHandlerTests
{
    private static Product CreateStockManagedProduct()
    {
        var categoryId = Guid.NewGuid();
        var result = Product.Create(
            "TST001",
            "Produit test stock",
            ProductType.Product,
            Money.Create(10m, Money.DefaultCurrency),
            VatRate.Standard,
            categoryId,
            isStockManaged: true);

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    [Fact]
    public async Task StartInventory_Complete_WarehouseHasNoStockItems_ButCatalogHasManagedProducts_Succeeds()
    {
        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var product = CreateStockManagedProduct();

        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.TenantId).Returns(tenantId);

        var productRepo = new Mock<IProductRepository>();
        productRepo
            .Setup(p => p.GetStockManagedProductsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });

        var stockRepo = new Mock<IStockItemRepository>();
        stockRepo
            .Setup(s => s.GetByWarehouseForInventoryAsync(warehouseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<StockItem>());

        var invRepo = new Mock<IPhysicalInventoryRepository>();
        invRepo.Setup(i => i.HasActiveInventoryAsync(warehouseId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        invRepo.Setup(i => i.GetNextReferenceAsync(It.IsAny<CancellationToken>())).ReturnsAsync("INVE-000001");
        invRepo
            .Setup(i => i.AddAsync(It.IsAny<PhysicalInventory>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PhysicalInventory inv, CancellationToken _) => inv);

        var warehouseRepo = new Mock<IWarehouseRepository>();

        var numberService = new Mock<IDocumentNumberService>();
        numberService
            .Setup(n => n.ReserveNextAsync(tenantId, NumberingDocumentType.PhysicalInventory, It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentNumberResult("INVE-000001", DateTime.UtcNow.Year, 1, "INVE"));

        var handler = new StartInventoryCommandHandler(
            invRepo.Object,
            stockRepo.Object,
            warehouseRepo.Object,
            productRepo.Object,
            tenant.Object,
            numberService.Object,
            StockTestDoubles.EmptyLots());

        var result = await handler.Handle(
            new StartInventoryCommand(InventoryType.Complete, warehouseId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.TotalProducts);
        Assert.Equal(product.Id, result.Value.Products[0].ProductId);
        Assert.Equal(0m, result.Value.Products[0].TheoreticalQuantity);
    }

    [Fact]
    public async Task StartInventory_Complete_NoStockManagedProductsInCatalog_Fails()
    {
        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.TenantId).Returns(tenantId);

        var productRepo = new Mock<IProductRepository>();
        productRepo
            .Setup(p => p.GetStockManagedProductsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Product>());

        var stockRepo = new Mock<IStockItemRepository>();
        var invRepo = new Mock<IPhysicalInventoryRepository>();
        invRepo.Setup(i => i.HasActiveInventoryAsync(warehouseId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var warehouseRepo = new Mock<IWarehouseRepository>();

        var numberService = new Mock<IDocumentNumberService>();
        numberService
            .Setup(n => n.ReserveNextAsync(tenantId, NumberingDocumentType.PhysicalInventory, It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentNumberResult("INVE-000001", DateTime.UtcNow.Year, 1, "INVE"));

        var handler = new StartInventoryCommandHandler(
            invRepo.Object,
            stockRepo.Object,
            warehouseRepo.Object,
            productRepo.Object,
            tenant.Object,
            numberService.Object,
            StockTestDoubles.EmptyLots());

        var result = await handler.Handle(
            new StartInventoryCommand(InventoryType.Complete, warehouseId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("catalogue", result.Error.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateInventory_LineWithDifference_NoStockItem_CreatesStockItemAndAdjusts()
    {
        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var product = CreateStockManagedProduct();

        var start = PhysicalInventory.Start(
            "INVE-000001",
            warehouseId,
            InventoryType.Complete,
            new[] { (product.Id, product.Name, (string?)product.Code, 0m) },
            null);
        Assert.True(start.IsSuccess);
        var inventory = start.Value;
        Assert.True(inventory.RecordCount(product.Id, 7m).IsSuccess);

        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.TenantId).Returns(tenantId);

        var invRepo = new Mock<IPhysicalInventoryRepository>();
        invRepo
            .Setup(i => i.GetWithLinesAsync(inventory.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inventory);
        invRepo.Setup(i => i.UpdateAsync(It.IsAny<PhysicalInventory>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var stockRepo = new Mock<IStockItemRepository>();
        stockRepo
            .Setup(s => s.GetByProductAndWarehouseAsync(product.Id, warehouseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StockItem?)null);
        stockRepo
            .Setup(s => s.AddAsync(It.IsAny<StockItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((StockItem si, CancellationToken _) => si);
        stockRepo
            .Setup(s => s.UpdateAsync(It.IsAny<StockItem>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new ValidateInventoryCommandHandler(
            invRepo.Object,
            stockRepo.Object,
            new Mock<IProductRepository>().Object,
            StockTestDoubles.Passthrough(stockRepo.Object),
            tenant.Object);

        var result = await handler.Handle(new ValidateInventoryCommand(inventory.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.ProductsWithChanges);
        stockRepo.Verify(s => s.AddAsync(It.IsAny<StockItem>(), It.IsAny<CancellationToken>()), Times.Once);
        stockRepo.Verify(s => s.UpdateAsync(It.IsAny<StockItem>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ValidateInventory_NoCounts_ConfirmsTheoretical_NoStockAdjustments()
    {
        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var product = CreateStockManagedProduct();

        var start = PhysicalInventory.Start(
            "INVE-000002",
            warehouseId,
            InventoryType.Complete,
            new[] { (product.Id, product.Name, (string?)product.Code, 4m) },
            null);
        Assert.True(start.IsSuccess);
        var inventory = start.Value;

        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.TenantId).Returns(tenantId);

        var invRepo = new Mock<IPhysicalInventoryRepository>();
        invRepo
            .Setup(i => i.GetWithLinesAsync(inventory.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inventory);
        invRepo.Setup(i => i.UpdateAsync(It.IsAny<PhysicalInventory>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var stockRepo = new Mock<IStockItemRepository>();
        var handler = new ValidateInventoryCommandHandler(
            invRepo.Object,
            stockRepo.Object,
            new Mock<IProductRepository>().Object,
            StockTestDoubles.Passthrough(stockRepo.Object),
            tenant.Object);

        var result = await handler.Handle(new ValidateInventoryCommand(inventory.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.ProductsWithChanges);
        Assert.Equal(InventoryStatus.Validated, inventory.Status);
        Assert.All(inventory.CountLines, line => Assert.Equal(line.TheoreticalQuantity, line.CountedQuantity));
        stockRepo.Verify(s => s.AddAsync(It.IsAny<StockItem>(), It.IsAny<CancellationToken>()), Times.Never);
        stockRepo.Verify(s => s.UpdateAsync(It.IsAny<StockItem>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ValidateInventory_PendingCounts_AppliesDirtyThenValidates()
    {
        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var counted = CreateStockManagedProduct();
        var confirmed = CreateStockManagedProduct();

        var start = PhysicalInventory.Start(
            "INVE-000003",
            warehouseId,
            InventoryType.Complete,
            new[]
            {
                (counted.Id, counted.Name, (string?)counted.Code, 10m),
                (confirmed.Id, confirmed.Name, (string?)confirmed.Code, 5m)
            },
            null);
        Assert.True(start.IsSuccess);
        var inventory = start.Value;

        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.TenantId).Returns(tenantId);

        var invRepo = new Mock<IPhysicalInventoryRepository>();
        invRepo
            .Setup(i => i.GetWithLinesAsync(inventory.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inventory);
        invRepo.Setup(i => i.UpdateAsync(It.IsAny<PhysicalInventory>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var stockRepo = new Mock<IStockItemRepository>();
        stockRepo
            .Setup(s => s.GetByProductAndWarehouseAsync(counted.Id, warehouseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StockItem?)null);
        stockRepo
            .Setup(s => s.AddAsync(It.IsAny<StockItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((StockItem si, CancellationToken _) => si);
        stockRepo
            .Setup(s => s.UpdateAsync(It.IsAny<StockItem>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new ValidateInventoryCommandHandler(
            invRepo.Object,
            stockRepo.Object,
            new Mock<IProductRepository>().Object,
            StockTestDoubles.Passthrough(stockRepo.Object),
            tenant.Object);

        var result = await handler.Handle(
            new ValidateInventoryCommand(
                inventory.Id,
                new[] { new InventoryPendingCount(counted.Id, 13m) }),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.ProductsWithChanges);
        Assert.Equal(13m, inventory.CountLines.Single(l => l.ProductId == counted.Id).CountedQuantity);
        Assert.Equal(5m, inventory.CountLines.Single(l => l.ProductId == confirmed.Id).CountedQuantity);
        stockRepo.Verify(s => s.AddAsync(It.IsAny<StockItem>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ValidateInventory_PendingCounts_UnknownProduct_FailsAndStaysInProgress()
    {
        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var product = CreateStockManagedProduct();

        var start = PhysicalInventory.Start(
            "INVE-000004",
            warehouseId,
            InventoryType.Complete,
            new[] { (product.Id, product.Name, (string?)product.Code, 2m) },
            null);
        Assert.True(start.IsSuccess);
        var inventory = start.Value;

        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.TenantId).Returns(tenantId);

        var invRepo = new Mock<IPhysicalInventoryRepository>();
        invRepo
            .Setup(i => i.GetWithLinesAsync(inventory.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inventory);

        var stockRepo = new Mock<IStockItemRepository>();
        var handler = new ValidateInventoryCommandHandler(
            invRepo.Object,
            stockRepo.Object,
            new Mock<IProductRepository>().Object,
            StockTestDoubles.Passthrough(stockRepo.Object),
            tenant.Object);

        var result = await handler.Handle(
            new ValidateInventoryCommand(
                inventory.Id,
                new[] { new InventoryPendingCount(Guid.NewGuid(), 3m) }),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(InventoryStatus.InProgress, inventory.Status);
        invRepo.Verify(i => i.UpdateAsync(It.IsAny<PhysicalInventory>(), It.IsAny<CancellationToken>()), Times.Never);
        stockRepo.Verify(s => s.AddAsync(It.IsAny<StockItem>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
