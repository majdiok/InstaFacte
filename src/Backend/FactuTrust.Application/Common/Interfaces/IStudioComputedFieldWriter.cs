using FactuTrust.Domain.Entities.Studio;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// Applies "compute-on-write" custom fields to a record's canonical JSON: values that are derived,
/// not entered by the user (currently AutoNumber; formulas in a later phase). Runs in the record
/// Create/Update handlers after validation and before persistence. The returned JSON replaces the
/// validated JSON. Best-effort and additive: entities without any computed field are returned unchanged.
/// </summary>
public interface IStudioComputedFieldWriter
{
    /// <summary>On creation: allocates AutoNumber values that are not yet present.</summary>
    Task<string> ApplyOnCreateAsync(
        Guid tenantId, Guid entityDefinitionId, IReadOnlyList<CustomFieldDefinition> fields,
        string canonicalJson, CancellationToken cancellationToken = default);

    /// <summary>
    /// On update: preserves the existing (immutable) AutoNumber value, or allocates one if the record
    /// predates the field. <paramref name="existingJson"/> is the record's current persisted JSON.
    /// </summary>
    Task<string> ApplyOnUpdateAsync(
        Guid tenantId, Guid entityDefinitionId, IReadOnlyList<CustomFieldDefinition> fields,
        string canonicalJson, string existingJson, CancellationToken cancellationToken = default);
}
