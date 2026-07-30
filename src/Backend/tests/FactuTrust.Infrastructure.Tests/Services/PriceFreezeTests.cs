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
/// Invariant central du lot 5 : le prix est résolu UNE FOIS, à la création de la ligne, puis
/// figé. Un document déjà émis ne doit jamais changer de prix parce qu'une grille a bougé.
///
/// Ces tests rejouent la séquence réelle des handlers (résoudre, puis passer le prix résolu en
/// prix de ligne), puis font bouger la grille pour vérifier que le document ne suit pas.
/// </summary>
public sealed class PriceFreezeTests
{
    private static readonly Guid ClientId = Guid.NewGuid();
    private static readonly DateTime Date = new(2026, 07, 20);

    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IClientRepository> _clients = new();
    private readonly Mock<IPriceListRepository> _priceLists = new();
    private readonly Mock<IClientProductPriceRepository> _clientPrices = new();

    [Fact]
    public async Task Quote_KeepsItsResolvedPrice_WhenPriceListChangesAfterwards()
    {
        var product = NewProduct(catalogHt: 100m);
        var priceList = PriceList.Create("Grille grossiste").Value;
        priceList.SetPrice(product.Id, Money.Create(80m));

        var resolver = SetupResolver(product, priceList);

        // 1. Le handler résout le prix, puis le grave sur la ligne.
        var resolved = await resolver.ResolveUnitPriceAsync(ClientId, product.Id, 10m, Date);
        Assert.Equal(PriceSource.PriceList, resolved.Value.Source);
        Assert.Equal(80m, resolved.Value.UnitPriceHT.Amount);

        var quote = NewQuote();
        Assert.True(quote.AddLine(product, 10m, resolved.Value.UnitPriceHT, discountPercent: null).IsSuccess);
        Assert.Equal(800m, quote.SubTotal.Amount);

        // 2. La grille change APRÈS l'émission du devis.
        priceList.SetPrice(product.Id, Money.Create(50m));

        // 3. Le devis ne bouge pas : ni la ligne, ni le total.
        var line = Assert.Single(quote.Lines);
        Assert.Equal(80m, line.UnitPrice.Amount);
        Assert.Equal(800m, quote.SubTotal.Amount);

        // 4. Mais une NOUVELLE ligne, elle, prend bien le nouveau prix.
        var reresolved = await resolver.ResolveUnitPriceAsync(ClientId, product.Id, 10m, Date);
        Assert.Equal(50m, reresolved.Value.UnitPriceHT.Amount);
    }

    [Fact]
    public async Task Quote_KeepsItsResolvedPrice_WhenPriceListIsDeactivatedAfterwards()
    {
        var product = NewProduct(catalogHt: 100m);
        var priceList = PriceList.Create("Grille saisonnière").Value;
        priceList.SetPrice(product.Id, Money.Create(80m));

        var resolver = SetupResolver(product, priceList);

        var resolved = await resolver.ResolveUnitPriceAsync(ClientId, product.Id, 5m, Date);
        var quote = NewQuote();
        Assert.True(quote.AddLine(product, 5m, resolved.Value.UnitPriceHT, discountPercent: null).IsSuccess);

        // La grille est désactivée : un document émis ne doit pas retomber au catalogue.
        priceList.Deactivate();

        Assert.Equal(80m, Assert.Single(quote.Lines).UnitPrice.Amount);
        Assert.Equal(400m, quote.SubTotal.Amount);

        // Une nouvelle ligne, en revanche, repart du catalogue.
        var reresolved = await resolver.ResolveUnitPriceAsync(ClientId, product.Id, 5m, Date);
        Assert.Equal(PriceSource.Catalog, reresolved.Value.Source);
        Assert.Equal(100m, reresolved.Value.UnitPriceHT.Amount);
    }

