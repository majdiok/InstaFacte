using FactuTrust.Application.Common.Interfaces.Pricing;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Pricing;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Infrastructure.Services.Pricing;

/// <summary>
/// Résolveur de promotions (voir <see cref="IPromotionResolver"/>).
///
/// Choisit parmi les promotions qui courent à la date donnée celle qui vise la ligne, puis
/// départage : priorité la plus élevée, et à égalité la plus spécifique. Départager au hasard
/// donnerait des remises différentes d'une saisie à l'autre sur des données identiques.
/// </summary>
public sealed class PromotionResolver : IPromotionResolver
{
    private readonly IPromotionRepository _promotionRepository;
    private readonly IProductRepository _productRepository;

    public PromotionResolver(
        IPromotionRepository promotionRepository,
        IProductRepository productRepository)
    {
        _promotionRepository = promotionRepository;
        _productRepository = productRepository;
    }

    public async Task<Result<PromotionResolution?>> ResolveAsync(
        Guid productId,
        Guid? productCategoryId,
        Guid? clientId,
        decimal quantity,
        Money unitPriceHT,
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        var running = await _promotionRepository.GetRunningAtAsync(date, cancellationToken);
        if (running.Count == 0)
            return Result.Success<PromotionResolution?>(null);

        // La catégorie n'est chargée que si une promotion la cible : inutile de payer une
        // lecture produit quand aucune promotion ne raisonne par catégorie.
        var categoryId = productCategoryId;
        if (categoryId is null && running.Any(p => p.ProductCategoryId.HasValue))
        {
            var product = await _productRepository.GetByIdAsync(productId, cancellationToken);
            categoryId = product?.CategoryId;
        }

        var winner = running
            .Where(p => p.Matches(productId, categoryId, clientId, quantity))
            .OrderByDescending(p => p.Priority)
            .ThenByDescending(p => p.Specificity)
            .FirstOrDefault();

        if (winner is null)
            return Result.Success<PromotionResolution?>(null);

        var percent = ComputePercent(winner, unitPriceHT, quantity);
        if (percent <= 0)
            return Result.Success<PromotionResolution?>(null);

        return Result.Success<PromotionResolution?>(
            new PromotionResolution(winner.Id, winner.Name, percent));
    }

    /// <summary>
    /// Ramène la remise en pourcentage de la ligne. Une promotion en montant est convertie ici,
    /// pour que la ligne n'ait qu'un seul mode de remise et que le moteur de calcul
    /// (remise → FODEC → TVA) reste intact.
    ///
    /// L'arrondi à 2 décimales est celui de <c>DiscountPercent</c> en base : une promotion en
    /// montant peut donc dériver de quelques millimes sur la ligne. C'est le prix à payer pour
    /// ne pas introduire un second mode de remise dans les trois moteurs.
    /// </summary>
    internal static decimal ComputePercent(Promotion promotion, Money unitPrice, decimal quantity)
    {
        if (promotion.DiscountPercent is { } percent)
            return percent;

        var lineTotal = unitPrice.Multiply(quantity);
        if (lineTotal.Amount <= 0)
            return 0m;

        var discount = promotion.ComputeDiscount(unitPrice, quantity);

        return Math.Round(discount.Amount / lineTotal.Amount * 100m, 2);
    }
}
