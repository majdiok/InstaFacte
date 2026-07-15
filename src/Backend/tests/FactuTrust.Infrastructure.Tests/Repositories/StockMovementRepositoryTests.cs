using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Repositories;

public sealed class StockMovementRepositoryTests : IDisposable
{
    private readonly string _databaseName;
    private readonly TestTenantDbContextFactory _contextFactory;
    private readonly StockMovementRepository _repository;

    public StockMovementRepositoryTests()
    {
        _databaseName = $"TestDb_StockSnapshot_{Guid.NewGuid()}";
        _contextFactory = new TestTenantDbContextFactory(_databaseName);
        _repository = new StockMovementRepository(_contextFactory);
    }

    private sealed class TestTenantDbContextFactory : ITenantDbContextFactory
    {
        private readonly string _databaseName;

        public TestTenantDbContextFactory(string databaseName) => _databaseName = databaseName;

        public TenantDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseInMemoryDatabase(databaseName: _databaseName)
                .Options;
            return new TenantDbContext(options);
        }
    }

    [Fact]
    public async Task GetStockSnapshotAtDateAsync_ShouldUseLastMovementOnOrBefore_AsOf_EndOfDay()
    {
        var (_, _, stockItem) = await SeedProductWarehouseAndStockItemAsync(applyStock: s =>
        {
            Assert.True(s.RecordEntry(10m, 2m, MovementReason.Purchase).IsSuccess);
            Assert.True(s.RecordEntry(5m, 2m, MovementReason.Purchase).IsSuccess);
        });

        var movements = await LoadMovementsOrderedAsync(stockItem.Id);
        Assert.Equal(2, movements.Count);

        await PatchMovementOccurredAtAsync(movements[0].Id, new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc));
        await PatchMovementOccurredAtAsync(movements[1].Id, new DateTime(2026, 1, 20, 12, 0, 0, DateTimeKind.Utc));

        var asOfMidJanuary = new DateTime(2026, 1, 15, 23, 59, 59, 999, DateTimeKind.Utc);
        var rowsMid = await _repository.GetStockSnapshotAtDateAsync(asOfMidJanuary, warehouseId: null);
        Assert.Single(rowsMid);
        Assert.Equal(10m, rowsMid[0].Quantity);

        var asOfEndJanuary = new DateTime(2026, 1, 25, 23, 59, 59, 999, DateTimeKind.Utc);
        var rowsEnd = await _repository.GetStockSnapshotAtDateAsync(asOfEndJanuary, warehouseId: null);
        Assert.Single(rowsEnd);
        Assert.Equal(15m, rowsEnd[0].Quantity);
        Assert.Equal("SnapProd", rowsEnd[0].ProductName);
        Assert.Equal("Entrepôt A", rowsEnd[0].WarehouseName);
    }

    [Fact]
    public async Task GetStockSnapshotAtDateAsync_WhenWarehouseIdProvided_ShouldFilterByWarehouse()
    {
        var (product, warehouseA, stockItemA) = await SeedProductWarehouseAndStockItemAsync(
            code: "WHA",
            name: "Entrepôt A",
            applyStock: s => Assert.True(s.RecordEntry(3m, 1m, MovementReason.Purchase).IsSuccess));

        var warehouseBResult = Warehouse.Create("WHB", "Entrepôt B");
        Assert.True(warehouseBResult.IsSuccess);
        var warehouseB = warehouseBResult.Value;

        var itemBResult = StockItem.Create(product.Id, warehouseB.Id);
        Assert.True(itemBResult.IsSuccess);
        Assert.True(itemBResult.Value.RecordEntry(7m, 1m, MovementReason.Purchase).IsSuccess);

        using (var ctx = _contextFactory.CreateContext())
        {
            ctx.Warehouses.Add(warehouseB);
            ctx.StockItems.Add(itemBResult.Value);
            await ctx.SaveChangesAsync();
        }

        var t = new DateTime(2026, 2, 1, 12, 0, 0, DateTimeKind.Utc);
        foreach (var id in new[] { stockItemA.Id, itemBResult.Value.Id })
        {
            var list = await LoadMovementsOrderedAsync(id);
            foreach (var m in list)
                await PatchMovementOccurredAtAsync(m.Id, t);
        }

        var asOf = new DateTime(2026, 2, 5, 23, 59, 59, 999, DateTimeKind.Utc);
        var filtered = await _repository.GetStockSnapshotAtDateAsync(asOf, warehouseA.Id);
        Assert.Single(filtered);
        Assert.Equal(3m, filtered[0].Quantity);
        Assert.Equal("Entrepôt A", filtered[0].WarehouseName);
    }

    [Fact]
    public async Task GetStockSnapshotAtDateAsync_WhenLatestBalanceIsZero_ShouldExcludeRow()
    {
        var (_, _, stockItem) = await SeedProductWarehouseAndStockItemAsync(applyStock: s =>
        {
            Assert.True(s.RecordEntry(5m, 1m, MovementReason.Purchase).IsSuccess);
            Assert.True(s.RecordExit(5m, MovementReason.Sale).IsSuccess);
        });

        var movements = await LoadMovementsOrderedAsync(stockItem.Id);
        Assert.Equal(2, movements.Count);
        await PatchMovementOccurredAtAsync(movements[0].Id, new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc));
        await PatchMovementOccurredAtAsync(movements[1].Id, new DateTime(2026, 3, 2, 10, 0, 0, DateTimeKind.Utc));

        var asOf = new DateTime(2026, 3, 10, 23, 59, 59, 999, DateTimeKind.Utc);
        var rows = await _repository.GetStockSnapshotAtDateAsync(asOf, null);
        Assert.Empty(rows);
    }

    private async Task<(Product Product, Warehouse Warehouse, StockItem StockItem)> SeedProductWarehouseAndStockItemAsync(
        string code = "WHA",
        string name = "Entrepôt A",
        Action<StockItem>? applyStock = null)
    {
        using var ctx = _contextFactory.CreateContext();

        var categoryResult = ProductCategory.Create("CAT-SNAP", "Cat snapshot");
        Assert.True(categoryResult.IsSuccess);
        ctx.ProductCategories.Add(categoryResult.Value);
        await ctx.SaveChangesAsync();

        var productResult = Product.Create(
            code: "SNP001",
            name: "SnapProd",
            type: ProductType.Product,
            unitPrice: Money.Create(10m, "TND"),
            vatRate: VatRate.Standard,
            categoryId: categoryResult.Value.Id,
            isStockManaged: true);
        Assert.True(productResult.IsSuccess);
        ctx.Products.Add(productResult.Value);
        await ctx.SaveChangesAsync();

        var whResult = Warehouse.Create(code, name);
        Assert.True(whResult.IsSuccess);
        ctx.Warehouses.Add(whResult.Value);
        await ctx.SaveChangesAsync();

        var itemResult = StockItem.Create(productResult.Value.Id, whResult.Value.Id);
        Assert.True(itemResult.IsSuccess);
        applyStock?.Invoke(itemResult.Value);

        ctx.StockItems.Add(itemResult.Value);
        await ctx.SaveChangesAsync();

        return (productResult.Value, whResult.Value, itemResult.Value);
    }

    private async Task<List<StockMovement>> LoadMovementsOrderedAsync(Guid stockItemId)
    {
        using var ctx = _contextFactory.CreateContext();
        return await ctx.StockMovements
            .Where(m => m.StockItemId == stockItemId)
            .OrderBy(m => m.OccurredAt)
            .ToListAsync();
    }

    private async Task PatchMovementOccurredAtAsync(Guid movementId, DateTime occurredAtUtc)
    {
        using var ctx = _contextFactory.CreateContext();
        var m = await ctx.StockMovements.FirstAsync(x => x.Id == movementId);
        ctx.Entry(m).Property(x => x.OccurredAt).CurrentValue = occurredAtUtc;
        await ctx.SaveChangesAsync();
    }

    public void Dispose()
    {
        using var context = _contextFactory.CreateContext();
        context.Database.EnsureDeleted();
    }
}
