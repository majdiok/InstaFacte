using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities.AI;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

/// <summary>
/// Persistence contract for AI export audit rows. Stored in the tenant database for compliance
/// and analytics. Read-only methods are scoped to the calling user.
/// </summary>
public interface IAiExportAuditRepository
{
    Task AddAsync(AiExportAudit audit, CancellationToken cancellationToken = default);

    Task<PagedResult<AiExportAudit>> GetByUserAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
