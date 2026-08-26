using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.Stock.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;
using FactuTrust.Domain.ValueObjects;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

public sealed class StockAllocationValidatorTests
{
    [Fact]
    public async Task Lot_ManualPicking_RequiresAllocations()
    {
        var product = CreateLotProduct(PickingPolicy.Manual);
        var validator = CreateValidator(product);

        var result = await validator.ValidateExitAllocationsAsync(product.Id, 5m, null);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Lot_ManualPicking_ValidatesSum()
    {
        var product = CreateLotProduct(PickingPolicy.Manual);
        var validator = CreateValidator(product);

        var result = await validator.ValidateExitAllocationsAsync(
            product.Id,
            5m,
            new[]
            {
                new StockAllocationInput(2m, LotNumber: "A"),
                new StockAllocationInput(3m, LotNumber: "B")
            });

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Lot_ManualPicking_RejectsQuantityOnlyAllocations()
    {
        var product = CreateLotProduct(PickingPolicy.Manual);
        var validator = CreateValidator(product);

        var result = await validator.ValidateExitAllocationsAsync(
            product.Id,
            5m,
            new[] { new StockAllocationInput(5m) });

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Lot_Fefo_AllowsQuantityOnlyAllocations()
    {
        var product = CreateLotProduct(PickingPolicy.Fefo);
        var validator = CreateValidator(product);

        var result = await validator.ValidateExitAllocationsAsync(
            product.Id,
            5m,
            new[] { new StockAllocationInput(5m) });

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Serial_EachAllocationMustBeQuantityOne()
    {
        var product = CreateSerialProduct();
        var validator = CreateValidator(product);

        var result = await validator.ValidateExitAllocationsAsync(
            product.Id,
            2m,
            new[]
            {
                new StockAllocationInput(1m, SerialNumber: "SN1"),
                new StockAllocationInput(2m, SerialNumber: "SN2")
            });

        Assert.True(result.IsFailure);
    }

    private static Product CreateLotProduct(PickingPolicy picking)
    {
        var product = Product.Create(
            "LOT-1",
            "Lot product",
            ProductType.Product,
            Money.Create(10m),
            VatRate.Standard,
            Guid.NewGuid(),
            isStockManaged: true).Value;
        product.ConfigureTraceability(TrackingMode.Lot, false, picking, CostingMethod.Fifo, null);
        return product;
    }

    private static Product CreateSerialProduct()
    {
        var product = Product.Create(
            "SER-1",
            "Serial product",
            ProductType.Product,
            Money.Create(10m),
            VatRate.Standard,
            Guid.NewGuid(),
            isStockManaged: true).Value;
        product.ConfigureTraceability(TrackingMode.Serial, false, PickingPolicy.None, CostingMethod.Fifo, null);
        return product;
    }

    private static StockAllocationValidator CreateValidator(Product product)
    {
        var products = new Mock<IProductRepository>();
        products
            .Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        return new StockAllocationValidator(products.Object);
    }
}
