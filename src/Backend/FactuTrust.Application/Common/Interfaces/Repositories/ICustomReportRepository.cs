using FactuTrust.Domain.Entities.Studio;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface ICustomReportRepository
{
    Task<IReadOnlyList<CustomReportDefinition>> ListAsync(Guid tenantId, string? dataSourceRef, CancellationToken cancellationToken = default);
    Task<CustomReportDefinition?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);
    Task<bool> KeyExistsAsync(Guid tenantId, string key, CancellationToken cancellationToken = default);
    Task AddAsync(CustomReportDefinition report, CancellationToken cancellationToken = default);
    Task UpdateAsync(CustomReportDefinition report, CancellationToken cancellationToken = default);
}
