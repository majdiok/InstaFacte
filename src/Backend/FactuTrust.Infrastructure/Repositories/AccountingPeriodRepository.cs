using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class AccountingPeriodRepository : IAccountingPeriodRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public AccountingPeriodRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<AccountingPeriod?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.AccountingPeriods.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<AccountingPeriod?> GetByYearMonthAsync(int fiscalYear, int month, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.AccountingPeriods
            .FirstOrDefaultAsync(p => p.FiscalYear == fiscalYear && p.Month == month, cancellationToken);
    }

    public async Task<IReadOnlyList<AccountingPeriod>> GetByFiscalYearAsync(int fiscalYear, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.AccountingPeriods
            .Where(p => p.FiscalYear == fiscalYear)
            .OrderBy(p => p.Month)
            .ToListAsync(cancellationToken);
    }

    public async Task<AccountingPeriod> AddAsync(AccountingPeriod entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.AccountingPeriods.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(AccountingPeriod entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.AccountingPeriods.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }
}
