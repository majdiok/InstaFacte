using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities.AI;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

/// <summary>
/// EF Core repository for <see cref="AiExportAudit"/>. Scoped per tenant via
/// <see cref="ITenantDbContextFactory"/>.
/// </summary>
public sealed class AiExportAuditRepository : IAiExportAuditRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public AiExportAuditRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task AddAsync(AiExportAudit audit, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.AiExportAudits.Add(audit);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<AiExportAudit>> GetByUserAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();

        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 100);

        var query = context.AiExportAudits
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.GeneratedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<AiExportAudit>.Create(items, safePage, safePageSize, totalCount);
    }
}
