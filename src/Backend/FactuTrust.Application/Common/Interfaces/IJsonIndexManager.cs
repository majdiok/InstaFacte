namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// Manages NON-persisted computed-column indexes on the tenant <c>CustomRecords</c> table to
/// accelerate per-field lookups (currently: unique-field checks). All operations are best-effort:
/// indexing is a performance optimization, never a correctness requirement, so failures never
/// propagate to the caller.
/// </summary>
public interface IJsonIndexManager
{
    /// <summary>Ensures the computed column + filtered index exist for a (sanitized) field key. Idempotent, best-effort.</summary>
    Task EnsureUniqueFieldIndexAsync(Guid tenantId, string fieldKey, CancellationToken cancellationToken = default);

    /// <summary>True if the indexed computed column exists for the field key (cached). Used to decide whether to seek the index.</summary>
    Task<bool> IndexedColumnExistsAsync(Guid tenantId, string fieldKey, CancellationToken cancellationToken = default);
}
