using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class EmployeeInKindBenefitRepository : IEmployeeInKindBenefitRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public EmployeeInKindBenefitRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<EmployeeInKindBenefit?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.EmployeeInKindBenefits.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<EmployeeInKindBenefit>> ListByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.EmployeeInKindBenefits
            .AsNoTracking()
            .Where(b => b.EmployeeId == employeeId)
            .OrderByDescending(b => b.StartDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EmployeeInKindBenefit>> ListActiveForEmployeesAsync(
        IReadOnlyCollection<Guid> employeeIds,
        DateTime referenceDate,
        CancellationToken cancellationToken = default)
    {
        if (employeeIds.Count == 0)
            return Array.Empty<EmployeeInKindBenefit>();

        var date = referenceDate.Date;
        await using var context = _contextFactory.CreateContext();
        return await context.EmployeeInKindBenefits
            .AsNoTracking()
            .Where(b => employeeIds.Contains(b.EmployeeId)
                && b.StartDate <= date
                && (b.EndDate == null || b.EndDate >= date))
            .ToListAsync(cancellationToken);
    }

    public async Task<EmployeeInKindBenefit> AddAsync(EmployeeInKindBenefit entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.EmployeeInKindBenefits.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(EmployeeInKindBenefit entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.EmployeeInKindBenefits.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(EmployeeInKindBenefit entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.EmployeeInKindBenefits.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }
}
