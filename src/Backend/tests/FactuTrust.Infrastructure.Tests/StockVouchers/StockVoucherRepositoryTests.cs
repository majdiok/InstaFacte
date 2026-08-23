using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.StockVouchers;

public sealed class StockVoucherRepositoryTests
{
    private sealed class InMemoryTenantDbContextFactory : ITenantDbContextFactory
    {
        private readonly DbContextOptions<TenantDbContext> _options;

        public InMemoryTenantDbContextFactory(string databaseName)
        {
            _options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseInMemoryDatabase(databaseName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        public TenantDbContext CreateContext() => new TenantDbContext(_options);
    }

    [Fact]
    public async Task AddAsync_WarehouseLoadedFromAnotherContext_DoesNotInsertDuplicateWarehouse()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var category = ProductCategory.Create("CAT", "Cat").Value;
        var product = Product.Create(
            "ART-1", "Article", ProductType.Product, Money.Create(10m), VatRate.Standard,
            category.Id, isStockManaged: true).Value;
        var warehouse = Warehouse.Create("WH1", "Principal").Value;
        var warehouseId = warehouse.Id;

        await using (var seed = factory.CreateContext())
        {
            seed.ProductCategories.Add(category);
            seed.Products.Add(product);
            seed.Warehouses.Add(warehouse);
            await seed.SaveChangesAsync();
        }

        Warehouse reloaded;
        await using (var load = factory.CreateContext())
        {
            reloaded = await load.Warehouses.FirstAsync(w => w.Id == warehouseId);
        }

        Product reloadedProduct;
        await using (var load = factory.CreateContext())
        {
            reloadedProduct = await load.Products.FirstAsync(p => p.Id == product.Id);
        }

        var voucher = StockVoucher.Create(
            StockVoucherNumber.Create("BE", 2026, 1),
            StockVoucherKind.Entry,
            reloaded,
            DateTime.Today,
            MovementReason.InitialStock).Value;
        Assert.True(voucher.AddLine(reloadedProduct, 4m, 10m).IsSuccess);

        var sut = new StockVoucherRepository(factory);
        await sut.AddAsync(voucher);

        await using var verify = factory.CreateContext();
        Assert.Equal(1, await verify.Warehouses.CountAsync());
        var saved = await verify.StockVouchers.Include(v => v.Lines).FirstAsync(v => v.Id == voucher.Id);
        Assert.Equal(warehouseId, saved.WarehouseId);
        Assert.Single(saved.Lines);
        Assert.Equal(4m, saved.Lines.First().Quantity);
    }

    [Fact]
    public async Task AddAsync_WithDisconnectedWarehouseNavigation_DoesNotInsertDuplicateWarehouse()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var warehouse = Warehouse.Create("WH1", "Principal").Value;
        var warehouseId = warehouse.Id;

        await using (var seed = factory.CreateContext())
        {
            seed.Warehouses.Add(warehouse);
            await seed.SaveChangesAsync();
        }

        Warehouse reloaded;
        await using (var load = factory.CreateContext())
        {
            reloaded = await load.Warehouses.FirstAsync(w => w.Id == warehouseId);
        }

        var voucher = StockVoucher.Create(
            StockVoucherNumber.Create("BE", 2026, 2),
            StockVoucherKind.Entry,
            reloaded,
            DateTime.Today,
            MovementReason.InitialStock).Value;

        typeof(StockVoucher).GetProperty(nameof(StockVoucher.Warehouse))!
            .SetValue(voucher, reloaded);

        var sut = new StockVoucherRepository(factory);
        await sut.AddAsync(voucher);

        await using var verify = factory.CreateContext();
        Assert.Equal(1, await verify.Warehouses.CountAsync());
        Assert.True(await verify.StockVouchers.AnyAsync(v => v.Id == voucher.Id));
    }
}
