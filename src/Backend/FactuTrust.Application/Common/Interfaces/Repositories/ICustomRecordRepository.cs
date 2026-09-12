using FactuTrust.Domain.Entities.Studio;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface ICustomRecordRepository
{
    Task<CustomRecord?> GetAsync(Guid tenantId, Guid entityDefinitionId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Paged list of active records. <paramref name="search"/> is the historical LIKE over the JSON
    /// document; <paramref name="filterField"/>/<paramref name="filterValue"/> (PR 2.1) add an exact,
    /// parameterised <c>JSON_VALUE(DataJson,'$.&lt;key&gt;') = @v</c> predicate (index seek on
    /// <c>jx_&lt;key&gt;</c> when the computed column exists) — both are cumulative. The caller validates
    /// the field key; a key that fails <c>StudioKey.IsValidShape</c> yields an empty page (never a scan).
    /// </summary>
    Task<(IReadOnlyList<CustomRecord> Items, int TotalCount)> ListAsync(
        Guid tenantId,
        Guid entityDefinitionId,
        string? search,
        int page,
        int pageSize,
        string? filterField = null,
        string? filterValue = null,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(Guid tenantId, Guid entityDefinitionId, CancellationToken cancellationToken = default);

    /// <summary>All active records for an entity, capped, for in-memory report execution.</summary>
    Task<IReadOnlyList<CustomRecord>> GetAllForReportAsync(Guid tenantId, Guid entityDefinitionId, int max, CancellationToken cancellationToken = default);

    /// <summary>True if another active record has <paramref name="value"/> at <paramref name="fieldKey"/> (for unique-field enforcement).</summary>
    Task<bool> ExistsWithFieldValueAsync(Guid tenantId, Guid entityDefinitionId, string fieldKey, string value, Guid? excludeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// True si un enregistrement actif porte déjà (<paramref name="fieldKeyA"/> = <paramref name="valueA"/>
    /// AND <paramref name="fieldKeyB"/> = <paramref name="valueB"/>) — unicité d'une paire de jonction
    /// (PR 2.1). <paramref name="excludeId"/> exclut l'enregistrement en cours de mise à jour.
    /// </summary>
    Task<bool> ExistsWithFieldPairAsync(
        Guid tenantId,
        Guid entityDefinitionId,
        string fieldKeyA,
        string valueA,
        string fieldKeyB,
        string valueB,
        Guid? excludeId,
        CancellationToken cancellationToken = default);

    Task AddAsync(CustomRecord record, CancellationToken cancellationToken = default);
    Task UpdateAsync(CustomRecord record, CancellationToken cancellationToken = default);

    /// <summary>Update using the client's RowVersion as the concurrency token (mismatch → DbUpdateConcurrencyException → 409).</summary>
    Task UpdateWithConcurrencyAsync(CustomRecord record, byte[]? expectedRowVersion, CancellationToken cancellationToken = default);
}