    [Fact]
    public async Task SalesOrder_KeepsItsResolvedPrice_WhenPriceListChangesAfterwards()
    {
        var product = NewProduct(catalogHt: 100m);
        var priceList = PriceList.Create("Grille grossiste").Value;
        priceList.SetPrice(product.Id, Money.Create(75m));

        var resolver = SetupResolver(product, priceList);

        var resolved = await resolver.ResolveUnitPriceAsync(ClientId, product.Id, 4m, Date);
        var order = NewSalesOrder();
        Assert.True(order.AddLine(product, 4m, resolved.Value.UnitPriceHT).IsSuccess);
        Assert.Equal(300m, order.SubTotal.Amount);

        priceList.SetPrice(product.Id, Money.Create(120m));

        Assert.Equal(75m, Assert.Single(order.Lines).UnitPrice.Amount);
        Assert.Equal(300m, order.SubTotal.Amount);
    }

    /// <summary>
    /// Sans grille ni prix négocié, le résolveur rend le prix catalogue : la ligne obtenue est
    /// identique à celle d'avant le lot 5. C'est la garantie de non-régression du câblage.
    /// </summary>
    [Fact]
    public async Task WithoutAnyPricing_ResolvedLineIsIdenticalToCatalogLine()
    {
        var product = NewProduct(catalogHt: 100m);
        var resolver = SetupResolver(product, priceList: null);

        var resolved = await resolver.ResolveUnitPriceAsync(ClientId, product.Id, 10m, Date);
        Assert.Equal(PriceSource.Catalog, resolved.Value.Source);

        // Ligne câblée sur le résolveur.
        var wired = NewQuote();
        Assert.True(wired.AddLine(product, 10m, resolved.Value.UnitPriceHT, discountPercent: 10m).IsSuccess);

        // Ligne d'avant le lot 5 : le domaine retombait sur product.UnitPrice.
        var legacy = NewQuote();
        Assert.True(legacy.AddLine(product, 10m, customUnitPrice: null, discountPercent: 10m).IsSuccess);

        Assert.Equal(legacy.SubTotal.Amount, wired.SubTotal.Amount);
        Assert.Equal(legacy.TotalVat.Amount, wired.TotalVat.Amount);
        Assert.Equal(legacy.TotalAmount.Amount, wired.TotalAmount.Amount);
        Assert.Equal(
            Assert.Single(legacy.Lines).UnitPrice.Amount,
            Assert.Single(wired.Lines).UnitPrice.Amount);
    }

    // ───────────────────────────────── Montage ─────────────────────────────────

    private PriceResolver SetupResolver(Product product, PriceList? priceList)
    {
        _products.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        _clientPrices.Setup(r => r.GetForClientProductAsync(ClientId, product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientProductPrice?)null);

        var client = NewClient();
        if (priceList is not null)
        {
            client.AssignPriceList(priceList.Id);
            // Le repository rend la MÊME instance : muter la grille dans le test reproduit
            // fidèlement une modification en base entre deux résolutions.
            _priceLists.Setup(r => r.GetByIdWithItemsAsync(priceList.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(priceList);
        }

        _clients.Setup(r => r.GetByIdAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(client);

        return new PriceResolver(_products.Object, _clients.Object, _priceLists.Object, _clientPrices.Object);
    }

    private static Product NewProduct(decimal catalogHt) =>
        Product.Create(
            code: "P-GEL",
            name: "Produit gel de prix",
            type: ProductType.Product,
            unitPrice: Money.Create(catalogHt),
            vatRate: VatRate.Standard,
            categoryId: Guid.NewGuid(),
            unit: "Unité",
            isFodecApplicable: false).Value;

    private static Client NewClient() =>
        Client.Create(
            "Client test", ClientType.Business,
            Address.Create("Rue 1", "Tunis", "Tunis").Value,
            Email.Create("client@test.tn").Value,
            NIF.Create("1234567/A/B/C/000").Value).Value;

    private static Quote NewQuote() =>
        Quote.Create(
            QuoteNumber.Create("DEV", 2026, 1),
            NewClient(),
            new DateTime(2026, 7, 20),
            new DateTime(2026, 8, 20)).Value;

    private static SalesOrder NewSalesOrder() =>
        SalesOrder.Create(
            SalesOrderNumber.Create("CDE", 2026, 1),
            NewClient(),
            new DateTime(2026, 7, 20)).Value;
}
