using FactuTrust.Application.Common.Interfaces.Pricing;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Infrastructure.Services.Pricing;

/// <summary>
/// Orchestre la résolution prix + promotion pour une ligne documentaire.
/// </summary>
public sealed class LinePricingOrchestrator : ILinePricingOrchestrator
{
    private readonly IPriceResolver _priceResolver;
    private readonly IPromotionResolver _promotionResolver;

    public LinePricingOrchestrator(
        IPriceResolver priceResolver,
        IPromotionResolver promotionResolver)
    {
        _priceResolver = priceResolver;
        _promotionResolver = promotionResolver;
    }

    public async Task<Result<ResolvedLinePricing>> ResolveAsync(
        Guid clientId,
        Product product,
        decimal quantity,
        DateTime documentDate,
        decimal? manualDiscountPercent,
        Money? priceOverride,
        CancellationToken cancellationToken = default)
    {
        Money unitPrice;
        if (priceOverride is not null)
        {
            unitPrice = priceOverride;
        }
        else
        {
            var priceResult = await _priceResolver.ResolveUnitPriceAsync(
                clientId, product.Id, quantity, documentDate, cancellationToken);
            if (priceResult.IsFailure)
                return Result.Failure<ResolvedLinePricing>(priceResult.Error);

            unitPrice = priceResult.Value.UnitPriceHT;
        }

        PromotionResolution? appliedPromotion = null;
        var discountPercent = manualDiscountPercent;

        if (discountPercent is null)
        {
            var promo = await _promotionResolver.ResolveAsync(
                product.Id, product.CategoryId, clientId,
                quantity, unitPrice, documentDate, cancellationToken);

            if (promo.IsFailure)
                return Result.Failure<ResolvedLinePricing>(promo.Error);

            if (promo.Value is { } applied)
            {
                appliedPromotion = applied;
                discountPercent = applied.DiscountPercent;
            }
        }

        if (discountPercent is { } percent
            && product.IsDiscountEnabled
            && product.MaxDiscountPercent.HasValue
            && percent > product.MaxDiscountPercent.Value)
        {
            discountPercent = product.MaxDiscountPercent.Value;
        }

        return Result.Success(new ResolvedLinePricing(unitPrice, discountPercent, appliedPromotion));
    }
}
