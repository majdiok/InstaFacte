using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Domain.Entities.Studio;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// Resolves "compute-on-read" custom fields (Lookup, Rollup) and injects their values into record DTOs
/// being returned from a list/get. Values are computed fresh from related/child data and never stored,
/// so they always reflect the current state. Best-effort: a field that cannot be resolved is simply absent.
/// </summary>
public interface IStudioComputedFieldReader
{
    /// <summary>Enriches the given record DTOs in place with Lookup/Rollup values (no-op if the entity has none).</summary>
    Task EnrichAsync(
        Guid tenantId, IReadOnlyList<CustomFieldDefinition> fields,
        IReadOnlyList<CustomRecordDto> records, CancellationToken cancellationToken = default);
}
