using FactuTrust.Domain.Entities.Studio;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface ICustomEntityRepository
{
    Task<CustomEntityDefinition?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);
    Task<CustomEntityDefinition?> GetByKeyAsync(Guid tenantId, string key, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomEntityDefinition>> ListAsync(Guid tenantId, bool includeInactive, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomEntityDefinition>> ListBySystemIdAsync(Guid tenantId, Guid systemId, CancellationToken cancellationToken = default);
    /// <summary>
    /// Whether <paramref name="key"/> is taken in the tenant. Repository reads are filtered on
    /// <c>!IsDeleted</c>; pass <paramref name="includeDeleted"/> = <see langword="true"/> to also see
    /// soft-deleted definitions — the unique index <c>IX_CustomEntityDefinitions_TenantId_Key</c> is NOT
    /// filtered, so a soft-deleted key can never be re-inserted (PR 2.1 review: junction key resolution).
    /// </summary>
    Task<bool> KeyExistsAsync(Guid tenantId, string key, bool includeDeleted = false, CancellationToken cancellationToken = default);
    Task<int> CountAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task AddAsync(CustomEntityDefinition entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(CustomEntityDefinition entity, CancellationToken cancellationToken = default);
}
