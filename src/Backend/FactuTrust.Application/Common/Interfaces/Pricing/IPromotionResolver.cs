using FactuTrust.Domain.Common;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Application.Common.Interfaces.Pricing;

/// <summary>
/// Promotion retenue pour une ligne, et la remise qu'elle produit.
/// </summary>
/// <param name="PromotionId">Promotion gagnante, pour la traçabilité.</param>
/// <param name="PromotionName">Libellé, affiché à l'écran et sur le document.</param>
/// <param name="DiscountPercent">
/// Remise ramenée en pourcentage de la ligne. Les promotions en montant sont converties ici,
/// de sorte que la ligne n'a qu'un seul mode de remise et que le moteur de calcul
/// (remise → FODEC → TVA) reste inchangé.
/// </param>
public sealed record PromotionResolution(Guid PromotionId, string PromotionName, decimal DiscountPercent);

/// <summary>
/// Point de résolution UNIQUE des promotions : « quelle promotion s'applique à ce produit, ce
/// client, cette quantité, cette date ».
///
/// <b>Distinct du prix.</b> Une promotion ne remplace pas le prix — c'est le rôle des grilles
/// (<see cref="IPriceResolver"/>). Elle vient APRÈS, sous forme de remise de ligne. La remise
/// ainsi obtenue est figée sur la ligne : la fin d'une promotion ne change pas un document émis.
///
/// <b>Elle ne s'impose jamais à une remise saisie.</b> Si le commercial a posé sa propre remise,
/// c'est elle qui vaut — la promotion serait sinon une surprise silencieuse.
/// </summary>
public interface IPromotionResolver
{
    /// <summary>
    /// Promotion applicable, ou <c>null</c> s'il n'y en a aucune. En cas de concurrence, la
    /// priorité la plus élevée gagne ; à égalité, la plus spécifique.
    /// </summary>
    /// <param name="unitPriceHT">
    /// Prix unitaire déjà résolu par <see cref="IPriceResolver"/>. Nécessaire pour convertir une
    /// promotion exprimée en montant vers le pourcentage que porte la ligne — sans lui, une
    /// telle promotion serait silencieusement ramenée à zéro.
    /// </param>
    Task<Result<PromotionResolution?>> ResolveAsync(
        Guid productId,
        Guid? productCategoryId,
        Guid? clientId,
        decimal quantity,
        Money unitPriceHT,
        DateTime date,
        CancellationToken cancellationToken = default);
}
