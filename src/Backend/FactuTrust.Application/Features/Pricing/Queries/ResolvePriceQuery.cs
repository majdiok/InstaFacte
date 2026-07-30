using FactuTrust.Application.Common.Interfaces.Pricing;
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
    /// Permet à l'écran de signaler « prix négocié » plutôt que de laisser croire à une saisie.
    /// </summary>
    public bool IsNegotiated { get; init; }
}

/// <summary>
/// Interroge le point de résolution unique avant la création d'une ligne, pour que l'écran
/// affiche le prix que le serveur appliquera — et non un prix catalogue que le serveur
/// remplacerait ensuite en silence.
/// </summary>
public sealed record ResolvePriceQuery(
    Guid? ClientId,
    Guid ProductId,
    decimal Quantity,
    DateTime? Date) : IRequest<Result<ResolvedPriceDto>>;

public sealed class ResolvePriceQueryHandler
    : IRequestHandler<ResolvePriceQuery, Result<ResolvedPriceDto>>
{
    private readonly IPriceResolver _priceResolver;

    public ResolvePriceQueryHandler(IPriceResolver priceResolver)
    {
        _priceResolver = priceResolver;
    }

    public async Task<Result<ResolvedPriceDto>> Handle(
        ResolvePriceQuery request, CancellationToken cancellationToken)
    {
        if (request.ProductId == Guid.Empty)
            return Result.Failure<ResolvedPriceDto>(Error.Validation("ProductId", "Le produit est obligatoire"));

        if (request.Quantity <= 0)
            return Result.Failure<ResolvedPriceDto>(Error.Validation("Quantity", "La quantité doit être supérieure à zéro"));

        // Sans date fournie, on se place au jour du document en cours de saisie.
        var date = request.Date ?? DateTime.UtcNow.Date;

        var result = await _priceResolver.ResolveUnitPriceAsync(
            request.ClientId, request.ProductId, request.Quantity, date, cancellationToken);

        if (result.IsFailure)
            return Result.Failure<ResolvedPriceDto>(result.Error);

        var resolution = result.Value;

        return Result.Success(new ResolvedPriceDto
        {
            UnitPriceHT = resolution.UnitPriceHT.Amount,
            Currency = resolution.UnitPriceHT.Currency,
            Source = resolution.Source.ToString(),
            IsNegotiated = resolution.Source != Domain.Enums.PriceSource.Catalog
        });
    }
}
