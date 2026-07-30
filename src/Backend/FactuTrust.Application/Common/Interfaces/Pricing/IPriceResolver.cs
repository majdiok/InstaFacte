using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Application.Common.Interfaces.Pricing;

/// <summary>
/// Prix HT retenu pour une ligne, et la raison de ce choix.
/// </summary>
/// <param name="UnitPriceHT">Prix unitaire HT applicable.</param>
/// <param name="Source">Origine du prix (prix négocié, grille, ou catalogue).</param>
public sealed record PriceResolution(Money UnitPriceHT, PriceSource Source);

/// <summary>
/// Point de résolution UNIQUE du prix : « quel prix HT pour ce client, ce produit, cette
/// quantité, cette date ». Tous les documents (devis, commande, BL, facture, POS) doivent
/// l'appeler — aucun ne doit résoudre un prix ailleurs, sous peine de divergences.
///
/// Priorité décroissante : prix négocié client → grille affectée au client → prix catalogue.
/// Le prix ainsi obtenu est destiné à être <b>figé</b> sur la ligne au moment de sa création :
/// un document déjà émis ne doit jamais changer de prix parce qu'une grille a bougé.
/// </summary>
public interface IPriceResolver
{
    /// <summary>
    /// Résout le prix unitaire HT. <paramref name="clientId"/> peut être <c>null</c> (vente
    /// comptoir sans client identifié) : on retombe alors directement sur le catalogue.
    /// La <paramref name="quantity"/> est déjà prise en compte (paliers en tranche 5B) ; le
    /// <paramref name="date"/> est la date du document, qui décide de la validité des grilles.
    /// </summary>
    Task<Result<PriceResolution>> ResolveUnitPriceAsync(
        Guid? clientId,
        Guid productId,
        decimal quantity,
        DateTime date,
        CancellationToken cancellationToken = default);
}
