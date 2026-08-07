using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class AnnualBonusRuleRepository : IAnnualBonusRuleRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public AnnualBonusRuleRepository(ITenantDbContextFactory contextFactory) => _contextFactory = contextFactory;

    public async Task<AnnualBonusRule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.AnnualBonusRules.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<AnnualBonusRule?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.AnnualBonusRules.FirstOrDefaultAsync(r => r.Code == code, cancellationToken);
    }

    public async Task<IReadOnlyList<AnnualBonusRule>> ListAsync(int? fiscalYear = null, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var query = context.AnnualBonusRules.AsNoTracking();
        if (fiscalYear.HasValue)
            query = query.Where(r => r.FiscalYear == null || r.FiscalYear == fiscalYear.Value);
        return await query.OrderBy(r => r.Code).ToListAsync(cancellationToken);
    }

    public async Task<AnnualBonusRule> AddAsync(AnnualBonusRule entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.AnnualBonusRules.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(AnnualBonusRule entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.AnnualBonusRules.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(AnnualBonusRule entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.AnnualBonusRules.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }
}
