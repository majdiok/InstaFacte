using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories.Studio;

public sealed class CustomReportRepository : ICustomReportRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public CustomReportRepository(ITenantDbContextFactory contextFactory) => _contextFactory = contextFactory;

    public async Task<IReadOnlyList<CustomReportDefinition>> ListAsync(Guid tenantId, string? dataSourceRef, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var query = context.CustomReportDefinitions.Where(r => r.TenantId == tenantId && r.IsActive);
        if (!string.IsNullOrWhiteSpace(dataSourceRef))
            query = query.Where(r => r.DataSourceRef == dataSourceRef);
        return await query.OrderBy(r => r.DisplayName).ToListAsync(cancellationToken);
    }

    public async Task<CustomReportDefinition?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.CustomReportDefinitions
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == id, cancellationToken);
    }

    public async Task<bool> KeyExistsAsync(Guid tenantId, string key, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.CustomReportDefinitions.AnyAsync(r => r.TenantId == tenantId && r.Key == key, cancellationToken);
    }

    public async Task AddAsync(CustomReportDefinition report, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.CustomReportDefinitions.Add(report);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(CustomReportDefinition report, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.CustomReportDefinitions.Update(report);
        await context.SaveChangesAsync(cancellationToken);
    }
}
