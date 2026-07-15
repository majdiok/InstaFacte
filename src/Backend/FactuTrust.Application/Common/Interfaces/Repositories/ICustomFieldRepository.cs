using FactuTrust.Domain.Entities.Studio;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface ICustomFieldRepository
{
    Task<IReadOnlyList<CustomFieldDefinition>> ListByEntityAsync(Guid tenantId, Guid entityDefinitionId, bool includeInactive, CancellationToken cancellationToken = default);
    Task<CustomFieldDefinition?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);
    Task<bool> KeyExistsAsync(Guid tenantId, Guid entityDefinitionId, string key, CancellationToken cancellationToken = default);
    Task<int> CountByEntityAsync(Guid tenantId, Guid entityDefinitionId, CancellationToken cancellationToken = default);
    Task<int> MaxSortOrderAsync(Guid tenantId, Guid entityDefinitionId, CancellationToken cancellationToken = default);
    Task AddAsync(CustomFieldDefinition field, CancellationToken cancellationToken = default);
    Task UpdateAsync(CustomFieldDefinition field, CancellationToken cancellationToken = default);
    Task UpdateRangeAsync(IReadOnlyList<CustomFieldDefinition> fields, CancellationToken cancellationToken = default);
}
