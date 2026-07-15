namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Tenant-side transactional write operations for the Storefront module.
/// Guarantees that the business-state change and the outbox message are persisted
/// within a single SQL transaction (wrapped in SQL Server's execution strategy).
/// </summary>
/// <remarks>
/// <para>
/// This abstraction exists because the existing repository pattern in the codebase creates a new
/// <c>TenantDbContext</c> for every operation, which would split the product update and outbox
/// insert into two independent transactions. Splitting them would leave the projection out of sync
/// when a write succeeds and the next write fails.
/// </para>
/// <para>
/// The implementation lives in <c>FactuTrust.Infrastructure</c>; it takes a lambda that operates on
/// a <c>TenantDbContext</c> and is executed inside <c>ExecuteInTransactionAsync</c>. Keeping this
/// abstraction in Application preserves Clean Architecture (the handler only depends on the interface).
/// </para>
/// </remarks>
public interface IStorefrontTenantWriter
{
    /// <summary>
    /// Flips the <c>IsPubliclyListed</c> flag on a tenant product and enqueues the matching
    /// outbox message in the same SQL transaction.
    /// </summary>
    /// <param name="productId">Target product ID.</param>
    /// <param name="isPubliclyListed">Desired visibility state.</param>
    /// <param name="categoryLabelResolver">
    /// Lambda invoked ONLY when <paramref name="isPubliclyListed"/> is <c>true</c> to resolve the
    /// category label (best-effort, may return <c>null</c>). Resolving the category outside the
    /// transaction keeps the transaction small.
    /// </param>
    /// <param name="storefrontProfileId">The tenant's storefront profile id (required for payload routing).</param>
    /// <param name="tenantId">The tenant id.</param>
    /// <returns>
    /// A <see cref="StorefrontProductVisibilityResult"/> describing the outcome of the operation.
    /// </returns>
    Task<StorefrontProductVisibilityResult> SetProductPublicVisibilityAsync(
        Guid productId,
        bool isPubliclyListed,
        Func<Guid, CancellationToken, Task<string?>> categoryLabelResolver,
        Guid storefrontProfileId,
        Guid tenantId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of <see cref="IStorefrontTenantWriter.SetProductPublicVisibilityAsync"/>.
/// </summary>
public sealed record StorefrontProductVisibilityResult
{
    public bool ProductFound { get; init; }
    public bool Changed { get; init; }
    public bool ProductIsActive { get; init; }

    public static StorefrontProductVisibilityResult NotFound() =>
        new() { ProductFound = false, Changed = false, ProductIsActive = false };

    public static StorefrontProductVisibilityResult Unchanged(bool isActive) =>
        new() { ProductFound = true, Changed = false, ProductIsActive = isActive };

    public static StorefrontProductVisibilityResult Applied(bool isActive) =>
        new() { ProductFound = true, Changed = true, ProductIsActive = isActive };
}
