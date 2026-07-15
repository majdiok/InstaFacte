using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// Resolves tenant users for CRM assignment (master DB, scoped to current tenant).
/// </summary>
public interface ITenantMemberDirectory
{
    /// <summary>
    /// Returns display name for an active user in the current tenant, or failure if not found.
    /// </summary>
    Task<Result<(Guid Id, string DisplayName)>> GetMemberAsync(Guid userId, CancellationToken cancellationToken = default);
}
