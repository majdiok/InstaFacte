using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Stock;

public sealed class StockMutationServicePassthroughTests
{
    [Fact]
    public async Task ApplyAsync_Entry_UpdatesCmupLikeRecordEntry()
    {
        var factory = CreateFactory();
        var (productId, warehouseId) = await SeedProductAndWarehouse(factory);
        var sut = new StockMutationService(factory, Options.Create(new StockTraceabilityOptions()));

        var first = await sut.ApplyAsync(new StockMutationRequest
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            Kind = StockMutationKind.Entry,
            Quantity = 10m,
            UnitCost = 2m,
            Reason = MovementReason.Purchase
        });
        Assert.True(first.IsSuccess, first.Error?.Description);
        Assert.Equal(2m, first.Value.AverageCost);

        var second = await sut.ApplyAsync(new StockMutationRequest
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            Kind = StockMutationKind.Entry,
            Quantity = 10m,
            UnitCost = 4m,
            Reason = MovementReason.Purchase
        });
        Assert.True(second.IsSuccess);
        Assert.Equal(3m, second.Value.AverageCost);
        Assert.Equal(20m, second.Value.QuantityOnHand);
    }

    [Fact]
    public async Task ApplyAsync_Exit_InsufficientStock_Fails()
    {
        var factory = CreateFactory();
        var (productId, warehouseId) = await SeedProductAndWarehouse(factory);
        var sut = new StockMutationService(factory, Options.Create(new StockTraceabilityOptions()));

        await sut.ApplyAsync(new StockMutationRequest
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            Kind = StockMutationKind.Entry,
            Quantity = 2m,
            UnitCost = 1m,
            Reason = MovementReason.InitialStock
        });

        var exit = await sut.ApplyAsync(new StockMutationRequest
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            Kind = StockMutationKind.Exit,
            Quantity = 5m,
            Reason = MovementReason.Sale
        });

        Assert.True(exit.IsFailure);
        Assert.Contains("Stock insuffisant", exit.Error.Description);
    }

    [Fact]
    public void ApplyPassthrough_Exit_AcceptsShortfallQuantity()
    {
        var item = StockItem.Create(Guid.NewGuid(), Guid.NewGuid()).Value;
        Assert.True(item.RecordEntry(10m, 5m, MovementReason.InitialStock).IsSuccess);

        var result = StockMutationService.ApplyPassthrough(item, new StockMutationRequest
        {
            ProductId = item.ProductId,
            WarehouseId = item.WarehouseId,
            Kind = StockMutationKind.Exit,
            Quantity = 4m,
            Reason = MovementReason.Sale,
            ShortfallQuantity = 2m
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(6m, item.QuantityOnHand);
        Assert.Equal(2m, item.Movements.Last().ShortfallQuantity);
    }

    [Fact]
    public async Task ApplyAsync_Fifo_ExitConsumesOldestLayers()
    {
        var factory = CreateFactory();
        var (productId, warehouseId) = await SeedProductAndWarehouse(factory, costing: CostingMethod.Fifo);
        var sut = new StockMutationService(factory, Options.Create(new StockTraceabilityOptions
        {
            FifoLifoValuationEnabled = true
        }));

        Assert.True((await sut.ApplyAsync(Entry(productId, warehouseId, 10m, 2m))).IsSuccess);
        Assert.True((await sut.ApplyAsync(Entry(productId, warehouseId, 10m, 4m))).IsSuccess);

        var exit = await sut.ApplyAsync(new StockMutationRequest
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            Kind = StockMutationKind.Exit,
            Quantity = 12m,
            Reason = MovementReason.Sale
        });
        Assert.True(exit.IsSuccess, exit.Error?.Description);

        await using var ctx = factory.CreateContext();
        var item = await ctx.StockItems.Include(s => s.Movements).FirstAsync(s => s.ProductId == productId);
        var exitMovements = item.Movements.Where(m => m.Type == MovementType.Exit).ToList();
        var exitValue = exitMovements.Sum(m => Math.Abs(m.Quantity) * m.UnitCost);
        Assert.Equal(28m, exitValue);
        Assert.Equal(8m, item.QuantityOnHand);
        Assert.Equal(4m, item.AverageCost);
    }

    [Fact]
    public async Task ApplyAsync_Fefo_ExitConsumesSoonestExpiryLot()
    {
        var factory = CreateFactory();
        var (productId, warehouseId) = await SeedProductAndWarehouse(
            factory,
            tracking: TrackingMode.Lot,
            picking: PickingPolicy.Fefo,
            hasExpiry: true);
        var sut = new StockMutationService(factory, Options.Create(new StockTraceabilityOptions
        {
            LotTrackingEnabled = true,
            ExpiryTrackingEnabled = true
        }));

        Assert.True((await sut.ApplyAsync(new StockMutationRequest
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            Kind = StockMutationKind.Entry,
            Quantity = 10m,
            UnitCost = 2m,
            Reason = MovementReason.Purchase,
            Allocations = new[]
            {
                new StockAllocationInput(10m, LotNumber: "L-OLD", ExpiryDate: DateTime.UtcNow.AddDays(5))
            }
        })).IsSuccess);

        Assert.True((await sut.ApplyAsync(new StockMutationRequest
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            Kind = StockMutationKind.Entry,
            Quantity = 10m,
            UnitCost = 2m,
            Reason = MovementReason.Purchase,
            Allocations = new[]
            {
                new StockAllocationInput(10m, LotNumber: "L-NEW", ExpiryDate: DateTime.UtcNow.AddDays(40))
            }
        })).IsSuccess);

        var exit = await sut.ApplyAsync(new StockMutationRequest
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            Kind = StockMutationKind.Exit,
            Quantity = 6m,
            Reason = MovementReason.Sale
        });
        Assert.True(exit.IsSuccess, exit.Error?.Description);

        await using var ctx = factory.CreateContext();
        var lots = await ctx.ProductLots.ToListAsync();
        var oldLot = lots.Single(l => l.LotNumber == "L-OLD");
        var newLot = lots.Single(l => l.LotNumber == "L-NEW");
        var item = await ctx.StockItems.FirstAsync(s => s.ProductId == productId);
        var oldBal = await ctx.StockLotBalances.SingleAsync(b => b.ProductLotId == oldLot.Id);
        var newBal = await ctx.StockLotBalances.SingleAsync(b => b.ProductLotId == newLot.Id);
        Assert.Equal(4m, oldBal.QuantityOnHand);
        Assert.Equal(10m, newBal.QuantityOnHand);
        Assert.Equal(14m, item.QuantityOnHand);
    }

    [Fact]
    public async Task CreateOpeningValuationLayers_CreatesLayerThenFifoExitUsesIt()
    {
        var factory = CreateFactory();
        var (productId, warehouseId) = await SeedProductAndWarehouse(factory);
        var average = new StockMutationService(factory, Options.Create(new StockTraceabilityOptions()));
        Assert.True((await average.ApplyAsync(Entry(productId, warehouseId, 10m, 3m))).IsSuccess);

        var fifo = new StockMutationService(factory, Options.Create(new StockTraceabilityOptions
        {
            FifoLifoValuationEnabled = true
        }));
        var opening = await fifo.CreateOpeningValuationLayersAsync(productId, CostingMethod.Fifo);
        Assert.True(opening.IsSuccess, opening.Error?.Description);

        var extra = await fifo.ApplyAsync(Entry(productId, warehouseId, 10m, 5m));
        Assert.True(extra.IsSuccess, extra.Error?.Description);

        var exit = await fifo.ApplyAsync(new StockMutationRequest
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            Kind = StockMutationKind.Exit,
            Quantity = 12m,
            Reason = MovementReason.Sale
        });
        Assert.True(exit.IsSuccess, exit.Error?.Description);

        await using var ctx = factory.CreateContext();
        var item = await ctx.StockItems.Include(s => s.Movements).FirstAsync(s => s.ProductId == productId);
        var exitValue = item.Movements.Where(m => m.Type == MovementType.Exit)
            .Sum(m => Math.Abs(m.Quantity) * m.UnitCost);
        Assert.Equal(40m, exitValue);
    }

    [Fact]
    public async Task ApplyAsync_Lifo_ExitConsumesNewestLayers()
    {
        var factory = CreateFactory();
        var (productId, warehouseId) = await SeedProductAndWarehouse(factory, costing: CostingMethod.Lifo);
        var sut = new StockMutationService(factory, Options.Create(new StockTraceabilityOptions
        {
            FifoLifoValuationEnabled = true
        }));

        Assert.True((await sut.ApplyAsync(Entry(productId, warehouseId, 10m, 2m))).IsSuccess);
        Assert.True((await sut.ApplyAsync(Entry(productId, warehouseId, 10m, 4m))).IsSuccess);

        var exit = await sut.ApplyAsync(new StockMutationRequest
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            Kind = StockMutationKind.Exit,
            Quantity = 12m,
            Reason = MovementReason.Sale
        });
        Assert.True(exit.IsSuccess, exit.Error?.Description);

        await using var ctx = factory.CreateContext();
        var item = await ctx.StockItems.Include(s => s.Movements).FirstAsync(s => s.ProductId == productId);
        var exitValue = item.Movements.Where(m => m.Type == MovementType.Exit)
            .Sum(m => Math.Abs(m.Quantity) * m.UnitCost);
        Assert.Equal(44m, exitValue);
    }

    private static StockMutationRequest Entry(Guid productId, Guid warehouseId, decimal qty, decimal cost) =>
        new()
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            Kind = StockMutationKind.Entry,
            Quantity = qty,
            UnitCost = cost,
            Reason = MovementReason.Purchase
        };

    private static InMemoryTenantDbContextFactory CreateFactory() =>
        new(Guid.NewGuid().ToString());

    private static async Task<(Guid ProductId, Guid WarehouseId)> SeedProductAndWarehouse(
        InMemoryTenantDbContextFactory factory,
        CostingMethod costing = CostingMethod.Average,
        TrackingMode tracking = TrackingMode.None,
        PickingPolicy picking = PickingPolicy.None,
        bool hasExpiry = false)
    {
        await using var ctx = factory.CreateContext();
        var category = ProductCategory.Create("CAT", "Cat").Value;
        ctx.ProductCategories.Add(category);
        var product = Product.Create(
            "SKU-1",
            "Article",
            ProductType.Product,
            Money.Create(10m, Money.DefaultCurrency),
            VatRate.Standard,
            category.Id,
            isStockManaged: true).Value;
        if (costing != CostingMethod.Average || tracking != TrackingMode.None || picking != PickingPolicy.None || hasExpiry)
            Assert.True(product.ConfigureTraceability(tracking, hasExpiry, picking, costing, hasExpiry ? 30 : null).IsSuccess);
        ctx.Products.Add(product);
        var warehouse = Warehouse.Create("WH1", "Principal", isDefault: true).Value;
        ctx.Warehouses.Add(warehouse);
        await ctx.SaveChangesAsync();
        return (product.Id, warehouse.Id);
    }
}

internal sealed class InMemoryTenantDbContextFactory : ITenantDbContextFactory
{
    private readonly string _name;

    public InMemoryTenantDbContextFactory(string name) => _name = name;

    public TenantDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseInMemoryDatabase(_name)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TenantDbContext(options);
    }
}
