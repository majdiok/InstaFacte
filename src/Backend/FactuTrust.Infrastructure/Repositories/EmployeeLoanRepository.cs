using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class EmployeeLoanRepository : IEmployeeLoanRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public EmployeeLoanRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<EmployeeLoan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.EmployeeLoans.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
    }

    public async Task<EmployeeLoan?> GetByIdWithInstallmentsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.EmployeeLoans
            .Include(l => l.Installments)
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
    }

    public async Task<EmployeeLoan?> GetByInstallmentIdAsync(Guid installmentId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.EmployeeLoans
            .Include(l => l.Installments)
            .FirstOrDefaultAsync(l => l.Installments.Any(i => i.Id == installmentId), cancellationToken);
    }

    public async Task<IReadOnlyList<EmployeeLoan>> ListByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.EmployeeLoans
            .AsNoTracking()
            .Include(l => l.Installments)
            .Where(l => l.EmployeeId == employeeId)
            .OrderByDescending(l => l.StartYear)
            .ThenByDescending(l => l.StartMonth)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EmployeeLoan>> ListWithDueInstallmentsForMonthAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.EmployeeLoans
            .Include(l => l.Installments)
            .Where(l => l.Installments.Any(i =>
                i.Year == year && i.Month == month && !i.IsSettled))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EmployeeLoan>> ListWithSettledInstallmentsForRunAsync(
        Guid payrollRunId,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.EmployeeLoans
            .Include(l => l.Installments)
            .Where(l => l.Installments.Any(i => i.SettledInPayrollRunId == payrollRunId))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Plan §5.4 : tous les prêts non annulés avec leurs échéances (diagnostic conformité).</summary>
    public async Task<IReadOnlyList<EmployeeLoan>> ListAllWithInstallmentsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.EmployeeLoans
            .Include(l => l.Installments)
            .Where(l => l.Status != EmployeeLoanStatus.Cancelled)
            .OrderBy(l => l.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<EmployeeLoan> AddAsync(EmployeeLoan entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.EmployeeLoans.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(EmployeeLoan entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.EmployeeLoans.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateRangeAsync(IReadOnlyList<EmployeeLoan> entities, CancellationToken cancellationToken = default)
    {
        if (entities.Count == 0)
            return;

        await using var context = _contextFactory.CreateContext();
        context.EmployeeLoans.UpdateRange(entities);
        await context.SaveChangesAsync(cancellationToken);
    }
}
