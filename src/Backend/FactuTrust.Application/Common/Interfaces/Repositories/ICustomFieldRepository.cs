using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface ICustomFieldRepository
{
    Task<IReadOnlyList<CustomFieldDefinition>> ListByEntityAsync(Guid tenantId, Guid entityDefinitionId, bool includeInactive, CancellationToken cancellationToken = default);
    /// <summary>
    /// All fields of a given <paramref name="fieldType"/> across every entity of the tenant, in one
    /// query (ordered by entity, then <c>SortOrder</c>, then key). Introduced for the relation resolver
    /// (PR 2.1 review): one read for all <see cref="CustomFieldType.RelationCustom"/> fields instead of
    /// one read per entity.
    /// </summary>
    Task<IReadOnlyList<CustomFieldDefinition>> ListByTypeAsync(Guid tenantId, CustomFieldType fieldType, bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<CustomFieldDefinition?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);
    Task<bool> KeyExistsAsync(Guid tenantId, Guid entityDefinitionId, string key, CancellationToken cancellationToken = default);
    Task<int> CountByEntityAsync(Guid tenantId, Guid entityDefinitionId, CancellationToken cancellationToken = default);
    Task<int> MaxSortOrderAsync(Guid tenantId, Guid entityDefinitionId, CancellationToken cancellationToken = default);
    Task AddAsync(CustomFieldDefinition field, CancellationToken cancellationToken = default);
    Task UpdateAsync(CustomFieldDefinition field, CancellationToken cancellationToken = default);
    Task UpdateRangeAsync(IReadOnlyList<CustomFieldDefinition> fields, CancellationToken cancellationToken = default);
}
