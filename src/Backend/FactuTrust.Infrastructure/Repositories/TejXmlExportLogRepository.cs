using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class TejXmlExportLogRepository : ITejXmlExportLogRepository
{
    private readonly TenantDbContextFactory _contextFactory;

    public TejXmlExportLogRepository(TenantDbContextFactory contextFactory) => _contextFactory = contextFactory;

    public async Task AddAsync(TejXmlExportLog log, CancellationToken ct = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.TejXmlExportLogs.Add(log);
        await context.SaveChangesAsync(ct);
    }

    public async Task<List<TejXmlExportLog>> GetRecentAsync(int take = 50, CancellationToken ct = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.TejXmlExportLogs
            .AsNoTracking()
            .OrderByDescending(e => e.CreatedAt)
            .Take(take)
            .ToListAsync(ct);
    }
}
