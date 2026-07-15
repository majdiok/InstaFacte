using FactuTrust.Domain.Entities.Studio;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface ICustomFormRepository
{
    /// <summary>The default form for an entity (one per entity), or null if none saved yet.</summary>
    Task<CustomFormDefinition?> GetDefaultByEntityAsync(Guid tenantId, Guid entityDefinitionId, CancellationToken cancellationToken = default);
    Task AddAsync(CustomFormDefinition form, CancellationToken cancellationToken = default);
    Task UpdateAsync(CustomFormDefinition form, CancellationToken cancellationToken = default);
}
