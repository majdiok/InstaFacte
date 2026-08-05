using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Features.Products.Queries;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

public sealed class SearchProductsForSelectQueryTests
{
    private readonly Mock<IProductRepository> _products = new();

    private SearchProductsForSelectQueryHandler CreateHandler() =>
        new(_products.Object, NullLogger<SearchProductsForSelectQueryHandler>.Instance);

    private static Product NewProduct(string code, string name, bool isFodec = false) =>
        Product.Create(
            code: code,
            name: name,
            type: ProductType.Product,
            unitPrice: Money.Create(100m),
            vatRate: VatRate.Standard,
            categoryId: Guid.NewGuid(),
            unit: "Unité",
            isFodecApplicable: isFodec).Value;

    [Fact]
    public async Task Handle_DefaultsIsActiveTrue_AndCapsPageSize()
    {
        var product = NewProduct("P-001", "Produit A");
        _products
            .Setup(r => r.SearchForSelectAsync(
                null,
                true,
                SearchProductsForSelectQueryHandler.MaxPageSize,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { product });

        var result = await CreateHandler().Handle(
            new SearchProductsForSelectQuery(Search: null, IsActive: true, Page: 1, PageSize: 500),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SearchProductsForSelectQueryHandler.MaxPageSize, result.Value.PageSize);
        Assert.Single(result.Value.Items);
        Assert.Equal("P-001", result.Value.Items[0].Code);
        Assert.Equal("Produit A", result.Value.Items[0].Name);
        Assert.Equal(100m, result.Value.Items[0].UnitPrice);
        Assert.Equal(19, result.Value.Items[0].VatRatePercent);

        _products.Verify(
            r => r.SearchForSelectAsync(null, true, SearchProductsForSelectQueryHandler.MaxPageSize, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_PassesSearchTerm_ToRepository()
    {
        _products
            .Setup(r => r.SearchForSelectAsync("tab", true, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Product>());

        var result = await CreateHandler().Handle(
            new SearchProductsForSelectQuery(Search: "tab", IsActive: true, Page: 1, PageSize: 50),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Items);
        _products.Verify(
            r => r.SearchForSelectAsync("tab", true, 50, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_DoesNotRequireWarehouseOrStockRepositories()
    {
        _products
            .Setup(r => r.SearchForSelectAsync(It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Product>());

        var result = await CreateHandler().Handle(
            new SearchProductsForSelectQuery(),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        _products.Verify(
            r => r.SearchForSelectAsync(It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _products.Verify(
            r => r.SearchAsync(
                It.IsAny<string?>(),
                It.IsAny<ProductType?>(),
                It.IsAny<bool?>(),
                It.IsAny<Guid?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}

public sealed class GetProductFodecFlagsQueryTests
{
    private readonly Mock<IProductRepository> _products = new();

    private GetProductFodecFlagsQueryHandler CreateHandler() => new(_products.Object);

    [Fact]
    public async Task Handle_EmptyIds_ReturnsEmpty_WithoutRepositoryCall()
    {
        var result = await CreateHandler().Handle(
            new GetProductFodecFlagsQuery(Array.Empty<Guid>()),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
        _products.Verify(
            r => r.GetFodecFlagsByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ReturnsFlags_FromRepository()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        _products
            .Setup(r => r.GetFodecFlagsByIdsAsync(
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(id1) && ids.Contains(id2)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, bool>
            {
                [id1] = true,
                [id2] = false
            });

        var result = await CreateHandler().Handle(
            new GetProductFodecFlagsQuery(new[] { id1, id2 }),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.Contains(result.Value, f => f.Id == id1 && f.IsFodecApplicable);
        Assert.Contains(result.Value, f => f.Id == id2 && !f.IsFodecApplicable);
    }

    [Fact]
    public async Task Handle_RejectsOversizedBatch()
    {
        var ids = Enumerable.Range(0, 201).Select(_ => Guid.NewGuid()).ToList();

        var result = await CreateHandler().Handle(
            new GetProductFodecFlagsQuery(ids),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        _products.Verify(
            r => r.GetFodecFlagsByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
