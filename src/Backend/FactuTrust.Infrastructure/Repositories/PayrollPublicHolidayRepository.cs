using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class PayrollPublicHolidayRepository : IPayrollPublicHolidayRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public PayrollPublicHolidayRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<PayrollPublicHoliday?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PayrollPublicHolidays.FirstOrDefaultAsync(h => h.Id == id, cancellationToken);
    }

    public async Task<bool> ExistsForDateAsync(int year, DateTime date, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var query = context.PayrollPublicHolidays
            .Where(h => h.Year == year && h.Date == date.Date);
        if (excludeId.HasValue)
            query = query.Where(h => h.Id != excludeId.Value);
        return await query.AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PayrollPublicHoliday>> ListByYearAsync(int year, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PayrollPublicHolidays.AsNoTracking()
            .Where(h => h.Year == year)
            .OrderBy(h => h.Date)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PayrollPublicHoliday>> ListForMonthAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        var monthStart = new DateTime(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        await using var context = _contextFactory.CreateContext();
        return await context.PayrollPublicHolidays.AsNoTracking()
            .Where(h => h.Date >= monthStart && h.Date <= monthEnd)
            .OrderBy(h => h.Date)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<int>> ListConfiguredYearsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PayrollPublicHolidays.AsNoTracking()
            .Select(h => h.Year)
            .Distinct()
            .OrderBy(y => y)
            .ToListAsync(cancellationToken);
    }

    public async Task<PayrollPublicHoliday> AddAsync(PayrollPublicHoliday entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.PayrollPublicHolidays.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task AddRangeAsync(IEnumerable<PayrollPublicHoliday> entities, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.PayrollPublicHolidays.AddRange(entities);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(PayrollPublicHoliday entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.PayrollPublicHolidays.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(PayrollPublicHoliday entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.PayrollPublicHolidays.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }
}
