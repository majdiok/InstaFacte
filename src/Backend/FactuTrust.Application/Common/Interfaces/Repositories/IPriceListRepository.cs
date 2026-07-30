using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Entities.Pricing;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

/// <summary>
/// Repository de la grille tarifaire (<see cref="PriceList"/>) et de ses lignes.
/// </summary>
public interface IPriceListRepository : IRepository<PriceList>
{
    /// <summary>Charge une grille avec ses lignes de prix.</summary>
    Task<PriceList?> GetByIdWithItemsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Toutes les grilles (sans les lignes), pour l'administration.</summary>
    Task<IReadOnlyList<PriceList>> GetAllSummaryAsync(CancellationToken cancellationToken = default);
}
