using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class PayrollMealVoucherLineRepository : IPayrollMealVoucherLineRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public PayrollMealVoucherLineRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<PayrollMealVoucherLine?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PayrollMealVoucherLines.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<PayrollMealVoucherLine>> ListForMonthAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PayrollMealVoucherLines
            .AsNoTracking()
            .Where(l => l.Year == year && l.Month == month)
            .OrderBy(l => l.EmployeeId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PayrollMealVoucherLine>> ListByEmployeeAndMonthAsync(
        Guid employeeId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PayrollMealVoucherLines
            .AsNoTracking()
            .Where(l => l.EmployeeId == employeeId && l.Year == year && l.Month == month)
            .ToListAsync(cancellationToken);
    }

    public async Task<PayrollMealVoucherLine> AddAsync(PayrollMealVoucherLine entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.PayrollMealVoucherLines.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(PayrollMealVoucherLine entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.PayrollMealVoucherLines.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(PayrollMealVoucherLine entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.PayrollMealVoucherLines.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }
}
