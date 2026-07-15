using FactuTrust.Domain.Entities.Studio;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface ICustomSystemRepository
{
    Task<CustomSystemDefinition?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);
    Task<CustomSystemDefinition?> GetByKeyAsync(Guid tenantId, string key, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomSystemDefinition>> ListAsync(Guid tenantId, bool includeInactive, CancellationToken cancellationToken = default);
    Task<bool> KeyExistsAsync(Guid tenantId, string key, CancellationToken cancellationToken = default);
    Task AddAsync(CustomSystemDefinition system, CancellationToken cancellationToken = default);
    Task UpdateAsync(CustomSystemDefinition system, CancellationToken cancellationToken = default);
}
