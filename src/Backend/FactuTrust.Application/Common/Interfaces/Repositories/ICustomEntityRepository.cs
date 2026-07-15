using FactuTrust.Domain.Entities.Studio;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface ICustomEntityRepository
{
    Task<CustomEntityDefinition?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);
    Task<CustomEntityDefinition?> GetByKeyAsync(Guid tenantId, string key, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomEntityDefinition>> ListAsync(Guid tenantId, bool includeInactive, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomEntityDefinition>> ListBySystemIdAsync(Guid tenantId, Guid systemId, CancellationToken cancellationToken = default);
    Task<bool> KeyExistsAsync(Guid tenantId, string key, CancellationToken cancellationToken = default);
    Task<int> CountAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task AddAsync(CustomEntityDefinition entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(CustomEntityDefinition entity, CancellationToken cancellationToken = default);
}
