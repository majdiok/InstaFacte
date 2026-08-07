using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class PayrollVariableAllowanceRepository : IPayrollVariableAllowanceRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public PayrollVariableAllowanceRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<PayrollVariableAllowanceLine?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PayrollVariableAllowanceLines.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<PayrollVariableAllowanceLine>> ListForMonthAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PayrollVariableAllowanceLines
            .AsNoTracking()
            .Where(l => l.Year == year && l.Month == month)
            .OrderBy(l => l.EmployeeId)
            .ThenBy(l => l.Label)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PayrollVariableAllowanceLine>> ListByEmployeeAndMonthAsync(
        Guid employeeId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PayrollVariableAllowanceLines
            .AsNoTracking()
            .Where(l => l.EmployeeId == employeeId && l.Year == year && l.Month == month)
            .OrderBy(l => l.Label)
            .ToListAsync(cancellationToken);
    }

    public async Task<PayrollVariableAllowanceLine> AddAsync(PayrollVariableAllowanceLine entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.PayrollVariableAllowanceLines.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(PayrollVariableAllowanceLine entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.PayrollVariableAllowanceLines.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(PayrollVariableAllowanceLine entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.PayrollVariableAllowanceLines.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<PayrollVariableAllowanceLine?> GetAutoAnnualBonusLineAsync(
        Guid employeeId, int year, int month, Guid annualBonusRuleId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PayrollVariableAllowanceLines.FirstOrDefaultAsync(
            l => l.EmployeeId == employeeId && l.Year == year && l.Month == month
                 && l.AnnualBonusRuleId == annualBonusRuleId
                 && l.Source == Domain.Enums.VariableAllowanceSource.AutoAnnualBonus,
            cancellationToken);
    }

    public async Task<IReadOnlyList<PayrollVariableAllowanceLine>> ListAutoAnnualBonusForMonthAsync(
        int year, int month, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PayrollVariableAllowanceLines
            .Where(l => l.Year == year && l.Month == month
                        && l.Source == Domain.Enums.VariableAllowanceSource.AutoAnnualBonus)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveBatchAsync(
        IReadOnlyList<PayrollVariableAllowanceLine> added,
        IReadOnlyList<PayrollVariableAllowanceLine> updated,
        CancellationToken cancellationToken = default)
    {
        if (added.Count == 0 && updated.Count == 0)
            return;

        await using var context = _contextFactory.CreateContext();
        if (added.Count > 0)
            context.PayrollVariableAllowanceLines.AddRange(added);
        foreach (var entity in updated)
            context.PayrollVariableAllowanceLines.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }
}
