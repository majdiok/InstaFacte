using FactuTrust.Application.Common.Interfaces.Repositories;

using FactuTrust.Domain.Entities.Payroll;

using FactuTrust.Infrastructure.MultiTenancy;

using Microsoft.EntityFrameworkCore;



namespace FactuTrust.Infrastructure.Repositories;



public sealed class PayrollOvertimeRepository : IPayrollOvertimeRepository

{

    private readonly ITenantDbContextFactory _contextFactory;



    public PayrollOvertimeRepository(ITenantDbContextFactory contextFactory)

    {

        _contextFactory = contextFactory;

    }



    public async Task<PayrollOvertimeLine?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)

    {

        await using var context = _contextFactory.CreateContext();

        return await context.PayrollOvertimeLines.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    }



    public async Task<IReadOnlyList<PayrollOvertimeLine>> ListForMonthAsync(int year, int month, CancellationToken cancellationToken = default)

    {

        await using var context = _contextFactory.CreateContext();

        return await context.PayrollOvertimeLines

            .AsNoTracking()

            .Where(l => l.Year == year && l.Month == month)

            .OrderBy(l => l.EmployeeId)

            .ThenBy(l => l.RatePercent)

            .ToListAsync(cancellationToken);

    }



    public async Task<IReadOnlyList<PayrollOvertimeLine>> ListByEmployeeAndMonthAsync(

        Guid employeeId,

        int year,

        int month,

        CancellationToken cancellationToken = default)

    {

        await using var context = _contextFactory.CreateContext();

        return await context.PayrollOvertimeLines

            .AsNoTracking()

            .Where(l => l.EmployeeId == employeeId && l.Year == year && l.Month == month)

            .OrderBy(l => l.RatePercent)

            .ToListAsync(cancellationToken);

    }



    public async Task<PayrollOvertimeLine> AddAsync(PayrollOvertimeLine entity, CancellationToken cancellationToken = default)

    {

        await using var context = _contextFactory.CreateContext();

        context.PayrollOvertimeLines.Add(entity);

        await context.SaveChangesAsync(cancellationToken);

        return entity;

    }



    public async Task UpdateAsync(PayrollOvertimeLine entity, CancellationToken cancellationToken = default)

    {

        await using var context = _contextFactory.CreateContext();

        context.PayrollOvertimeLines.Update(entity);

        await context.SaveChangesAsync(cancellationToken);

    }



    public async Task DeleteAsync(PayrollOvertimeLine entity, CancellationToken cancellationToken = default)

    {

        await using var context = _contextFactory.CreateContext();

        context.PayrollOvertimeLines.Remove(entity);

        await context.SaveChangesAsync(cancellationToken);

    }

}


