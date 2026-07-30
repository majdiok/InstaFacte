using FactuTrust.Domain.Entities.Pricing;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

/// <summary>Promotions datées (tranche 5C).</summary>
public sealed class PromotionTests
{
    private static readonly Guid ProductId = Guid.NewGuid();
    private static readonly Guid CategoryId = Guid.NewGuid();
    private static readonly Guid ClientId = Guid.NewGuid();

    [Fact]
    public void IsRunningAt_RespectsItsWindowInclusively()
    {
        var promo = NewPercentPromotion(10m,
            start: new DateTime(2026, 7, 1), end: new DateTime(2026, 7, 31));

        Assert.False(promo.IsRunningAt(new DateTime(2026, 6, 30)));
        Assert.True(promo.IsRunningAt(new DateTime(2026, 7, 1)));
        Assert.True(promo.IsRunningAt(new DateTime(2026, 7, 31)));
        Assert.False(promo.IsRunningAt(new DateTime(2026, 8, 1)));
    }

    [Fact]
    public void DeactivatedPromotion_NeverRuns()
    {
        var promo = NewPercentPromotion(10m,
            start: new DateTime(2026, 7, 1), end: new DateTime(2026, 7, 31));
        promo.Deactivate();

        Assert.False(promo.IsRunningAt(new DateTime(2026, 7, 15)));
    }

    [Fact]
    public void ANullTarget_MeansAll()
    {
        var promo = NewPercentPromotion(10m);

        Assert.True(promo.Matches(ProductId, CategoryId, ClientId, 1m));
        Assert.True(promo.Matches(Guid.NewGuid(), null, null, 1m));
    }

    [Fact]
    public void AProductTargetedPromotion_IgnoresOtherProducts()
    {
        var promo = NewPercentPromotion(10m, productId: ProductId);

        Assert.True(promo.Matches(ProductId, CategoryId, ClientId, 1m));
        Assert.False(promo.Matches(Guid.NewGuid(), CategoryId, ClientId, 1m));
    }

    [Fact]
    public void ACategoryTargetedPromotion_MatchesProductsOfThatCategoryOnly()
    {
        var promo = NewPercentPromotion(10m, categoryId: CategoryId);

        Assert.True(promo.Matches(ProductId, CategoryId, null, 1m));
        Assert.False(promo.Matches(ProductId, Guid.NewGuid(), null, 1m));
        // Produit sans catégorie connue : on ne devine pas.
        Assert.False(promo.Matches(ProductId, null, null, 1m));
    }

    [Fact]
    public void AClientTargetedPromotion_IgnoresAnonymousSales()
    {
        var promo = NewPercentPromotion(10m, clientId: ClientId);

        Assert.True(promo.Matches(ProductId, null, ClientId, 1m));
        Assert.False(promo.Matches(ProductId, null, null, 1m));
    }

    [Fact]
    public void MinQuantity_GatesThePromotion()
    {
        var promo = NewPercentPromotion(10m, minQuantity: 10m);

        Assert.False(promo.Matches(ProductId, null, null, 9m));
        Assert.True(promo.Matches(ProductId, null, null, 10m));
    }

    [Fact]
    public void Specificity_RanksProductAndClientAboveCategory()
    {
        var all = NewPercentPromotion(5m);
        var category = NewPercentPromotion(5m, categoryId: CategoryId);
        var product = NewPercentPromotion(5m, productId: ProductId);
        var productAndClient = NewPercentPromotion(5m, productId: ProductId, clientId: ClientId);

        Assert.True(category.Specificity > all.Specificity);
        Assert.True(product.Specificity > category.Specificity);
        Assert.True(productAndClient.Specificity > product.Specificity);
    }

    [Fact]
    public void ComputeDiscount_AppliesThePercentToTheLineTotal()
    {
        var promo = NewPercentPromotion(10m);

        var discount = promo.ComputeDiscount(Money.Create(100m), 5m);

        Assert.Equal(50m, discount.Amount);
    }

    [Fact]
    public void ComputeDiscount_AppliesTheFixedAmountPerUnit()
    {
        var promo = Promotion.Create(
            "Remise unitaire", new DateTime(2026, 7, 1), new DateTime(2026, 7, 31),
            PromotionDiscountType.Amount, null, Money.Create(3m)).Value;

        var discount = promo.ComputeDiscount(Money.Create(100m), 5m);

        Assert.Equal(15m, discount.Amount);
    }

    [Fact]
    public void ComputeDiscount_NeverExceedsTheLineTotal()
    {
        var promo = Promotion.Create(
            "Remise démesurée", new DateTime(2026, 7, 1), new DateTime(2026, 7, 31),
            PromotionDiscountType.Amount, null, Money.Create(500m)).Value;

        // Une remise ne rend pas d'argent au client.
        var discount = promo.ComputeDiscount(Money.Create(100m), 1m);

        Assert.Equal(100m, discount.Amount);
    }

