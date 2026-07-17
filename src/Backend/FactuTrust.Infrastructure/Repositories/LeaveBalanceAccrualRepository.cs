using FactuTrust.Application.Common.Interfaces.Repositories;

using FactuTrust.Domain.Entities.Payroll;

using FactuTrust.Infrastructure.MultiTenancy;

using Microsoft.EntityFrameworkCore;



namespace FactuTrust.Infrastructure.Repositories;



public sealed class LeaveBalanceAccrualRepository : ILeaveBalanceAccrualRepository

{

    private readonly ITenantDbContextFactory _contextFactory;



    public LeaveBalanceAccrualRepository(ITenantDbContextFactory contextFactory)

    {

        _contextFactory = contextFactory;

    }



    public async Task<LeaveBalanceAccrual?> GetByEmployeePeriodAsync(

        Guid employeeId,

        int year,

        int month,

        CancellationToken cancellationToken = default)

    {

        await using var context = _contextFactory.CreateContext();

        return await context.LeaveBalanceAccruals

            .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.Year == year && a.Month == month, cancellationToken);

    }



    public async Task<IReadOnlyList<LeaveBalanceAccrual>> ListByEmployeeAndYearAsync(

        Guid employeeId,

        int year,

        CancellationToken cancellationToken = default)

    {

        await using var context = _contextFactory.CreateContext();

        return await context.LeaveBalanceAccruals

            .AsNoTracking()

            .Where(a => a.EmployeeId == employeeId && a.Year == year)

            .OrderBy(a => a.Month)

            .ToListAsync(cancellationToken);

    }



    public async Task<IReadOnlyList<LeaveBalanceAccrual>> ListByPayrollRunIdAsync(

        Guid payrollRunId,

        CancellationToken cancellationToken = default)

    {

        await using var context = _contextFactory.CreateContext();

        return await context.LeaveBalanceAccruals

            .Where(a => a.PayrollRunId == payrollRunId)

            .ToListAsync(cancellationToken);

    }



    public async Task<IReadOnlyList<LeaveBalanceAccrual>> GetByEmployeePeriodsAsync(
        IReadOnlyCollection<Guid> employeeIds,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        if (employeeIds.Count == 0)
            return Array.Empty<LeaveBalanceAccrual>();

        await using var context = _contextFactory.CreateContext();
        return await context.LeaveBalanceAccruals
            .AsNoTracking()
            .Where(a => employeeIds.Contains(a.EmployeeId) && a.Year == year && a.Month == month)
            .ToListAsync(cancellationToken);
    }

    public async Task<LeaveBalanceAccrual> AddAsync(LeaveBalanceAccrual entity, CancellationToken cancellationToken = default)

    {

        await using var context = _contextFactory.CreateContext();

        context.LeaveBalanceAccruals.Add(entity);

        await context.SaveChangesAsync(cancellationToken);

        return entity;

    }

    public async Task AddRangeAsync(IReadOnlyList<LeaveBalanceAccrual> entities, CancellationToken cancellationToken = default)
    {
        if (entities.Count == 0)
            return;

        await using var context = _contextFactory.CreateContext();
        context.LeaveBalanceAccruals.AddRange(entities);
        await context.SaveChangesAsync(cancellationToken);
    }



    public async Task DeleteByPayrollRunIdAsync(Guid payrollRunId, CancellationToken cancellationToken = default)

    {

        await using var context = _contextFactory.CreateContext();

        var accruals = await context.LeaveBalanceAccruals

            .Where(a => a.PayrollRunId == payrollRunId)

            .ToListAsync(cancellationToken);

        if (accruals.Count == 0)

            return;



        context.LeaveBalanceAccruals.RemoveRange(accruals);

        await context.SaveChangesAsync(cancellationToken);

    }

}


