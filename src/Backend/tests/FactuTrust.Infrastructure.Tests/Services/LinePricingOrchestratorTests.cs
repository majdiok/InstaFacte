using FactuTrust.Application.Common.Interfaces.Pricing;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.Services.Pricing;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

public sealed class LinePricingOrchestratorTests
{
    private static readonly Guid ClientId = Guid.NewGuid();
    private static readonly DateTime DocumentDate = new(2026, 8, 1);

    [Fact]
    public async Task WithoutPromotion_ReturnsResolvedPriceOnly()
    {
        var product = NewProduct();
        var priceResolver = new Mock<IPriceResolver>();
        priceResolver
            .Setup(r => r.ResolveUnitPriceAsync(ClientId, product.Id, 1m, DocumentDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new PriceResolution(Money.Create(100m), PriceSource.Catalog)));

        var promotionResolver = new Mock<IPromotionResolver>();
        promotionResolver
            .Setup(r => r.ResolveAsync(product.Id, product.CategoryId, ClientId, 1m, It.IsAny<Money>(), DocumentDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<PromotionResolution?>(null));

        var orchestrator = new LinePricingOrchestrator(priceResolver.Object, promotionResolver.Object);

        var result = await orchestrator.ResolveAsync(ClientId, product, 1m, DocumentDate, null, null);

        Assert.True(result.IsSuccess);
        Assert.Equal(100m, result.Value.UnitPriceHT.Amount);
        Assert.Null(result.Value.DiscountPercent);
        Assert.Null(result.Value.AppliedPromotion);
    }

    [Fact]
    public async Task WithPromotion_AppliesDiscountPercent()
    {
        var product = NewProduct();
        var promoId = Guid.NewGuid();
        var priceResolver = new Mock<IPriceResolver>();
        priceResolver
            .Setup(r => r.ResolveUnitPriceAsync(ClientId, product.Id, 3m, DocumentDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new PriceResolution(Money.Create(1430m), PriceSource.Catalog)));

        var promotionResolver = new Mock<IPromotionResolver>();
        promotionResolver
            .Setup(r => r.ResolveAsync(product.Id, product.CategoryId, ClientId, 3m, It.IsAny<Money>(), DocumentDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<PromotionResolution?>(new PromotionResolution(promoId, "solde été", 10m)));

        var orchestrator = new LinePricingOrchestrator(priceResolver.Object, promotionResolver.Object);

        var result = await orchestrator.ResolveAsync(ClientId, product, 3m, DocumentDate, null, null);

        Assert.True(result.IsSuccess);
        Assert.Equal(10m, result.Value.DiscountPercent);
        Assert.Equal("solde été", result.Value.AppliedPromotion!.PromotionName);
    }

    [Fact]
    public async Task ManualDiscount_SkipsPromotionResolver()
    {
        var product = NewProduct();
        var priceResolver = new Mock<IPriceResolver>();
        priceResolver
            .Setup(r => r.ResolveUnitPriceAsync(ClientId, product.Id, 3m, DocumentDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new PriceResolution(Money.Create(100m), PriceSource.Catalog)));

        var promotionResolver = new Mock<IPromotionResolver>();

        var orchestrator = new LinePricingOrchestrator(priceResolver.Object, promotionResolver.Object);

        var result = await orchestrator.ResolveAsync(ClientId, product, 3m, DocumentDate, 5m, null);

        Assert.True(result.IsSuccess);
        Assert.Equal(5m, result.Value.DiscountPercent);
        Assert.Null(result.Value.AppliedPromotion);
        promotionResolver.Verify(
            r => r.ResolveAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<decimal>(), It.IsAny<Money>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PromotionAboveMaxDiscount_IsCapped()
    {
        var product = NewProductWithDiscountCap(maxPercent: 5m);
        var priceResolver = new Mock<IPriceResolver>();
        priceResolver
            .Setup(r => r.ResolveUnitPriceAsync(ClientId, product.Id, 3m, DocumentDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new PriceResolution(Money.Create(100m), PriceSource.Catalog)));

        var promotionResolver = new Mock<IPromotionResolver>();
        promotionResolver
            .Setup(r => r.ResolveAsync(product.Id, product.CategoryId, ClientId, 3m, It.IsAny<Money>(), DocumentDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<PromotionResolution?>(new PromotionResolution(Guid.NewGuid(), "solde", 10m)));

        var orchestrator = new LinePricingOrchestrator(priceResolver.Object, promotionResolver.Object);

        var result = await orchestrator.ResolveAsync(ClientId, product, 3m, DocumentDate, null, null);

        Assert.Equal(5m, result.Value.DiscountPercent);
    }

    private static Product NewProduct()
    {
        var result = Product.Create(
            "P-001", "Article test", ProductType.Product,
            Money.Create(100m), VatRate.Standard, Guid.NewGuid());
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static Product NewProductWithDiscountCap(decimal maxPercent)
    {
        var result = Product.Create(
            "P-002", "Article remise", ProductType.Product,
            Money.Create(100m), VatRate.Standard, Guid.NewGuid(),
            isDiscountEnabled: true,
            maxDiscountPercent: maxPercent);
        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
