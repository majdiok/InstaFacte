using FactuTrust.Application.Features.Pricing.Queries;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Résolution par lot — le chemin utilisé par la caisse pour retarifer un ticket entier lors du
/// rattachement à un client. Ce qui compte ici n'est pas le prix (couvert par
/// <see cref="PriceResolverTests"/>) mais la robustesse du lot : ne pas multiplier les appels,
/// ne pas invalider tout un ticket pour une ligne, et refuser les lots démesurés.
/// </summary>
public sealed class ResolvePricesBatchQueryTests
{
    private static readonly Guid ClientId = Guid.NewGuid();

    private readonly Mock<IMediator> _mediator = new();

    private ResolvePricesBatchQueryHandler CreateHandler() => new(_mediator.Object);

    private void SetupPrice(Guid productId, decimal amount, PriceSource source)
    {
        _mediator
            .Setup(m => m.Send(
                It.Is<ResolvePriceQuery>(q => q.ProductId == productId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new ResolvedPriceDto
            {
                UnitPriceHT = amount,
                Currency = "TND",
                Source = source.ToString(),
                IsNegotiated = source is PriceSource.PriceList or PriceSource.ClientPrice
            }));
    }

    [Fact]
    public async Task EmptyBatch_ReturnsEmpty_WithoutCallingTheResolver()
    {
        var result = await CreateHandler().Handle(
            new ResolvePricesBatchQuery(ClientId, Array.Empty<ResolvePriceItemDto>(), null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
        _mediator.Verify(
            m => m.Send(It.IsAny<ResolvePriceQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RepeatedProduct_IsResolvedOnlyOnce()
    {
        var productId = Guid.NewGuid();
        SetupPrice(productId, 80m, PriceSource.PriceList);

        var items = new[]
        {
            new ResolvePriceItemDto { ProductId = productId, Quantity = 1m },
            new ResolvePriceItemDto { ProductId = productId, Quantity = 3m },
            new ResolvePriceItemDto { ProductId = productId, Quantity = 2m }
        };

        var result = await CreateHandler().Handle(
            new ResolvePricesBatchQuery(ClientId, items, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Count);
        Assert.All(result.Value, r => Assert.Equal(80m, r.UnitPriceHT));

        // Same quantity key (1) is requested once; quantities 3 and 2 are distinct keys.
        _mediator.Verify(
            m => m.Send(It.Is<ResolvePriceQuery>(q => q.ProductId == productId), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task UnresolvableProduct_IsOmitted_WithoutFailingTheWholeBatch()
    {
        var good = Guid.NewGuid();
        var missing = Guid.NewGuid();

        SetupPrice(good, 50m, PriceSource.Catalog);
        _mediator
            .Setup(m => m.Send(
                It.Is<ResolvePriceQuery>(q => q.ProductId == missing),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<ResolvedPriceDto>(Error.NotFound("Product", missing)));

        var items = new[]
        {
            new ResolvePriceItemDto { ProductId = good, Quantity = 1m },
            new ResolvePriceItemDto { ProductId = missing, Quantity = 1m }
        };

        var result = await CreateHandler().Handle(
            new ResolvePricesBatchQuery(ClientId, items, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var line = Assert.Single(result.Value);
        Assert.Equal(good, line.ProductId);
    }

    [Fact]
    public async Task OversizedBatch_IsRefused()
    {
        var items = Enumerable.Range(0, 201)
            .Select(_ => new ResolvePriceItemDto { ProductId = Guid.NewGuid(), Quantity = 1m })
            .ToArray();

        var result = await CreateHandler().Handle(
            new ResolvePricesBatchQuery(ClientId, items, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        _mediator.Verify(
            m => m.Send(It.IsAny<ResolvePriceQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task NonPositiveQuantity_IsNormalisedToOne()
    {
        var productId = Guid.NewGuid();
        SetupPrice(productId, 40m, PriceSource.Catalog);

        var items = new[] { new ResolvePriceItemDto { ProductId = productId, Quantity = 0m } };

        var result = await CreateHandler().Handle(
            new ResolvePricesBatchQuery(ClientId, items, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _mediator.Verify(
            m => m.Send(
                It.Is<ResolvePriceQuery>(q => q.ProductId == productId && q.Quantity == 1m),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CatalogPrice_IsNotFlaggedAsNegotiated()
    {
        var catalog = Guid.NewGuid();
        var negotiated = Guid.NewGuid();

        SetupPrice(catalog, 100m, PriceSource.Catalog);
        SetupPrice(negotiated, 70m, PriceSource.ClientPrice);

        var items = new[]
        {
            new ResolvePriceItemDto { ProductId = catalog, Quantity = 1m },
            new ResolvePriceItemDto { ProductId = negotiated, Quantity = 1m }
        };

        var result = await CreateHandler().Handle(
            new ResolvePricesBatchQuery(ClientId, items, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Single(r => r.ProductId == catalog).IsNegotiated);
        Assert.True(result.Value.Single(r => r.ProductId == negotiated).IsNegotiated);
    }
}
