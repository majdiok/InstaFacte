using FactuTrust.Domain.Entities.Studio;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface ICustomViewRepository
{
    Task<IReadOnlyList<CustomViewDefinition>> ListAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<CustomViewDefinition?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);
    Task<bool> KeyExistsAsync(Guid tenantId, string key, CancellationToken cancellationToken = default);
    Task AddAsync(CustomViewDefinition view, CancellationToken cancellationToken = default);
    Task UpdateAsync(CustomViewDefinition view, CancellationToken cancellationToken = default);
}
