using FactuTrust.Domain.Entities.Studio;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface ICustomRecordRepository
{
    Task<CustomRecord?> GetAsync(Guid tenantId, Guid entityDefinitionId, Guid id, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<CustomRecord> Items, int TotalCount)> ListAsync(
        Guid tenantId,
        Guid entityDefinitionId,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(Guid tenantId, Guid entityDefinitionId, CancellationToken cancellationToken = default);

    /// <summary>All active records for an entity, capped, for in-memory report execution.</summary>
    Task<IReadOnlyList<CustomRecord>> GetAllForReportAsync(Guid tenantId, Guid entityDefinitionId, int max, CancellationToken cancellationToken = default);

    /// <summary>True if another active record has <paramref name="value"/> at <paramref name="fieldKey"/> (for unique-field enforcement).</summary>
    Task<bool> ExistsWithFieldValueAsync(Guid tenantId, Guid entityDefinitionId, string fieldKey, string value, Guid? excludeId, CancellationToken cancellationToken = default);

    Task AddAsync(CustomRecord record, CancellationToken cancellationToken = default);
    Task UpdateAsync(CustomRecord record, CancellationToken cancellationToken = default);

    /// <summary>Update using the client's RowVersion as the concurrency token (mismatch → DbUpdateConcurrencyException → 409).</summary>
    Task UpdateWithConcurrencyAsync(CustomRecord record, byte[]? expectedRowVersion, CancellationToken cancellationToken = default);
}
