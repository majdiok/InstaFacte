using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

/// <summary>Catalogue des journaux et familles de journaux (déblocage des journaux figés).</summary>
public interface IJournalRepository
{
    Task<IReadOnlyList<Journal>> GetAllJournalsAsync(bool includeInactive, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JournalFamily>> GetAllFamiliesAsync(CancellationToken cancellationToken = default);
    Task<Journal?> GetJournalByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Journal?> GetJournalByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<bool> ExistsActiveJournalCodeAsync(string code, CancellationToken cancellationToken = default);
    Task AddJournalAsync(Journal journal, CancellationToken cancellationToken = default);
    Task UpdateJournalAsync(Journal journal, CancellationToken cancellationToken = default);
    Task AddFamilyAsync(JournalFamily family, CancellationToken cancellationToken = default);
}
