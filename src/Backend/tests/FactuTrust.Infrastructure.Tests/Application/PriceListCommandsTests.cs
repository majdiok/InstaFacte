using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.Pricing.Commands;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.Pricing;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

/// <summary>
/// Garde-fous de l'administration des grilles tarifaires. Ce sont eux qui empêchent qu'une
/// manipulation d'écran fasse retomber des clients au catalogue sans que personne le voie.
/// </summary>
public sealed class PriceListCommandsTests
{
    private readonly Mock<IPriceListRepository> _priceLists = new();
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IClientRepository> _clients = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IAuditService> _audit = new();

    public PriceListCommandsTests()
    {
        _currentUser.SetupGet(u => u.UserId).Returns(Guid.NewGuid());
    }

    [Fact]
    public async Task DeletePriceList_IsRefused_WhenStillAssignedToClients()
    {
        var priceList = PriceList.Create("Grille grossiste").Value;
        _priceLists.Setup(r => r.GetByIdAsync(priceList.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(priceList);
        _priceLists.Setup(r => r.CountAssignedClientsAsync(priceList.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var handler = new DeletePriceListCommandHandler(_priceLists.Object, _audit.Object);

        var result = await handler.Handle(new DeletePriceListCommand(priceList.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("3 client", result.Error.Description);
        _priceLists.Verify(
            r => r.DeleteAsync(It.IsAny<PriceList>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeletePriceList_Succeeds_WhenNoClientIsAssigned()
    {
        var priceList = PriceList.Create("Grille abandonnée").Value;
        _priceLists.Setup(r => r.GetByIdAsync(priceList.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(priceList);
        _priceLists.Setup(r => r.CountAssignedClientsAsync(priceList.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var handler = new DeletePriceListCommandHandler(_priceLists.Object, _audit.Object);

        var result = await handler.Handle(new DeletePriceListCommand(priceList.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _priceLists.Verify(r => r.DeleteAsync(priceList, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetPriceListItem_IsRefused_WhenProductDoesNotExist()
    {
        var priceList = PriceList.Create("Grille grossiste").Value;
        var productId = Guid.NewGuid();

        _priceLists.Setup(r => r.GetByIdWithItemsAsync(priceList.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(priceList);
        _products.Setup(r => r.GetByIdAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        var handler = new SetPriceListItemCommandHandler(
            _priceLists.Object, _products.Object, _currentUser.Object, _audit.Object);

        var result = await handler.Handle(
            new SetPriceListItemCommand(priceList.Id, productId, 80m), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(priceList.Items);
    }

    [Fact]
    public async Task SetPriceListItem_UpsertsInTheGridCurrency()
    {
        var priceList = PriceList.Create("Grille grossiste").Value;
        var product = NewProduct();

        _priceLists.Setup(r => r.GetByIdWithItemsAsync(priceList.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(priceList);
        _products.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var handler = new SetPriceListItemCommandHandler(
            _priceLists.Object, _products.Object, _currentUser.Object, _audit.Object);

        Assert.True((await handler.Handle(
            new SetPriceListItemCommand(priceList.Id, product.Id, 80m), CancellationToken.None)).IsSuccess);

        // Deuxième appel sur le même produit : mise à jour, pas de doublon.
        Assert.True((await handler.Handle(
            new SetPriceListItemCommand(priceList.Id, product.Id, 70m), CancellationToken.None)).IsSuccess);

        var item = Assert.Single(priceList.Items);
        Assert.Equal(70m, item.UnitPriceHT.Amount);
        Assert.Equal(priceList.Currency, item.UnitPriceHT.Currency);
    }

    [Fact]
    public async Task AssignClientPriceList_IsRefused_WhenPriceListDoesNotExist()
    {
        var client = NewClient();
        var ghostPriceListId = Guid.NewGuid();

        _clients.Setup(r => r.GetByIdAsync(client.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(client);
        _priceLists.Setup(r => r.ExistsAsync(ghostPriceListId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new AssignClientPriceListCommandHandler(
            _clients.Object, _priceLists.Object, _currentUser.Object, _audit.Object);

        var result = await handler.Handle(
            new AssignClientPriceListCommand(client.Id, ghostPriceListId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Null(client.PriceListId);
    }

    [Fact]
    public async Task AssignClientPriceList_AcceptsNull_ToReturnToCatalog()
    {
        var client = NewClient();
        client.AssignPriceList(Guid.NewGuid());

        _clients.Setup(r => r.GetByIdAsync(client.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(client);

        var handler = new AssignClientPriceListCommandHandler(
            _clients.Object, _priceLists.Object, _currentUser.Object, _audit.Object);

        var result = await handler.Handle(
            new AssignClientPriceListCommand(client.Id, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(client.PriceListId);
    }

    private static Product NewProduct() =>
        Product.Create(
            code: "P-GRID",
            name: "Produit grille",
            type: ProductType.Product,
            unitPrice: Money.Create(100m),
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
