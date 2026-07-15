namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// Atomically reserves the next value of a per-tenant AutoNumber sequence (one counter per
/// entity + field key). Backed by a Serializable transaction + execution strategy, mirroring the
/// invoice document-numbering allocator. Concurrency-safe: two callers never get the same value.
/// </summary>
public interface ICustomSequenceAllocator
{
    /// <summary>Reserves and returns the next 1-based sequence value, creating the counter on first use.</summary>
    Task<long> ReserveNextAsync(Guid tenantId, Guid entityDefinitionId, string fieldKey, CancellationToken cancellationToken = default);
}
