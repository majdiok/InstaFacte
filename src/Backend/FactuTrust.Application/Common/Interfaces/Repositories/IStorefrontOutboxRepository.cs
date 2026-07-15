using FactuTrust.Domain.Entities.Storefront;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

/// <summary>
/// Tenant-side outbox used by the Storefront module. Every change to a tenant aggregate that
/// impacts the public projection (Master DB) is enqueued here in the same transaction as the
/// business change, then consumed asynchronously by <c>StorefrontProjectionSyncService</c>.
/// </summary>
/// <remarks>
/// The outbox table lives in the tenant database. The repository therefore operates on the
/// caller's current <c>TenantDbContext</c> scope and MUST be called before <c>SaveChangesAsync</c>
/// so that both writes share the same EF Core transaction/execution strategy.
/// </remarks>
public interface IStorefrontOutboxRepository
{
    /// <summary>
    /// Enqueues a message to be emitted after the next <c>SaveChangesAsync</c>. The payload is
    /// expected to already be a sanitized, public-friendly JSON object.
    /// </summary>
    Task EnqueueAsync(
        string aggregateType,
        Guid aggregateId,
        int sourceVersion,
        string eventType,
        string payloadJson,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns up to <paramref name="batchSize"/> pending outbox messages in FIFO order.
    /// Used by the projection sync service.
    /// </summary>
    Task<IReadOnlyList<StorefrontOutboxMessage>> GetPendingAsync(int batchSize, CancellationToken cancellationToken = default);

    Task MarkProcessedAsync(Guid messageId, CancellationToken cancellationToken = default);

    Task RecordAttemptFailureAsync(Guid messageId, string error, CancellationToken cancellationToken = default);
}
