using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface IJournalEntryRepository
{
    /// <summary>
    /// Charge une écriture SUIVIE (avec lignes + période) dans un contexte unique, applique la mutation
    /// domaine puis persiste. Nécessaire pour les mutations qui reconfigurent les lignes (ex. édition de
    /// brouillon) : le suivi de modifications supprime correctement les anciennes lignes et insère les
    /// nouvelles. Retourne NotFound si l'écriture n'existe pas, sinon le résultat de la mutation.
    /// </summary>
    Task<Result> MutateAsync(Guid id, Func<JournalEntry, Result> mutate, CancellationToken cancellationToken = default);
    Task<JournalEntry?> GetBySourceAsync(string sourceEntityType, Guid sourceEntityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Comme <see cref="GetBySourceAsync"/>, mais ignore les écritures extournées et rend la plus
    /// récente. Indispensable dès qu'une même source peut porter plusieurs écritures successives
    /// (cycle de paie rouvert puis revalidé) : <see cref="GetBySourceAsync"/> est un
    /// <c>FirstOrDefault</c> sans tri et deviendrait alors non déterministe.
    /// </summary>
    Task<JournalEntry?> GetActiveBySourceAsync(string sourceEntityType, Guid sourceEntityId, CancellationToken cancellationToken = default);
    Task<JournalEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JournalEntry>> GetDraftsByPeriodAsync(Guid periodId, string? journalCode, CancellationToken cancellationToken = default);
    Task<int> CountDraftsAsync(CancellationToken cancellationToken = default);
    Task<int> CountDraftsByFiscalYearAsync(int fiscalYear, CancellationToken cancellationToken = default);
    Task<JournalEntry> AddAsync(JournalEntry entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(JournalEntry entity, CancellationToken cancellationToken = default);
    Task RemoveAsync(JournalEntry entity, CancellationToken cancellationToken = default);
    Task<int> ReserveNextEntryNumberAsync(string journalCode, int fiscalYear, CancellationToken cancellationToken = default);
    Task<decimal> SumDebitsByAccountAsync(string accountNumber, DateTime from, DateTime to, CancellationToken cancellationToken = default);
}
