using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class ChartOfAccountRepository : IChartOfAccountRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public ChartOfAccountRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.ChartOfAccounts.CountAsync(cancellationToken);
    }

    public async Task<ChartOfAccount?> GetByAccountNumberAsync(string accountNumber, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var n = accountNumber.Trim();
        return await context.ChartOfAccounts.AsNoTracking()
            .FirstOrDefaultAsync(c => c.AccountNumber == n, cancellationToken);
    }

    public async Task<IReadOnlyList<ChartOfAccount>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.ChartOfAccounts.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.AccountNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<ChartOfAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.ChartOfAccounts.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<ChartOfAccount> AddAsync(ChartOfAccount entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.ChartOfAccounts.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(ChartOfAccount entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.ChartOfAccounts.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }
}
