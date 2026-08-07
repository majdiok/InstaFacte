using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class EmployeeAnnualBonusRuleRepository : IEmployeeAnnualBonusRuleRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public EmployeeAnnualBonusRuleRepository(ITenantDbContextFactory contextFactory) => _contextFactory = contextFactory;

    public async Task<EmployeeAnnualBonusRule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.EmployeeAnnualBonusRules.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<EmployeeAnnualBonusRule?> GetByEmployeeAndRuleAsync(
        Guid employeeId, Guid ruleId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.EmployeeAnnualBonusRules.FirstOrDefaultAsync(
            a => a.EmployeeId == employeeId && a.AnnualBonusRuleId == ruleId, cancellationToken);
    }

    public async Task<IReadOnlyList<EmployeeAnnualBonusRule>> ListActiveAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.EmployeeAnnualBonusRules
            .AsNoTracking()
            .Where(a => a.IsActive)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EmployeeAnnualBonusRule>> ListByEmployeeAsync(
        Guid employeeId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.EmployeeAnnualBonusRules
            .AsNoTracking()
            .Where(a => a.EmployeeId == employeeId)
            .ToListAsync(cancellationToken);
    }

    public async Task<EmployeeAnnualBonusRule> AddAsync(EmployeeAnnualBonusRule entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.EmployeeAnnualBonusRules.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(EmployeeAnnualBonusRule entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.EmployeeAnnualBonusRules.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(EmployeeAnnualBonusRule entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.EmployeeAnnualBonusRules.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }
}
