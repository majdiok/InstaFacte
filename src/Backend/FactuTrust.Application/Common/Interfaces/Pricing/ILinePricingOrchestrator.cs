using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Application.Common.Interfaces.Pricing;

/// <summary>
/// Prix unitaire résolu + remise de ligne (promotion ou manuelle) pour une ligne documentaire.
/// </summary>
public sealed record ResolvedLinePricing(
    Money UnitPriceHT,
    decimal? DiscountPercent,
    PromotionResolution? AppliedPromotion);

/// <summary>
/// Point d'orchestration unique : résout le prix puis la promotion applicable, en respectant
/// la priorité remise manuelle &gt; promotion automatique et le plafond catalogue.
/// </summary>
public interface ILinePricingOrchestrator
{
    Task<Result<ResolvedLinePricing>> ResolveAsync(
        Guid clientId,
        Product product,
        decimal quantity,
        DateTime documentDate,
        decimal? manualDiscountPercent,
        Money? priceOverride,
        CancellationToken cancellationToken = default);
}
