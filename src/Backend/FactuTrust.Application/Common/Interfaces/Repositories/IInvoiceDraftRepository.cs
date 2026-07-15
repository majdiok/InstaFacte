using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

/// <summary>
/// Repository interface for invoice drafts.
/// </summary>
public interface IInvoiceDraftRepository
{
    /// <summary>
    /// Gets a draft by ID.
    /// </summary>
    Task<InvoiceDraft?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new draft.
    /// </summary>
    Task<InvoiceDraft> AddAsync(InvoiceDraft entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing draft.
    /// </summary>
    Task UpdateAsync(InvoiceDraft entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a draft (hard delete for non-converted drafts).
    /// </summary>
    Task DeleteAsync(InvoiceDraft entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets paginated list of user's drafts.
    /// </summary>
    Task<(IReadOnlyList<InvoiceDraft> Items, int TotalCount)> GetUserDraftsAsync(
        Guid userId,
        int page,
        int pageSize,
        bool includeExpired,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets expired drafts for cleanup.
    /// </summary>
    Task<IReadOnlyList<InvoiceDraft>> GetExpiredDraftsAsync(
        int batchSize = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a draft exists.
    /// </summary>
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets draft by idempotency key.
    /// </summary>
    Task<InvoiceDraft?> GetByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}
