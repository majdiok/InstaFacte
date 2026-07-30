using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.Pricing;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.Services.Pricing;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Vérifie la priorité du point de résolution unique : prix négocié client → grille affectée
/// au client → prix catalogue. C'est l'invariant de tarification du lot 5 : chaque document
/// doit obtenir le MÊME prix pour un même contexte, et le plus spécifique doit l'emporter.
/// </summary>
public sealed class PriceResolverTests
{
    private static readonly Guid ClientId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();
    private static readonly DateTime Date = new(2026, 07, 20);

    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IClientRepository> _clients = new();
    private readonly Mock<IPriceListRepository> _priceLists = new();
    private readonly Mock<IClientProductPriceRepository> _clientPrices = new();

    private PriceResolver CreateResolver() =>
        new(_products.Object, _clients.Object, _priceLists.Object, _clientPrices.Object);

    private static Product CatalogProduct(decimal catalogPrice)
    {
        var result = Product.Create(
            "P-001", "Article test", ProductType.Product,
            Money.Create(catalogPrice), VatRate.Standard, Guid.NewGuid());
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private void SetupProduct(decimal catalogPrice) =>
        _products.Setup(r => r.GetByIdAsync(ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CatalogProduct(catalogPrice));

    [Fact]
    public async Task NoClient_FallsBackToCatalog()
    {
        SetupProduct(100m);
        var resolver = CreateResolver();

        var result = await resolver.ResolveUnitPriceAsync(null, ProductId, 1m, Date);

        Assert.True(result.IsSuccess);
        Assert.Equal(PriceSource.Catalog, result.Value.Source);
        Assert.Equal(100m, result.Value.UnitPriceHT.Amount);
    }

    [Fact]
    public async Task UnknownProduct_Fails()
    {
        _products.Setup(r => r.GetByIdAsync(ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);
        var resolver = CreateResolver();

        var result = await resolver.ResolveUnitPriceAsync(ClientId, ProductId, 1m, Date);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task ClientWithoutAnyPricing_FallsBackToCatalog()
    {
        SetupProduct(100m);
        _clientPrices.Setup(r => r.GetForClientProductAsync(ClientId, ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientProductPrice?)null);
        _clients.Setup(r => r.GetByIdAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildClient(priceListId: null));
        var resolver = CreateResolver();

        var result = await resolver.ResolveUnitPriceAsync(ClientId, ProductId, 1m, Date);

        Assert.True(result.IsSuccess);
        Assert.Equal(PriceSource.Catalog, result.Value.Source);
        Assert.Equal(100m, result.Value.UnitPriceHT.Amount);
    }

    [Fact]
    public async Task PriceList_TakesPrecedenceOverCatalog()
    {
        SetupProduct(100m);
        _clientPrices.Setup(r => r.GetForClientProductAsync(ClientId, ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientProductPrice?)null);

        var priceList = PriceList.Create("Grille grossiste").Value;
        priceList.SetPrice(ProductId, Money.Create(80m));
        _clients.Setup(r => r.GetByIdAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildClient(priceListId: priceList.Id));
        _priceLists.Setup(r => r.GetByIdWithItemsAsync(priceList.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(priceList);
        var resolver = CreateResolver();

        var result = await resolver.ResolveUnitPriceAsync(ClientId, ProductId, 1m, Date);

        Assert.True(result.IsSuccess);
        Assert.Equal(PriceSource.PriceList, result.Value.Source);
        Assert.Equal(80m, result.Value.UnitPriceHT.Amount);
    }

    [Fact]
    public async Task NegotiatedClientPrice_TakesPrecedenceOverPriceList()
    {
        SetupProduct(100m);

        var negotiated = ClientProductPrice.Create(ClientId, ProductId, Money.Create(65m)).Value;
        _clientPrices.Setup(r => r.GetForClientProductAsync(ClientId, ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(negotiated);

        // Une grille existe aussi, mais ne doit jamais être consultée : le prix négocié prime.
        var priceList = PriceList.Create("Grille grossiste").Value;
        priceList.SetPrice(ProductId, Money.Create(80m));
        _clients.Setup(r => r.GetByIdAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildClient(priceListId: priceList.Id));
        _priceLists.Setup(r => r.GetByIdWithItemsAsync(priceList.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(priceList);
        var resolver = CreateResolver();

        var result = await resolver.ResolveUnitPriceAsync(ClientId, ProductId, 1m, Date);

        Assert.True(result.IsSuccess);
        Assert.Equal(PriceSource.ClientPrice, result.Value.Source);
        Assert.Equal(65m, result.Value.UnitPriceHT.Amount);
    }

    [Fact]
    public async Task ExpiredPriceList_IsIgnored_FallsBackToCatalog()
    {
        SetupProduct(100m);
        _clientPrices.Setup(r => r.GetForClientProductAsync(ClientId, ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientProductPrice?)null);

        // Grille dont la validité s'est terminée avant la date du document.
        var priceList = PriceList.Create("Promo close", validFrom: new DateTime(2026, 01, 01),
            validUntil: new DateTime(2026, 06, 30)).Value;
        priceList.SetPrice(ProductId, Money.Create(80m));
        _clients.Setup(r => r.GetByIdAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildClient(priceListId: priceList.Id));
        _priceLists.Setup(r => r.GetByIdWithItemsAsync(priceList.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(priceList);
        var resolver = CreateResolver();

        var result = await resolver.ResolveUnitPriceAsync(ClientId, ProductId, 1m, Date);

        Assert.True(result.IsSuccess);
        Assert.Equal(PriceSource.Catalog, result.Value.Source);
        Assert.Equal(100m, result.Value.UnitPriceHT.Amount);
    }

    [Fact]
    public async Task InactiveNegotiatedPrice_IsIgnored_UsesPriceList()
    {
        SetupProduct(100m);

        var negotiated = ClientProductPrice.Create(ClientId, ProductId, Money.Create(65m)).Value;
        negotiated.Deactivate();
        _clientPrices.Setup(r => r.GetForClientProductAsync(ClientId, ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(negotiated);

        var priceList = PriceList.Create("Grille grossiste").Value;
        priceList.SetPrice(ProductId, Money.Create(80m));
        _clients.Setup(r => r.GetByIdAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildClient(priceListId: priceList.Id));
        _priceLists.Setup(r => r.GetByIdWithItemsAsync(priceList.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(priceList);
        var resolver = CreateResolver();

        var result = await resolver.ResolveUnitPriceAsync(ClientId, ProductId, 1m, Date);

        Assert.True(result.IsSuccess);
        Assert.Equal(PriceSource.PriceList, result.Value.Source);
        Assert.Equal(80m, result.Value.UnitPriceHT.Amount);
    }

    [Fact]
    public async Task PriceListWithoutTheProduct_FallsBackToCatalog()
    {
        SetupProduct(100m);
        _clientPrices.Setup(r => r.GetForClientProductAsync(ClientId, ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientProductPrice?)null);

        // Grille applicable mais qui ne porte pas ce produit précis.
        var priceList = PriceList.Create("Grille partielle").Value;
        priceList.SetPrice(Guid.NewGuid(), Money.Create(80m));
        _clients.Setup(r => r.GetByIdAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildClient(priceListId: priceList.Id));
        _priceLists.Setup(r => r.GetByIdWithItemsAsync(priceList.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(priceList);
        var resolver = CreateResolver();

        var result = await resolver.ResolveUnitPriceAsync(ClientId, ProductId, 1m, Date);

        Assert.True(result.IsSuccess);
        Assert.Equal(PriceSource.Catalog, result.Value.Source);
        Assert.Equal(100m, result.Value.UnitPriceHT.Amount);
    }

    private static Client BuildClient(Guid? priceListId)
    {
        var client = Client.Create(
            "Client test", ClientType.Business,
            Address.Create("Rue 1", "Tunis", "Tunis").Value,
            Email.Create("client@test.tn").Value,
            NIF.Create("1234567/A/B/C/000").Value).Value;

        if (priceListId.HasValue)
            client.AssignPriceList(priceListId.Value);

        return client;
    }
}
