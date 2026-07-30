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
/// Paliers quantitatifs (tranche 5B). Le point sensible : un article SANS palier doit se
/// résoudre exactement comme avant, quelle que soit la quantité.
/// </summary>
public sealed class PriceListTierTests
{
    private static readonly Guid ClientId = Guid.NewGuid();
    private static readonly DateTime Date = new(2026, 07, 20);

    // ─────────────────────────── Domaine ───────────────────────────

    [Fact]
    public void WithoutTiers_QuantityHasNoEffect()
    {
        var productId = Guid.NewGuid();
        var priceList = PriceList.Create("Grille simple").Value;
        priceList.SetPrice(productId, Money.Create(100m));

        Assert.Equal(100m, priceList.TryGetUnitPrice(productId, 1m)!.Amount);
        Assert.Equal(100m, priceList.TryGetUnitPrice(productId, 500m)!.Amount);
    }

    [Fact]
    public void HighestReachedTier_Wins()
    {
        var productId = Guid.NewGuid();
        var priceList = PriceList.Create("Grille dégressive").Value;
        priceList.SetPrice(productId, Money.Create(100m));
        Assert.True(priceList.SetTier(productId, 10m, Money.Create(90m)).IsSuccess);
        Assert.True(priceList.SetTier(productId, 50m, Money.Create(80m)).IsSuccess);

        Assert.Equal(100m, priceList.TryGetUnitPrice(productId, 9m)!.Amount);
        Assert.Equal(90m, priceList.TryGetUnitPrice(productId, 10m)!.Amount);
        Assert.Equal(90m, priceList.TryGetUnitPrice(productId, 49m)!.Amount);
        Assert.Equal(80m, priceList.TryGetUnitPrice(productId, 50m)!.Amount);
        Assert.Equal(80m, priceList.TryGetUnitPrice(productId, 999m)!.Amount);
    }

    [Fact]
    public void TiersDeclaredOutOfOrder_StillResolveByThreshold()
    {
        var productId = Guid.NewGuid();
        var priceList = PriceList.Create("Grille dégressive").Value;
        priceList.SetPrice(productId, Money.Create(100m));

        // Saisis à l'envers : c'est le seuil qui décide, pas l'ordre d'ajout.
        priceList.SetTier(productId, 50m, Money.Create(80m));
        priceList.SetTier(productId, 10m, Money.Create(90m));

        Assert.Equal(90m, priceList.TryGetUnitPrice(productId, 12m)!.Amount);
        Assert.Equal(80m, priceList.TryGetUnitPrice(productId, 60m)!.Amount);
    }

    [Fact]
    public void SetTier_IsUpsert_NotDuplicate()
    {
        var productId = Guid.NewGuid();
        var priceList = PriceList.Create("Grille").Value;
        priceList.SetPrice(productId, Money.Create(100m));

        priceList.SetTier(productId, 10m, Money.Create(90m));
        priceList.SetTier(productId, 10m, Money.Create(85m));

        var item = Assert.Single(priceList.Items);
        Assert.Single(item.Tiers);
        Assert.Equal(85m, priceList.TryGetUnitPrice(productId, 10m)!.Amount);
    }

    [Fact]
    public void RemoveTier_FallsBackToTheTierBelow()
    {
        var productId = Guid.NewGuid();
        var priceList = PriceList.Create("Grille").Value;
        priceList.SetPrice(productId, Money.Create(100m));
        priceList.SetTier(productId, 10m, Money.Create(90m));
        priceList.SetTier(productId, 50m, Money.Create(80m));

        priceList.RemoveTier(productId, 50m);

        Assert.Equal(90m, priceList.TryGetUnitPrice(productId, 60m)!.Amount);
    }

    [Fact]
    public void SetTier_IsRefused_AtOrBelowQuantityOne()
    {
        var productId = Guid.NewGuid();
        var priceList = PriceList.Create("Grille").Value;
        priceList.SetPrice(productId, Money.Create(100m));

        // En deçà de 2, c'est le prix de base qui joue : un « palier 1 » serait un doublon
        // silencieux du prix de base.
        Assert.True(priceList.SetTier(productId, 1m, Money.Create(90m)).IsFailure);
        Assert.True(priceList.SetTier(productId, 0m, Money.Create(90m)).IsFailure);
    }

    [Fact]
    public void SetTier_IsRefused_WhenProductHasNoBasePrice()
    {
        var priceList = PriceList.Create("Grille").Value;

        var result = priceList.SetTier(Guid.NewGuid(), 10m, Money.Create(90m));

        Assert.True(result.IsFailure);
    }

