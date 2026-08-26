using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Stock;

public sealed class EfStockTraceabilityQueryTests
{
    [Fact]
    public async Task ListLotsAsync_OrdersByExpiryThenLotNumber_AndMapsQuantityAvailable()
    {
        var factory = new InMemoryTenantDbContextFactory(Guid.NewGuid().ToString());
        Guid stockItemId;

        await using (var ctx = factory.CreateContext())
        {
            var category = ProductCategory.Create("CAT", "Cat").Value;
            ctx.ProductCategories.Add(category);

            var product = Product.Create(
                "SKU-LOT",
                "Article lot",
                ProductType.Product,
                Money.Create(10m, Money.DefaultCurrency),
                VatRate.Standard,
                category.Id,
                isStockManaged: true).Value;
            ctx.Products.Add(product);

            var warehouse = Warehouse.Create("WH1", "Principal", isDefault: true).Value;
            ctx.Warehouses.Add(warehouse);

            var stockItem = StockItem.Create(product.Id, warehouse.Id).Value;
            ctx.StockItems.Add(stockItem);
            stockItemId = stockItem.Id;

            var lotWithExpiry = ProductLot.Create(
                product.Id,
                "LOT-B",
                expiryDate: new DateTime(2026, 6, 1)).Value;
            var lotWithoutExpiry = ProductLot.Create(
                product.Id,
                "LOT-A").Value;
            ctx.ProductLots.AddRange(lotWithExpiry, lotWithoutExpiry);

            var balanceWithExpiry = StockLotBalance.Create(stockItem.Id, lotWithExpiry.Id).Value;
            Assert.True(balanceWithExpiry.Increase(10m).IsSuccess);
            var balanceWithoutExpiry = StockLotBalance.Create(stockItem.Id, lotWithoutExpiry.Id).Value;
            Assert.True(balanceWithoutExpiry.Increase(5m).IsSuccess);
            ctx.StockLotBalances.AddRange(balanceWithExpiry, balanceWithoutExpiry);

            await ctx.SaveChangesAsync();
        }

        var sut = new EfStockTraceabilityQuery(factory);
        var lots = await sut.ListLotsAsync(stockItemId);

        Assert.Equal(2, lots.Count);
        Assert.Equal("LOT-B", lots[0].LotNumber);
        Assert.Equal(new DateTime(2026, 6, 1), lots[0].ExpiryDate);
        Assert.Equal(10m, lots[0].QuantityOnHand);
        Assert.Equal(0m, lots[0].QuantityReserved);
        Assert.Equal(10m, lots[0].QuantityAvailable);

        Assert.Equal("LOT-A", lots[1].LotNumber);
        Assert.Null(lots[1].ExpiryDate);
        Assert.Equal(5m, lots[1].QuantityOnHand);
        Assert.Equal(5m, lots[1].QuantityAvailable);
    }
}
