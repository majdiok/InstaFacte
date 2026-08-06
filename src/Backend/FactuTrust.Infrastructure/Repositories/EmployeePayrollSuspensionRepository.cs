using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class EmployeePayrollSuspensionRepository : IEmployeePayrollSuspensionRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public EmployeePayrollSuspensionRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<EmployeePayrollSuspension?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.EmployeePayrollSuspensions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<EmployeePayrollSuspension>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.EmployeePayrollSuspensions
            .OrderByDescending(s => s.StartDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EmployeePayrollSuspension>> ListByEmployeeAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.EmployeePayrollSuspensions
            .Where(s => s.EmployeeId == employeeId)
            .OrderByDescending(s => s.StartDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EmployeePayrollSuspension>> ListForMonthAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        var monthStart = new DateTime(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        await using var context = _contextFactory.CreateContext();
        return await context.EmployeePayrollSuspensions
            .AsNoTracking()
            .Where(s => s.IsApproved && s.StartDate <= monthEnd && (s.EndDate == null || s.EndDate >= monthStart))
            .ToListAsync(cancellationToken);
    }

    public async Task<EmployeePayrollSuspension> AddAsync(
        EmployeePayrollSuspension entity,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.EmployeePayrollSuspensions.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(EmployeePayrollSuspension entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.EmployeePayrollSuspensions.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(EmployeePayrollSuspension entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.EmployeePayrollSuspensions.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.EmployeePayrollSuspensions.AnyAsync(s => s.Id == id, cancellationToken);
    }
}
