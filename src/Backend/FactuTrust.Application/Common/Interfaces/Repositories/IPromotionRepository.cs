using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Entities.Pricing;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

/// <summary>Repository des promotions datées (<see cref="Promotion"/>).</summary>
public interface IPromotionRepository : IRepository<Promotion>
{
    /// <summary>
    /// Promotions qui COURENT à cette date (actives et dans leur fenêtre). Le filtrage par cible
    /// se fait en mémoire : elles sont peu nombreuses, et les règles de ciblage (cible nulle =
    /// tous, catégorie, spécificité) sont portées par le domaine, pas par SQL.
    /// </summary>
    Task<IReadOnlyList<Promotion>> GetRunningAtAsync(DateTime date, CancellationToken cancellationToken = default);

    /// <summary>Toutes les promotions, pour l'administration.</summary>
    Task<IReadOnlyList<Promotion>> GetAllOrderedAsync(CancellationToken cancellationToken = default);
}
