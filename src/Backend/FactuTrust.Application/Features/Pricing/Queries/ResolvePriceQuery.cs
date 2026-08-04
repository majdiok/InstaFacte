using FactuTrust.Application.Common.Interfaces.Pricing;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Pricing.Queries;

/// <summary>
/// Prix résolu tel qu'il sera gravé sur la ligne, et la raison de ce choix.
/// </summary>
public sealed class ResolvedPriceDto
{
    public decimal UnitPriceHT { get; init; }
    public string Currency { get; init; } = string.Empty;

    /// <summary>Origine du prix : <c>Catalog</c>, <c>PriceList</c> ou <c>ClientPrice</c>.</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>
    /// Vrai lorsque le prix provient d'une grille ou d'un accord client, et non du catalogue.
    /// </summary>
    public bool IsNegotiated { get; init; }

    /// <summary>Remise promotionnelle applicable (pourcentage de ligne).</summary>
    public decimal? PromotionDiscountPercent { get; init; }

    public string? PromotionName { get; init; }
    public Guid? PromotionId { get; init; }

    /// <summary>Vrai si une promotion s'applique à cette ligne.</summary>
    public bool PromotionEligible { get; init; }

    /// <summary>Quantité minimale requise quand une promotion existe mais n'est pas encore atteinte.</summary>
    public decimal? PromotionMinQuantityRequired { get; init; }
}

/// <summary>
/// Interroge le point de résolution unique avant la création d'une ligne, pour que l'écran
/// affiche le prix et la promotion que le serveur appliquera.
/// </summary>
public sealed record ResolvePriceQuery(
    Guid? ClientId,
    Guid ProductId,
    decimal Quantity,
    DateTime? Date) : IRequest<Result<ResolvedPriceDto>>;

public sealed class ResolvePriceQueryHandler
    : IRequestHandler<ResolvePriceQuery, Result<ResolvedPriceDto>>
{
    private readonly ILinePricingOrchestrator _linePricingOrchestrator;
    private readonly IProductRepository _productRepository;
    private readonly IPromotionRepository _promotionRepository;
    private readonly IPriceResolver _priceResolver;

    public ResolvePriceQueryHandler(
        ILinePricingOrchestrator linePricingOrchestrator,
        IProductRepository productRepository,
        IPromotionRepository promotionRepository,
        IPriceResolver priceResolver)
    {
        _linePricingOrchestrator = linePricingOrchestrator;
        _productRepository = productRepository;
        _promotionRepository = promotionRepository;
        _priceResolver = priceResolver;
    }

    public async Task<Result<ResolvedPriceDto>> Handle(
        ResolvePriceQuery request, CancellationToken cancellationToken)
    {
        if (request.ProductId == Guid.Empty)
            return Result.Failure<ResolvedPriceDto>(Error.Validation("ProductId", "Le produit est obligatoire"));

        if (request.Quantity <= 0)
            return Result.Failure<ResolvedPriceDto>(Error.Validation("Quantity", "La quantité doit être supérieure à zéro"));

        var date = request.Date ?? DateTime.UtcNow.Date;

        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product is null)
            return Result.Failure<ResolvedPriceDto>(Error.NotFound("Produit", request.ProductId));

        var clientId = request.ClientId ?? Guid.Empty;
        if (clientId == Guid.Empty)
        {
            var priceOnly = await _priceResolver.ResolveUnitPriceAsync(
                null, product.Id, request.Quantity, date, cancellationToken);
            if (priceOnly.IsFailure)
                return Result.Failure<ResolvedPriceDto>(priceOnly.Error);

            return Result.Success(new ResolvedPriceDto
            {
                UnitPriceHT = priceOnly.Value.UnitPriceHT.Amount,
                Currency = priceOnly.Value.UnitPriceHT.Currency,
                Source = priceOnly.Value.Source.ToString(),
                IsNegotiated = priceOnly.Value.Source != Domain.Enums.PriceSource.Catalog
            });
        }

        var pricing = await _linePricingOrchestrator.ResolveAsync(
            clientId,
            product,
            request.Quantity,
            date,
            manualDiscountPercent: null,
            priceOverride: null,
            cancellationToken);

        if (pricing.IsFailure)
            return Result.Failure<ResolvedPriceDto>(pricing.Error);

        var priceResult = await _priceResolver.ResolveUnitPriceAsync(
            clientId, product.Id, request.Quantity, date, cancellationToken);
        if (priceResult.IsFailure)
            return Result.Failure<ResolvedPriceDto>(priceResult.Error);

        var applied = pricing.Value.AppliedPromotion;
        decimal? minQtyHint = null;
        string? hintName = null;

        if (applied is null)
        {
            var running = await _promotionRepository.GetRunningAtAsync(date, cancellationToken);
            var categoryId = product.CategoryId;
            var scoped = running
                .Where(p => p.MatchesScope(product.Id, categoryId, clientId))
                .OrderByDescending(p => p.Priority)
                .ThenByDescending(p => p.Specificity)
                .FirstOrDefault();

            if (scoped is not null && request.Quantity < scoped.MinQuantity)
            {
                minQtyHint = scoped.MinQuantity;
                hintName = scoped.Name;
            }
        }

        return Result.Success(new ResolvedPriceDto
        {
            UnitPriceHT = pricing.Value.UnitPriceHT.Amount,
            Currency = pricing.Value.UnitPriceHT.Currency,
            Source = priceResult.Value.Source.ToString(),
            IsNegotiated = priceResult.Value.Source != Domain.Enums.PriceSource.Catalog,
            PromotionDiscountPercent = pricing.Value.DiscountPercent,
            PromotionName = applied?.PromotionName ?? hintName,
            PromotionId = applied?.PromotionId,
            PromotionEligible = applied is not null,
            PromotionMinQuantityRequired = minQtyHint
        });
    }
}
