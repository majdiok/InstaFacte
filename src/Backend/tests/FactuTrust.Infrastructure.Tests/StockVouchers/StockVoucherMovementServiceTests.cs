using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using FactuTrust.Infrastructure.Tests.Stock;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.StockVouchers;

public sealed class StockVoucherMovementServiceTests
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

    private sealed record SeedEntry(InMemoryTenantDbContextFactory Factory, Guid VoucherId, Guid ProductId, Guid WarehouseId);
    private sealed record SeedIssue(InMemoryTenantDbContextFactory Factory, Guid VoucherId, Guid ProductId, Guid WarehouseId);

    [Fact]
    public async Task ValidateAsync_Entry_CreatesStockAndIsIdempotent()
    {
        var seed = await SeedEntryDraftAsync(quantity: 5m, unitCost: 8m);
        var sut = new StockVoucherMovementService(seed.Factory, StockTestDoubles.Real(seed.Factory), NullLogger<StockVoucherMovementService>.Instance);

        var first = await sut.ValidateAsync(seed.VoucherId);
        Assert.True(first.IsSuccess, first.IsFailure ? first.Error.Description : "");

        var second = await sut.ValidateAsync(seed.VoucherId);
        Assert.True(second.IsSuccess);

        await using var verify = seed.Factory.CreateContext();
        var voucher = await verify.StockVouchers.FirstAsync(v => v.Id == seed.VoucherId);
        Assert.Equal(StockVoucherStatus.Validated, voucher.Status);

        var stock = await verify.StockItems.FirstAsync(s => s.ProductId == seed.ProductId && s.WarehouseId == seed.WarehouseId);
        Assert.Equal(5m, stock.QuantityOnHand);
        Assert.Equal(8m, stock.AverageCost);

        var movements = await verify.StockMovements.CountAsync(m => m.Reference == voucher.StockMovementReference);
        Assert.Equal(1, movements);
    }

    [Fact]
    public async Task ValidateAsync_Issue_InsufficientStock_Fails()
    {
        var seed = await SeedIssueDraftAsync(onHand: 1m, requested: 3m);
        var sut = new StockVoucherMovementService(seed.Factory, StockTestDoubles.Real(seed.Factory), NullLogger<StockVoucherMovementService>.Instance);

        var result = await sut.ValidateAsync(seed.VoucherId);
        Assert.True(result.IsFailure);

        await using var verify = seed.Factory.CreateContext();
        var voucher = await verify.StockVouchers.FirstAsync(v => v.Id == seed.VoucherId);
        Assert.Equal(StockVoucherStatus.Draft, voucher.Status);
    }

    [Fact]
    public async Task ValidateAsync_Issue_DecrementsStock()
    {
        var seed = await SeedIssueDraftAsync(onHand: 10m, requested: 3m);
        var sut = new StockVoucherMovementService(seed.Factory, StockTestDoubles.Real(seed.Factory), NullLogger<StockVoucherMovementService>.Instance);

        var result = await sut.ValidateAsync(seed.VoucherId);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : "");

        await using var verify = seed.Factory.CreateContext();
        var stock = await verify.StockItems.FirstAsync(s => s.ProductId == seed.ProductId && s.WarehouseId == seed.WarehouseId);
        Assert.Equal(7m, stock.QuantityOnHand);
    }

    [Fact]
    public async Task CancelAsync_ValidatedEntry_ReversesWhenStockStillAvailable()
    {
        var seed = await SeedEntryDraftAsync(quantity: 5m, unitCost: 8m);
        var sut = new StockVoucherMovementService(seed.Factory, StockTestDoubles.Real(seed.Factory), NullLogger<StockVoucherMovementService>.Instance);
        Assert.True((await sut.ValidateAsync(seed.VoucherId)).IsSuccess);

        var cancel = await sut.CancelAsync(seed.VoucherId, "Erreur de saisie");
        Assert.True(cancel.IsSuccess, cancel.IsFailure ? cancel.Error.Description : "");

        await using var verify = seed.Factory.CreateContext();
        var voucher = await verify.StockVouchers.FirstAsync(v => v.Id == seed.VoucherId);
        Assert.Equal(StockVoucherStatus.Cancelled, voucher.Status);

        var stock = await verify.StockItems.FirstAsync(s => s.ProductId == seed.ProductId && s.WarehouseId == seed.WarehouseId);
        Assert.Equal(0m, stock.QuantityOnHand);
    }

    [Fact]
    public async Task ValidateAsync_InactiveWarehouse_Fails()
    {
        var seed = await SeedEntryDraftAsync(quantity: 2m, unitCost: 5m);
        await using (var ctx = seed.Factory.CreateContext())
        {
            var warehouse = await ctx.Warehouses.FirstAsync(w => w.Id == seed.WarehouseId);
            warehouse.Deactivate();
            await ctx.SaveChangesAsync();
        }

        var sut = new StockVoucherMovementService(seed.Factory, StockTestDoubles.Real(seed.Factory), NullLogger<StockVoucherMovementService>.Instance);
        var result = await sut.ValidateAsync(seed.VoucherId);
        Assert.True(result.IsFailure);

        await using var verify = seed.Factory.CreateContext();
        var voucher = await verify.StockVouchers.FirstAsync(v => v.Id == seed.VoucherId);
        Assert.Equal(StockVoucherStatus.Draft, voucher.Status);
    }

    [Fact]
    public async Task CancelAsync_ValidatedIssue_RestoresStock()
    {
        var seed = await SeedIssueDraftAsync(onHand: 10m, requested: 4m);
        var sut = new StockVoucherMovementService(seed.Factory, StockTestDoubles.Real(seed.Factory), NullLogger<StockVoucherMovementService>.Instance);
        Assert.True((await sut.ValidateAsync(seed.VoucherId)).IsSuccess);

        var cancel = await sut.CancelAsync(seed.VoucherId, "Erreur de saisie");
        Assert.True(cancel.IsSuccess, cancel.IsFailure ? cancel.Error.Description : "");

        await using var verify = seed.Factory.CreateContext();
        var stock = await verify.StockItems.FirstAsync(s => s.ProductId == seed.ProductId && s.WarehouseId == seed.WarehouseId);
        Assert.Equal(10m, stock.QuantityOnHand);
    }

    [Fact]
    public async Task CancelAsync_ValidatedEntry_FailsWhenStockAlreadyConsumed()
    {
        var seed = await SeedEntryDraftAsync(quantity: 5m, unitCost: 8m);
        var sut = new StockVoucherMovementService(seed.Factory, StockTestDoubles.Real(seed.Factory), NullLogger<StockVoucherMovementService>.Instance);
        Assert.True((await sut.ValidateAsync(seed.VoucherId)).IsSuccess);

        await using (var ctx = seed.Factory.CreateContext())
        {
            var stock = await ctx.StockItems
                .Include(s => s.Movements)
                .FirstAsync(s => s.ProductId == seed.ProductId && s.WarehouseId == seed.WarehouseId);
            Assert.True(stock.RecordExit(5m, MovementReason.Sale, "Facture X").IsSuccess);
            await ctx.SaveChangesAsync();
        }

        var cancel = await sut.CancelAsync(seed.VoucherId, "Trop tard");
        Assert.True(cancel.IsFailure);
    }

    private static async Task<SeedEntry> SeedEntryDraftAsync(decimal quantity, decimal unitCost)
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var category = ProductCategory.Create("CAT", "Cat").Value;
        var product = Product.Create(
            "ART-1", "Article", ProductType.Product, Money.Create(10m), VatRate.Standard,
            category.Id, isStockManaged: true).Value;
        var warehouse = Warehouse.Create("WH1", "Principal").Value;
        var voucher = StockVoucher.Create(
            StockVoucherNumber.Create("BE", 2026, 1),
            StockVoucherKind.Entry,
            warehouse,
            DateTime.Today,
            MovementReason.InitialStock).Value;
        Assert.True(voucher.AddLine(product, quantity, unitCost).IsSuccess);

        await using var ctx = factory.CreateContext();
        ctx.ProductCategories.Add(category);
        ctx.Products.Add(product);
        ctx.Warehouses.Add(warehouse);
        ctx.StockVouchers.Add(voucher);
        await ctx.SaveChangesAsync();

        return new SeedEntry(factory, voucher.Id, product.Id, warehouse.Id);
    }

    private static async Task<SeedIssue> SeedIssueDraftAsync(decimal onHand, decimal requested)
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        var category = ProductCategory.Create("CAT", "Cat").Value;
        var product = Product.Create(
            "ART-2", "Article 2", ProductType.Product, Money.Create(10m), VatRate.Standard,
            category.Id, isStockManaged: true).Value;
        var warehouse = Warehouse.Create("WH1", "Principal").Value;
        var stockItem = StockItem.Create(product.Id, warehouse.Id).Value;
        Assert.True(stockItem.RecordEntry(onHand, 6m, MovementReason.InitialStock, "INIT").IsSuccess);

        var voucher = StockVoucher.Create(
            StockVoucherNumber.Create("BS", 2026, 1),
            StockVoucherKind.Issue,
            warehouse,
            DateTime.Today,
            MovementReason.Damage).Value;
        Assert.True(voucher.AddLine(product, requested, 6m).IsSuccess);

        await using var ctx = factory.CreateContext();
        ctx.ProductCategories.Add(category);
        ctx.Products.Add(product);
        ctx.Warehouses.Add(warehouse);
        ctx.StockItems.Add(stockItem);
        ctx.StockVouchers.Add(voucher);
        await ctx.SaveChangesAsync();

        return new SeedIssue(factory, voucher.Id, product.Id, warehouse.Id);
    }
}
