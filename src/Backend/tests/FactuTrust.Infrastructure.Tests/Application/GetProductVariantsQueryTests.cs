using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Features.Products.Queries;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

public sealed class GetProductVariantsQueryTests
{
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IProductAttributeRepository> _attributes = new();
    private readonly Mock<IWarehouseRepository> _warehouses = new();
    private readonly Mock<IStockItemRepository> _stockItems = new();

    private GetProductVariantsQueryHandler CreateHandler() =>
        new(_products.Object, _attributes.Object, _warehouses.Object, _stockItems.Object);

    private static Product Parent() =>
        Product.Create("TSHIRT", "T-Shirt", ProductType.Product, Money.Create(20m), VatRate.Standard, Guid.NewGuid()).Value;

    private static Product Child(Guid parentId)
    {
        var child = Product.Create(
            "TSHIRT-M",
            "T-Shirt (M)",
            ProductType.Product,
            Money.Create(22m),
            VatRate.Standard,
            Guid.NewGuid(),
            isStockManaged: true).Value;
        child.AttachToParent(parentId);
        return child;
    }

    [Fact]
    public async Task Handle_ReturnsChildren_WithAttributes()
    {
        var parent = Parent();
        parent.MarkAsVariantTemplate();
        var child = Child(parent.Id);
        child.AttachToParent(parent.Id);

        _products.Setup(p => p.GetByIdAsync(parent.Id, It.IsAny<CancellationToken>())).ReturnsAsync(parent);
        _products.Setup(p => p.GetChildrenByParentIdAsync(parent.Id, 1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Product> { child }, 1));
        _attributes.Setup(a => a.GetVariantAttributesByProductIdsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<FactuTrust.Application.DTOs.ProductVariantAttributePairDto>>());
        _warehouses.Setup(w => w.GetDefaultAsync(It.IsAny<CancellationToken>())).ReturnsAsync((Warehouse?)null);

        var result = await CreateHandler().Handle(
            new GetProductVariantsQuery(parent.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Equal("TSHIRT-M", result.Value.Items[0].Code);
    }

    [Fact]
    public async Task Handle_ParentNotFound_Fails()
    {
        var id = Guid.NewGuid();
        _products.Setup(p => p.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Product?)null);

        var result = await CreateHandler().Handle(new GetProductVariantsQuery(id), CancellationToken.None);

        Assert.True(result.IsFailure);
    }
}

public sealed class GetProductsQueryVariantFilterTests
{
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IWarehouseRepository> _warehouses = new();
    private readonly Mock<IStockItemRepository> _stockItems = new();

    private GetProductsQueryHandler CreateHandler() =>
        new(_products.Object, _warehouses.Object, _stockItems.Object, NullLogger<GetProductsQueryHandler>.Instance);

    [Fact]
    public async Task Handle_PassesExcludeVariantTemplates_ToRepository()
    {
        _products.Setup(p => p.SearchAsync(
                It.IsAny<string?>(),
                It.IsAny<ProductType?>(),
                It.IsAny<bool?>(),
                It.IsAny<Guid?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>(),
                true,
                It.IsAny<Guid?>(),
                It.IsAny<bool?>(),
                It.IsAny<bool?>()))
            .ReturnsAsync((Array.Empty<Product>(), 0));
        _warehouses.Setup(w => w.GetDefaultAsync(It.IsAny<CancellationToken>())).ReturnsAsync((Warehouse?)null);

        var result = await CreateHandler().Handle(
            new GetProductsQuery(ExcludeVariantTemplates: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        _products.Verify(
            p => p.SearchAsync(
                It.IsAny<string?>(),
                It.IsAny<ProductType?>(),
                It.IsAny<bool?>(),
                It.IsAny<Guid?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>(),
                true,
                It.IsAny<Guid?>(),
                It.IsAny<bool?>(),
                It.IsAny<bool?>()),
            Times.Once);
    }
}
