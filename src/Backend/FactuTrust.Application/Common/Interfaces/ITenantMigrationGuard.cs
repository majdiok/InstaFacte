using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// Ensures tenant migrations are applied before running tenant-scoped operations.
/// </summary>
public interface ITenantMigrationGuard
{
    /// <summary>
    /// Ensures migrations are applied for the given tenant.
    /// </summary>
    Task<Result> EnsureMigrationsAppliedAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
