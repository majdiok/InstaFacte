using FactuTrust.Application.Common.Interfaces.Pricing;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Infrastructure.Services.Pricing;

/// <summary>
/// Résolveur de prix unique (voir <see cref="IPriceResolver"/>).
///
/// Priorité décroissante, premier applicable retenu :
///  1. prix négocié client / produit, s'il est actif et valide à la date ;
///  2. grille tarifaire affectée au client, si elle est applicable à la date et porte le produit ;
///  3. prix catalogue du produit — repli qui répond toujours.
///
/// Ce service ne fige rien lui-même : il répond un prix. C'est l'appelant (création de ligne)
/// qui grave ce prix sur le document, de sorte qu'un document émis ne bouge plus.
/// </summary>
public sealed class PriceResolver : IPriceResolver
{
    private readonly IProductRepository _productRepository;
    private readonly IClientRepository _clientRepository;
    private readonly IPriceListRepository _priceListRepository;
    private readonly IClientProductPriceRepository _clientProductPriceRepository;

    public PriceResolver(
        IProductRepository productRepository,
        IClientRepository clientRepository,
        IPriceListRepository priceListRepository,
        IClientProductPriceRepository clientProductPriceRepository)
    {
        _productRepository = productRepository;
        _clientRepository = clientRepository;
        _priceListRepository = priceListRepository;
        _clientProductPriceRepository = clientProductPriceRepository;
    }

    public async Task<Result<PriceResolution>> ResolveUnitPriceAsync(
        Guid? clientId,
        Guid productId,
        decimal quantity,
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetByIdAsync(productId, cancellationToken);
        if (product is null)
            return Result.Failure<PriceResolution>(Error.NotFound("Product", productId));

        // Sans client identifié (vente comptoir anonyme), seul le catalogue s'applique.
        if (clientId is { } id && id != Guid.Empty)
        {
            // 1) Prix négocié client / produit — le cas particulier prioritaire.
            var negotiated = await _clientProductPriceRepository
                .GetForClientProductAsync(id, productId, cancellationToken);
            if (negotiated is not null && negotiated.IsApplicableAt(date))
                return Result.Success(new PriceResolution(negotiated.UnitPriceHT, PriceSource.ClientPrice));

            // 2) Grille affectée au client, si applicable et si elle porte le produit.
            var client = await _clientRepository.GetByIdAsync(id, cancellationToken);
            if (client?.PriceListId is { } priceListId)
            {
                var priceList = await _priceListRepository
                    .GetByIdWithItemsAsync(priceListId, cancellationToken);
                if (priceList is not null && priceList.IsApplicableAt(date))
                {
                    var listPrice = priceList.TryGetUnitPrice(productId);
                    if (listPrice is not null)
                        return Result.Success(new PriceResolution(listPrice, PriceSource.PriceList));
                }
            }
        }

        // 3) Repli catalogue.
        return Result.Success(new PriceResolution(product.UnitPrice, PriceSource.Catalog));
    }
}
