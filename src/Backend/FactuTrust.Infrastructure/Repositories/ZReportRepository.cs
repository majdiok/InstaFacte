using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class ZReportRepository : IZReportRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public ZReportRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<ZReport?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.ZReports.FirstOrDefaultAsync(z => z.Id == id, cancellationToken);
    }

    public async Task<ZReport?> GetBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.ZReports.FirstOrDefaultAsync(z => z.CashRegisterSessionId == sessionId, cancellationToken);
    }

    public async Task<IReadOnlyList<ZReport>> ListAsync(
        DateTime fromUtc,
        DateTime toUtc,
        Guid? cashRegisterId,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var query = context.ZReports.AsQueryable()
            .Where(z => z.GeneratedAt >= fromUtc && z.GeneratedAt <= toUtc);

        if (cashRegisterId.HasValue)
        {
            var sessionIds = context.CashRegisterSessions
                .Where(s => s.CashRegisterId == cashRegisterId.Value)
                .Select(s => s.Id);
            query = query.Where(z => sessionIds.Contains(z.CashRegisterSessionId));
        }

        return await query
            .OrderByDescending(z => z.GeneratedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ZReport>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.ZReports.OrderByDescending(z => z.GeneratedAt).ToListAsync(cancellationToken);
    }

    public async Task<ZReport> AddAsync(ZReport entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.ZReports.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(ZReport entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.ZReports.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(ZReport entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.ZReports.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.ZReports.AnyAsync(z => z.Id == id, cancellationToken);
    }
}
