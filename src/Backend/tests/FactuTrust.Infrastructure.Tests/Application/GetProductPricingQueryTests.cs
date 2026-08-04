using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Features.Pricing.Queries;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.Pricing;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

public sealed class GetProductPricingQueryTests
{
    private readonly Mock<IClientProductPriceRepository> _prices = new();
    private readonly Mock<IClientRepository> _clients = new();
    private readonly Mock<IProductRepository> _products = new();

    private GetProductPricingQueryHandler CreateHandler() =>
        new(_prices.Object, _clients.Object, _products.Object);

    [Fact]
    public async Task Handle_ReturnsNotFound_WhenProductDoesNotExist()
    {
        var productId = Guid.NewGuid();
        _products.Setup(r => r.GetByIdAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        var result = await CreateHandler().Handle(
            new GetProductPricingQuery(productId), CancellationToken.None);

        Assert.True(result.IsFailure);
        _prices.Verify(
            r => r.GetByProductAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReturnsEmptyClientPrices_WhenNoNegotiatedPricesExist()
    {
        var product = NewProduct();
        _products.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        _prices.Setup(r => r.GetByProductAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ClientProductPrice>());

        var result = await CreateHandler().Handle(
            new GetProductPricingQuery(product.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(product.Id, result.Value.ProductId);
        Assert.Equal(product.Code, result.Value.ProductCode);
        Assert.Equal(100m, result.Value.CatalogUnitPriceHT);
        Assert.Empty(result.Value.ClientPrices);
    }

    [Fact]
    public async Task Handle_ReturnsClientPrices_OrderedByClientName()
    {
        var product = NewProduct();
        var clientA = NewClient("Alpha Client");
        var clientB = NewClient("Beta Client");

        var priceA = ClientProductPrice.Create(clientA.Id, product.Id, Money.Create(80m)).Value;
        var priceB = ClientProductPrice.Create(clientB.Id, product.Id, Money.Create(90m)).Value;

        _products.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        _prices.Setup(r => r.GetByProductAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { priceB, priceA });
        _clients.Setup(r => r.GetByIdAsync(clientA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(clientA);
        _clients.Setup(r => r.GetByIdAsync(clientB.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(clientB);

        var result = await CreateHandler().Handle(
            new GetProductPricingQuery(product.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.ClientPrices.Count);
        Assert.Equal("Alpha Client", result.Value.ClientPrices[0].ClientName);
        Assert.Equal("Beta Client", result.Value.ClientPrices[1].ClientName);
        Assert.Equal(80m, result.Value.ClientPrices[0].UnitPriceHT);
        Assert.Equal(100m, result.Value.ClientPrices[0].CatalogUnitPriceHT);
    }

    [Fact]
    public async Task Handle_MarksInactivePrice_AsNotApplicableToday()
    {
        var product = NewProduct();
        var client = NewClient("Client inactif");
        var price = ClientProductPrice.Create(client.Id, product.Id, Money.Create(75m)).Value;
        price.Deactivate();

        _products.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        _prices.Setup(r => r.GetByProductAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { price });
        _clients.Setup(r => r.GetByIdAsync(client.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(client);

        var result = await CreateHandler().Handle(
            new GetProductPricingQuery(product.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var line = Assert.Single(result.Value.ClientPrices);
        Assert.False(line.IsActive);
        Assert.False(line.IsApplicableToday);
    }

    private static Product NewProduct() =>
        Product.Create(
            code: "P-001",
            name: "Produit test",
            type: ProductType.Product,
            unitPrice: Money.Create(100m),
            vatRate: VatRate.Standard,
            categoryId: Guid.NewGuid(),
            unit: "Unité").Value;

    private static Client NewClient(string name) =>
        Client.Create(
            name, ClientType.Business,
            Address.Create("Rue 1", "Tunis", "Tunis").Value,
            Email.Create("client@test.tn").Value,
            NIF.Create("1234567/A/B/C/000").Value).Value;
}
