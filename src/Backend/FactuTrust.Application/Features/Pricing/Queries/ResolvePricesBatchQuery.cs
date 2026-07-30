using FactuTrust.Application.Common.Interfaces.Pricing;
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
}

/// <summary>
/// Résout le prix de plusieurs produits en une seule fois.
///
/// Indispensable en caisse : lorsqu'un caissier rattache une commande de vingt lignes à un
/// client porteur d'une grille, retarifer ligne à ligne demanderait vingt allers-retours.
/// </summary>
public sealed record ResolvePricesBatchQuery(
    Guid? ClientId,
    IReadOnlyList<ResolvePriceItemDto> Items,
    DateTime? Date) : IRequest<Result<IReadOnlyList<ResolvedPriceLineDto>>>;

public sealed class ResolvePricesBatchQueryHandler
    : IRequestHandler<ResolvePricesBatchQuery, Result<IReadOnlyList<ResolvedPriceLineDto>>>
{
    /// <summary>
    /// Garde-fou : une requête de lot n'est pas un moyen d'aspirer le référentiel tarifaire.
    /// Un ticket de caisse dépasse rarement quelques dizaines de lignes.
    /// </summary>
    private const int MaxItems = 200;

    private readonly IPriceResolver _priceResolver;

    public ResolvePricesBatchQueryHandler(IPriceResolver priceResolver)
    {
        _priceResolver = priceResolver;
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

        var date = request.Date ?? DateTime.UtcNow.Date;
        var resolved = new List<ResolvedPriceLineDto>(request.Items.Count);

        // Les produits répétés ne sont résolus qu'une fois : en caisse, le même article revient
        // souvent sur plusieurs lignes.
        var seen = new Dictionary<Guid, ResolvedPriceLineDto>();

        foreach (var item in request.Items)
        {
            if (item.ProductId == Guid.Empty)
                continue;

            if (seen.TryGetValue(item.ProductId, out var already))
            {
                resolved.Add(already);
                continue;
            }

            var result = await _priceResolver.ResolveUnitPriceAsync(
                request.ClientId, item.ProductId, item.Quantity <= 0 ? 1m : item.Quantity,
                date, cancellationToken);

            // Un produit introuvable n'invalide pas tout le lot : la ligne est simplement
            // omise, et l'appelant conserve le prix qu'il affichait déjà.
            if (result.IsFailure)
                continue;

            var line = new ResolvedPriceLineDto
            {
                ProductId = item.ProductId,
                UnitPriceHT = result.Value.UnitPriceHT.Amount,
                Currency = result.Value.UnitPriceHT.Currency,
                Source = result.Value.Source.ToString(),
                IsNegotiated = result.Value.Source != Domain.Enums.PriceSource.Catalog
            };

            seen[item.ProductId] = line;
            resolved.Add(line);
        }

        return Result.Success<IReadOnlyList<ResolvedPriceLineDto>>(resolved);
    }
}
