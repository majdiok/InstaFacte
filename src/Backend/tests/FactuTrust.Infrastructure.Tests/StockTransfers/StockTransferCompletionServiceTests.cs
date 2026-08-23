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

namespace FactuTrust.Infrastructure.Tests.StockTransfers;

public sealed class StockTransferCompletionServiceTests
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
    public async Task CompleteTransferAsync_WhenTransferMissing_ReturnsNotFound()
    {
        var dbName = Guid.NewGuid().ToString();
        var factory = new InMemoryTenantDbContextFactory(dbName);
        var sut = new StockTransferCompletionService(factory, StockTestDoubles.Real(factory), NullLogger<StockTransferCompletionService>.Instance);

        var missingId = Guid.NewGuid();
        var result = await sut.CompleteTransferAsync(missingId);

        Assert.False(result.IsSuccess);
        Assert.Equal("StockTransfer.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task CompleteTransferAsync_WhenConfirmedAndStockAvailable_CompletesTransfer()
    {
        var dbName = Guid.NewGuid().ToString();
        var factory = new InMemoryTenantDbContextFactory(dbName);

        var category = ProductCategory.Create("TSTCAT", "Test cat").Value;
        var product = Product.Create(
            "TST-P1",
            "Produit test",
            ProductType.Product,
            Money.Create(10m),
            VatRate.Standard,
            category.Id,
            isStockManaged: true).Value;

        var sourceWh = Warehouse.Create("SRC", "Source WH").Value;
        var destWh = Warehouse.Create("DST", "Dest WH").Value;

        var number = StockTransferNumber.Create("TR", 2026, 1);
        var transfer = StockTransfer.Create(
            number,
            sourceWh.Id,
            destWh.Id,
            DateTime.UtcNow.Date).Value;

        var addLine = transfer.AddLine(product, 4m);
        Assert.True(addLine.IsSuccess);

        var confirm = transfer.Confirm();
        Assert.True(confirm.IsSuccess);

        var stockItem = StockItem.Create(product.Id, sourceWh.Id).Value;
        var entry = stockItem.RecordEntry(20m, 5m, MovementReason.Purchase, "INIT", "Seed");
        Assert.True(entry.IsSuccess);

        await using (var ctx = factory.CreateContext())
        {
            ctx.ProductCategories.Add(category);
            ctx.Products.Add(product);
            ctx.Warehouses.AddRange(sourceWh, destWh);
            ctx.StockItems.Add(stockItem);
            ctx.StockTransfers.Add(transfer);
            await ctx.SaveChangesAsync();
        }

        await using (var probe = factory.CreateContext())
        {
            var persisted = await probe.StockTransfers.AsNoTracking().FirstAsync(t => t.Id == transfer.Id);
            Assert.Equal(StockTransferStatus.Confirmed, persisted.Status);
        }

        var sut = new StockTransferCompletionService(factory, StockTestDoubles.Real(factory), NullLogger<StockTransferCompletionService>.Instance);
        var result = await sut.CompleteTransferAsync(transfer.Id);

        Assert.True(result.IsSuccess, result.IsFailure ? $"{result.Error.Code}: {result.Error.Description}" : "");

        await using (var verify = factory.CreateContext())
        {
            var t = await verify.StockTransfers.Include(x => x.Lines).FirstAsync(x => x.Id == transfer.Id);
            Assert.Equal(StockTransferStatus.Completed, t.Status);

            var source = await verify.StockItems.FirstAsync(s =>
                s.ProductId == product.Id && s.WarehouseId == sourceWh.Id);
            Assert.Equal(16m, source.QuantityOnHand);

            var dest = await verify.StockItems.FirstAsync(s =>
                s.ProductId == product.Id && s.WarehouseId == destWh.Id);
            Assert.Equal(4m, dest.QuantityOnHand);
        }
    }
}
