using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface IJournalEntryTemplateRepository
{
    Task<JournalEntryTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JournalEntryTemplate>> GetAllAsync(
        bool? activeOnly = null,
        string? search = null,
        CancellationToken cancellationToken = default);
    Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task<JournalEntryTemplate> AddAsync(JournalEntryTemplate entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(JournalEntryTemplate entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(JournalEntryTemplate entity, CancellationToken cancellationToken = default);
    Task IncrementUsageAsync(Guid id, CancellationToken cancellationToken = default);
}
