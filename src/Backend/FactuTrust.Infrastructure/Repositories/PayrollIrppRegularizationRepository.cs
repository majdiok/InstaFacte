using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class PayrollIrppRegularizationRepository : IPayrollIrppRegularizationRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public PayrollIrppRegularizationRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<PayrollIrppRegularization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PayrollIrppRegularizations.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<PayrollIrppRegularization?> GetByEmployeeAndMonthAsync(
        Guid employeeId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PayrollIrppRegularizations
            .FirstOrDefaultAsync(
                r => r.EmployeeId == employeeId && r.Year == year && r.Month == month,
                cancellationToken);
    }

    public async Task<IReadOnlyList<PayrollIrppRegularization>> ListForMonthAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PayrollIrppRegularizations
            .AsNoTracking()
            .Where(r => r.Year == year && r.Month == month)
            .OrderBy(r => r.EmployeeId)
            .ToListAsync(cancellationToken);
    }

    public async Task<PayrollIrppRegularization> AddAsync(PayrollIrppRegularization entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.PayrollIrppRegularizations.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(PayrollIrppRegularization entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.PayrollIrppRegularizations.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(PayrollIrppRegularization entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.PayrollIrppRegularizations.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveBatchAsync(
        IReadOnlyList<PayrollIrppRegularization> added,
        IReadOnlyList<PayrollIrppRegularization> updated,
        CancellationToken cancellationToken = default)
    {
        if (added.Count == 0 && updated.Count == 0)
            return;

        await using var context = _contextFactory.CreateContext();

        if (added.Count > 0)
            context.PayrollIrppRegularizations.AddRange(added);

        foreach (var entity in updated)
            context.PayrollIrppRegularizations.Update(entity);

        await context.SaveChangesAsync(cancellationToken);
    }
}
