using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class SalesTargetRepository : ISalesTargetRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public SalesTargetRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<SalesTarget?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.SalesTargets
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<SalesTarget?> GetByUserYearMonthAsync(
        Guid userId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.SalesTargets
            .AsNoTracking()
            .FirstOrDefaultAsync(
                t => t.UserId == userId && t.Year == year && t.Month == month,
                cancellationToken);
    }

    public async Task<IReadOnlyList<SalesTarget>> GetByYearAsync(
        int year,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var query = context.SalesTargets
            .AsNoTracking()
            .Where(t => t.Year == year);

        if (userId.HasValue)
            query = query.Where(t => t.UserId == userId.Value);

        return await query
            .OrderBy(t => t.Month)
            .ThenBy(t => t.UserName)
            .ToListAsync(cancellationToken);
    }

    public async Task<SalesTarget> AddAsync(SalesTarget entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.SalesTargets.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(SalesTarget entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.SalesTargets.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(SalesTarget entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.SalesTargets.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }
}
