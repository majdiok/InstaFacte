using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

/// <summary>Catalogue des devises et de leurs taux de change, par exercice.</summary>
public interface ICurrencyRepository
{
    Task<IReadOnlyList<Currency>> GetAllAsync(bool includeInactive, CancellationToken cancellationToken = default);
    Task<Currency?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Currency?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>Devise de tenue des comptes. Semée par la migration, donc toujours présente.</summary>
    Task<Currency?> GetFunctionalAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Currency currency, CancellationToken cancellationToken = default);
    Task UpdateAsync(Currency currency, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CurrencyExchangeRate>> GetRatesAsync(Guid currencyId, int fiscalYear, CancellationToken cancellationToken = default);

    /// <summary>Nombre de taux saisis par devise pour un exercice, pour l'état « configuré / non configuré » de la liste.</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountRatesByCurrencyAsync(int fiscalYear, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remplace intégralement la table de taux d'une devise pour un exercice. Les taux absents de
    /// <paramref name="rates"/> sont supprimés : l'écran d'édition envoie toujours la grille complète.
    /// </summary>
    Task ReplaceRatesAsync(Guid currencyId, int fiscalYear, IReadOnlyList<CurrencyExchangeRate> rates, CancellationToken cancellationToken = default);

    /// <summary>Vrai si au moins une écriture référence ce code devise : la devise n'est alors plus supprimable.</summary>
    Task<bool> IsUsedByJournalEntriesAsync(string code, CancellationToken cancellationToken = default);
}
