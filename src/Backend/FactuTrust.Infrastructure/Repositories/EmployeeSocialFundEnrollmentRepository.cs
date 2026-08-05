using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class EmployeeSocialFundEnrollmentRepository : IEmployeeSocialFundEnrollmentRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public EmployeeSocialFundEnrollmentRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<EmployeeSocialFundEnrollment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.EmployeeSocialFundEnrollments.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<EmployeeSocialFundEnrollment>> ListByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.EmployeeSocialFundEnrollments
            .AsNoTracking()
            .Where(e => e.EmployeeId == employeeId)
            .OrderByDescending(e => e.StartDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EmployeeSocialFundEnrollment>> ListActiveForEmployeesAsync(
        IReadOnlyCollection<Guid> employeeIds,
        DateTime referenceDate,
        CancellationToken cancellationToken = default)
    {
        if (employeeIds.Count == 0)
            return Array.Empty<EmployeeSocialFundEnrollment>();

        var date = referenceDate.Date;
        await using var context = _contextFactory.CreateContext();
        return await context.EmployeeSocialFundEnrollments
            .AsNoTracking()
            .Where(e => employeeIds.Contains(e.EmployeeId)
                && e.StartDate <= date
                && (e.EndDate == null || e.EndDate >= date))
            .ToListAsync(cancellationToken);
    }

    public async Task<EmployeeSocialFundEnrollment> AddAsync(EmployeeSocialFundEnrollment entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.EmployeeSocialFundEnrollments.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(EmployeeSocialFundEnrollment entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.EmployeeSocialFundEnrollments.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(EmployeeSocialFundEnrollment entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.EmployeeSocialFundEnrollments.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }
}
