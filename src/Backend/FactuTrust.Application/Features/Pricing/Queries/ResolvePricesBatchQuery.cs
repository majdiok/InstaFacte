using FactuTrust.Application.Common.Interfaces.Pricing;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Pricing.Queries;

/// <summary>Un couple produit / quantité dont on veut connaître le prix applicable.</summary>
public sealed class ResolvePriceItemDto
{
    public Guid ProductId { get; init; }
    public decimal Quantity { get; init; } = 1m;
}

/// <summary>Prix résolu pour un produit, dans une réponse par lot.</summary>
public sealed class ResolvedPriceLineDto
{
    public Guid ProductId { get; init; }
    public decimal UnitPriceHT { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public bool IsNegotiated { get; init; }
    public decimal? PromotionDiscountPercent { get; init; }
    public string? PromotionName { get; init; }
    public Guid? PromotionId { get; init; }
    public bool PromotionEligible { get; init; }
    public decimal? PromotionMinQuantityRequired { get; init; }
}

/// <summary>
/// Résout le prix de plusieurs produits en une seule fois.
/// </summary>
public sealed record ResolvePricesBatchQuery(
    Guid? ClientId,
    IReadOnlyList<ResolvePriceItemDto> Items,
    DateTime? Date) : IRequest<Result<IReadOnlyList<ResolvedPriceLineDto>>>;

public sealed class ResolvePricesBatchQueryHandler
    : IRequestHandler<ResolvePricesBatchQuery, Result<IReadOnlyList<ResolvedPriceLineDto>>>
{
    private const int MaxItems = 200;

    private readonly IMediator _mediator;

    public ResolvePricesBatchQueryHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<Result<IReadOnlyList<ResolvedPriceLineDto>>> Handle(
        ResolvePricesBatchQuery request, CancellationToken cancellationToken)
    {
        if (request.Items is null || request.Items.Count == 0)
            return Result.Success<IReadOnlyList<ResolvedPriceLineDto>>(Array.Empty<ResolvedPriceLineDto>());

        if (request.Items.Count > MaxItems)
        {
            return Result.Failure<IReadOnlyList<ResolvedPriceLineDto>>(
                Error.Validation("Items", $"Un lot ne peut pas dépasser {MaxItems} lignes"));
        }

        var resolved = new List<ResolvedPriceLineDto>(request.Items.Count);
        var seen = new Dictionary<(Guid ProductId, decimal Quantity), ResolvedPriceLineDto>();

        foreach (var item in request.Items)
        {
            if (item.ProductId == Guid.Empty)
                continue;

            var key = (item.ProductId, item.Quantity <= 0 ? 1m : item.Quantity);
            if (seen.TryGetValue(key, out var already))
            {
                resolved.Add(already);
                continue;
            }

            var result = await _mediator.Send(
                new ResolvePriceQuery(request.ClientId, item.ProductId, key.Item2, request.Date),
                cancellationToken);

            if (result.IsFailure)
                continue;

            var dto = result.Value;
            var line = new ResolvedPriceLineDto
            {
                ProductId = item.ProductId,
                UnitPriceHT = dto.UnitPriceHT,
                Currency = dto.Currency,
                Source = dto.Source,
                IsNegotiated = dto.IsNegotiated,
                PromotionDiscountPercent = dto.PromotionDiscountPercent,
                PromotionName = dto.PromotionName,
                PromotionId = dto.PromotionId,
                PromotionEligible = dto.PromotionEligible,
                PromotionMinQuantityRequired = dto.PromotionMinQuantityRequired
            };

            seen[key] = line;
            resolved.Add(line);
        }

        return Result.Success<IReadOnlyList<ResolvedPriceLineDto>>(resolved);
    }
}
