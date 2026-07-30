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
/// Résolveur de promotions (tranche 5C). Le point sensible : sans promotion en cours, il ne
/// doit rien changer — c'est ce qui garantit que le câblage n'introduit aucune régression.
/// </summary>
public sealed class PromotionResolverTests
{
    private static readonly Guid ProductId = Guid.NewGuid();
    private static readonly Guid CategoryId = Guid.NewGuid();
    private static readonly Guid ClientId = Guid.NewGuid();
    private static readonly DateTime Date = new(2026, 07, 20);
    private static readonly Money UnitPrice = Money.Create(100m);

    private readonly Mock<IPromotionRepository> _promotions = new();
    private readonly Mock<IProductRepository> _products = new();

    private PromotionResolver Build(params Promotion[] running)
    {
        _promotions.Setup(r => r.GetRunningAtAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(running);

        _products.Setup(r => r.GetByIdAsync(ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewProduct());

        return new PromotionResolver(_promotions.Object, _products.Object);
    }

    [Fact]
    public async Task WithoutAnyRunningPromotion_ResolvesToNothing()
    {
        var resolver = Build();

        var result = await resolver.ResolveAsync(ProductId, CategoryId, ClientId, 5m, UnitPrice, Date);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task ANonMatchingPromotion_ResolvesToNothing()
    {
        var resolver = Build(NewPercent(10m, productId: Guid.NewGuid()));

        var result = await resolver.ResolveAsync(ProductId, CategoryId, ClientId, 5m, UnitPrice, Date);

        Assert.Null(result.Value);
    }

    [Fact]
    public async Task APercentagePromotion_IsReturnedAsIs()
    {
        var resolver = Build(NewPercent(15m, productId: ProductId));

        var result = await resolver.ResolveAsync(ProductId, CategoryId, ClientId, 5m, UnitPrice, Date);

        Assert.NotNull(result.Value);
        Assert.Equal(15m, result.Value!.DiscountPercent);
    }

    /// <summary>
    /// Une promotion en montant doit être convertie : sans cela, la ligne n'en verrait rien,
    /// puisqu'elle ne porte qu'une remise en pourcentage.
    /// </summary>
    [Fact]
    public async Task AnAmountPromotion_IsConvertedToAPercentOfTheLine()
    {
        var promo = Promotion.Create(
            "3 DT par unité", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31),
            PromotionDiscountType.Amount, null, Money.Create(3m), productId: ProductId).Value;

        var resolver = Build(promo);

        // 3 DT sur un prix de 100 = 3 % de la ligne, quelle que soit la quantité.
        var result = await resolver.ResolveAsync(ProductId, CategoryId, ClientId, 5m, UnitPrice, Date);

        Assert.NotNull(result.Value);
        Assert.Equal(3m, result.Value!.DiscountPercent);
    }

    [Fact]
    public async Task HigherPriority_Wins()
    {
        var low = NewPercent(5m, productId: ProductId, priority: 0);
        var high = NewPercent(20m, productId: ProductId, priority: 10);

        var resolver = Build(low, high);

        var result = await resolver.ResolveAsync(ProductId, CategoryId, ClientId, 1m, UnitPrice, Date);

        Assert.Equal(20m, result.Value!.DiscountPercent);
    }

    /// <summary>
    /// À priorité égale, la plus spécifique gagne — sinon la remise dépendrait de l'ordre de
    /// lecture en base, donc changerait d'une saisie à l'autre.
    /// </summary>
    [Fact]
    public async Task AtEqualPriority_TheMoreSpecificWins()
    {
        var broad = NewPercent(5m);
        var targeted = NewPercent(12m, productId: ProductId, clientId: ClientId);

        var resolver = Build(broad, targeted);

        var result = await resolver.ResolveAsync(ProductId, CategoryId, ClientId, 1m, UnitPrice, Date);

        Assert.Equal(12m, result.Value!.DiscountPercent);
    }

    [Fact]
    public async Task BelowMinQuantity_ThePromotionDoesNotApply()
    {
        var resolver = Build(NewPercent(10m, productId: ProductId, minQuantity: 10m));

        var below = await resolver.ResolveAsync(ProductId, CategoryId, ClientId, 9m, UnitPrice, Date);
        var reached = await resolver.ResolveAsync(ProductId, CategoryId, ClientId, 10m, UnitPrice, Date);

        Assert.Null(below.Value);
        Assert.NotNull(reached.Value);
    }

    [Fact]
    public async Task ACategoryPromotion_LoadsTheProductWhenTheCategoryIsUnknown()
    {
        var resolver = Build(NewPercent(8m, categoryId: CategoryId));

        // L'appelant ne fournit pas la catégorie : le résolveur va la chercher.
        var result = await resolver.ResolveAsync(ProductId, null, ClientId, 1m, UnitPrice, Date);

        Assert.NotNull(result.Value);
        Assert.Equal(8m, result.Value!.DiscountPercent);
        _products.Verify(r => r.GetByIdAsync(ProductId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WithoutAnyCategoryPromotion_TheProductIsNotLoaded()
    {
        var resolver = Build(NewPercent(8m, productId: ProductId));

        await resolver.ResolveAsync(ProductId, null, ClientId, 1m, UnitPrice, Date);

        // Aucune promotion ne raisonne par catégorie : inutile de payer une lecture produit.
        _products.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Promotion NewPercent(
        decimal percent,
        Guid? productId = null,
        Guid? categoryId = null,
        Guid? clientId = null,
        decimal minQuantity = 1m,
        int priority = 0) =>
        Promotion.Create(
            "Promotion test",
            new DateTime(2026, 1, 1),
            new DateTime(2026, 12, 31),
            PromotionDiscountType.Percentage,
            percent,
            null,
            productId,
            categoryId,
            clientId,
            minQuantity,
            priority).Value;

    private static Product NewProduct() =>
        Product.Create(
            code: "P-PROMO",
            name: "Produit promo",
            type: ProductType.Product,
            unitPrice: Money.Create(100m),
            vatRate: VatRate.Standard,
            categoryId: CategoryId,
            unit: "Unité").Value;
}