    // ─────────────────────────── Résolveur ───────────────────────────

    [Fact]
    public async Task Resolver_PicksTheTierMatchingTheQuantity()
    {
        var product = NewProduct(catalogHt: 100m);
        var priceList = PriceList.Create("Grille dégressive").Value;
        priceList.SetPrice(product.Id, Money.Create(95m));
        priceList.SetTier(product.Id, 10m, Money.Create(90m));
        priceList.SetTier(product.Id, 50m, Money.Create(80m));

        var resolver = BuildResolver(product, priceList);

        var small = await resolver.ResolveUnitPriceAsync(ClientId, product.Id, 5m, Date);
        var medium = await resolver.ResolveUnitPriceAsync(ClientId, product.Id, 25m, Date);
        var large = await resolver.ResolveUnitPriceAsync(ClientId, product.Id, 100m, Date);

        Assert.Equal(95m, small.Value.UnitPriceHT.Amount);
        Assert.Equal(90m, medium.Value.UnitPriceHT.Amount);
        Assert.Equal(80m, large.Value.UnitPriceHT.Amount);
        Assert.All(new[] { small, medium, large },
            r => Assert.Equal(PriceSource.PriceList, r.Value.Source));
    }

    /// <summary>
    /// Non-régression de la tranche 5A : sans palier, la quantité ne change rien au prix résolu.
    /// </summary>
    [Fact]
    public async Task Resolver_WithoutTiers_IsUnaffectedByQuantity()
    {
        var product = NewProduct(catalogHt: 100m);
        var priceList = PriceList.Create("Grille simple").Value;
        priceList.SetPrice(product.Id, Money.Create(85m));

        var resolver = BuildResolver(product, priceList);

        var one = await resolver.ResolveUnitPriceAsync(ClientId, product.Id, 1m, Date);
        var many = await resolver.ResolveUnitPriceAsync(ClientId, product.Id, 1000m, Date);

        Assert.Equal(85m, one.Value.UnitPriceHT.Amount);
        Assert.Equal(85m, many.Value.UnitPriceHT.Amount);
    }

    /// <summary>
    /// Le prix négocié client prime sur les paliers : un accord particulier ne se fait pas
    /// écraser par un dégressif de grille, même à grande quantité.
    /// </summary>
    [Fact]
    public async Task NegotiatedPrice_StillWinsOverTiers()
    {
        var product = NewProduct(catalogHt: 100m);
        var priceList = PriceList.Create("Grille dégressive").Value;
        priceList.SetPrice(product.Id, Money.Create(95m));
        priceList.SetTier(product.Id, 10m, Money.Create(70m));

        var resolver = BuildResolver(product, priceList, negotiated: Money.Create(88m));

        var result = await resolver.ResolveUnitPriceAsync(ClientId, product.Id, 500m, Date);

        Assert.Equal(PriceSource.ClientPrice, result.Value.Source);
        Assert.Equal(88m, result.Value.UnitPriceHT.Amount);
    }

    // ─────────────────────────── Montage ───────────────────────────

    private static PriceResolver BuildResolver(Product product, PriceList priceList, Money? negotiated = null)
    {
        var products = new Mock<IProductRepository>();
        var clients = new Mock<IClientRepository>();
        var priceLists = new Mock<IPriceListRepository>();
        var clientPrices = new Mock<IClientProductPriceRepository>();

        products.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        clientPrices.Setup(r => r.GetForClientProductAsync(ClientId, product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(negotiated is null
                ? null
                : ClientProductPrice.Create(ClientId, product.Id, negotiated).Value);

        var client = NewClient();
        client.AssignPriceList(priceList.Id);
        clients.Setup(r => r.GetByIdAsync(ClientId, It.IsAny<CancellationToken>())).ReturnsAsync(client);
        priceLists.Setup(r => r.GetByIdWithItemsAsync(priceList.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(priceList);

        return new PriceResolver(products.Object, clients.Object, priceLists.Object, clientPrices.Object);
    }

    private static Product NewProduct(decimal catalogHt) =>
        Product.Create(
            code: "P-PALIER",
            name: "Produit à paliers",
            type: ProductType.Product,
            unitPrice: Money.Create(catalogHt),
            vatRate: VatRate.Standard,
            categoryId: Guid.NewGuid(),
            unit: "Unité").Value;

    private static Client NewClient() =>
        Client.Create(
            "Client test", ClientType.Business,
            Address.Create("Rue 1", "Tunis", "Tunis").Value,
            Email.Create("client@test.tn").Value,
            NIF.Create("1234567/A/B/C/000").Value).Value;
}