    [Fact]
    public void Create_RefusesAnInvertedWindow()
    {
        var result = Promotion.Create(
            "Promo", new DateTime(2026, 7, 31), new DateTime(2026, 7, 1),
            PromotionDiscountType.Percentage, 10m, null);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Create_RefusesAPercentagePromotionWithoutARate()
    {
        var result = Promotion.Create(
            "Promo", new DateTime(2026, 7, 1), new DateTime(2026, 7, 31),
            PromotionDiscountType.Percentage, null, null);

        Assert.True(result.IsFailure);
    }

    private static Promotion NewPercentPromotion(
        decimal percent,
        DateTime? start = null,
        DateTime? end = null,
        Guid? productId = null,
        Guid? categoryId = null,
        Guid? clientId = null,
        decimal minQuantity = 1m) =>
        Promotion.Create(
            "Promotion test",
            start ?? new DateTime(2026, 1, 1),
            end ?? new DateTime(2026, 12, 31),
            PromotionDiscountType.Percentage,
            percent,
            null,
            productId,
            categoryId,
            clientId,
            minQuantity).Value;
}

/// <summary>
/// Conditions de règlement structurées (tranche 5C). C'est le calcul d'échéance qui manquait
/// au champ texte libre, et sans lequel aucune balance âgée n'est fiable.
/// </summary>
public sealed class PaymentTermTemplateTests
{
    [Fact]
    public void NetDays_AddsTheDelayToTheDocumentDate()
    {
        var term = PaymentTermTemplate.Create("30 jours", 30).Value;

        Assert.Equal(new DateTime(2026, 8, 19), term.ComputeDueDate(new DateTime(2026, 7, 20)));
    }

    [Fact]
    public void EndOfMonth_PushesToTheLastDayOfThatMonth()
    {
        var term = PaymentTermTemplate.Create(
            "30 jours fin de mois", 30, PaymentDueMode.EndOfMonth).Value;

        // 20/07 + 30 j = 19/08 → 31/08
        Assert.Equal(new DateTime(2026, 8, 31), term.ComputeDueDate(new DateTime(2026, 7, 20)));
    }

    [Fact]
    public void EndOfMonthOnDay_PushesToThatDayOfTheFollowingMonth()
    {
        var term = PaymentTermTemplate.Create(
            "30 jours fin de mois le 10", 30, PaymentDueMode.EndOfMonthOnDay, dueDayOfMonth: 10).Value;

        // 20/07 + 30 j = 19/08 → le 10 du mois suivant = 10/09
        Assert.Equal(new DateTime(2026, 9, 10), term.ComputeDueDate(new DateTime(2026, 7, 20)));
    }

    [Fact]
    public void EndOfMonthOnDay_ClampsToTheLastDayOfAShortMonth()
    {
        var term = PaymentTermTemplate.Create(
            "fin de mois le 31", 0, PaymentDueMode.EndOfMonthOnDay, dueDayOfMonth: 31).Value;

        // Janvier → le 31 février n'existe pas : on retombe sur le 28 (2026 n'est pas bissextile).
        Assert.Equal(new DateTime(2026, 2, 28), term.ComputeDueDate(new DateTime(2026, 1, 15)));
    }

    [Fact]
    public void ComputeEarlyPaymentDeadline_IsNullWithoutADiscount()
    {
        var term = PaymentTermTemplate.Create("30 jours", 30).Value;

        Assert.Null(term.ComputeEarlyPaymentDeadline(new DateTime(2026, 7, 20)));
    }

    [Fact]
    public void ComputeEarlyPaymentDeadline_UsesTheDiscountWindow()
    {
        var term = PaymentTermTemplate.Create(
            "30 jours, escompte 2 % sous 10", 30,
            earlyPaymentDiscountPercent: 2m, earlyPaymentDays: 10).Value;

        Assert.Equal(new DateTime(2026, 7, 30), term.ComputeEarlyPaymentDeadline(new DateTime(2026, 7, 20)));
    }

    [Fact]
    public void Create_RefusesADiscountWindowLongerThanTheDelay()
    {
        // Un escompte « sous 45 jours » avec un délai de 30 serait toujours acquis.
        var result = PaymentTermTemplate.Create(
            "Incohérent", 30, earlyPaymentDiscountPercent: 2m, earlyPaymentDays: 45);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Create_RefusesADiscountRateWithoutAWindow()
    {
        var result = PaymentTermTemplate.Create("Incomplet", 30, earlyPaymentDiscountPercent: 2m);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Create_RefusesAnInvalidDayOfMonth()
    {
        var result = PaymentTermTemplate.Create(
            "Jour invalide", 30, PaymentDueMode.EndOfMonthOnDay, dueDayOfMonth: 45);

        Assert.True(result.IsFailure);
    }

    [Theory]
    [InlineData(0, "Paiement comptant")]
    [InlineData(30, "Paiement à 30 jours")]
    public void ToDocumentLabel_ProducesWhatThePrintedDocumentShows(int days, string expected)
    {
        var term = PaymentTermTemplate.Create("T", days).Value;

        Assert.Equal(expected, term.ToDocumentLabel());
    }

    [Fact]
    public void ToDocumentLabel_MentionsTheEarlyPaymentDiscount()
    {
        var term = PaymentTermTemplate.Create(
            "30 j escompte", 30, earlyPaymentDiscountPercent: 2m, earlyPaymentDays: 10).Value;

        Assert.Contains("escompte 2 % sous 10 jours", term.ToDocumentLabel());
    }
}
