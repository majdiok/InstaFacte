using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class EmployeeGarnishmentRepository : IEmployeeGarnishmentRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public EmployeeGarnishmentRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<EmployeeGarnishment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.EmployeeGarnishments.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
    }

    public async Task<EmployeeGarnishment?> GetByIdWithInstallmentsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.EmployeeGarnishments
            .Include(g => g.Installments)
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<EmployeeGarnishment>> ListByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.EmployeeGarnishments
            .AsNoTracking()
            .Include(g => g.Installments)
            .Where(g => g.EmployeeId == employeeId)
            .OrderBy(g => g.Priority)
            .ThenBy(g => g.IssuedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EmployeeGarnishment>> ListActiveForEmployeesAsync(
        IReadOnlyCollection<Guid> employeeIds,
        DateTime referenceDate,
        CancellationToken cancellationToken = default)
    {
        if (employeeIds.Count == 0)
            return Array.Empty<EmployeeGarnishment>();

        var date = referenceDate.Date;
        await using var context = _contextFactory.CreateContext();
        var garnishments = await context.EmployeeGarnishments
            .Include(g => g.Installments)
            .Where(g => employeeIds.Contains(g.EmployeeId)
                && g.Status == Domain.Enums.EmployeeGarnishmentStatus.Active
                && g.StartDate <= date
                && (g.EndDate == null || g.EndDate >= date))
            .ToListAsync(cancellationToken);

        return garnishments.Where(g => g.IsActiveOn(date)).ToList();
    }

    public async Task<IReadOnlyList<EmployeeGarnishment>> ListWithInstallmentsForRunAsync(
        Guid payrollRunId,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.EmployeeGarnishments
            .Include(g => g.Installments)
            .Where(g => g.Installments.Any(i => i.PayrollRunId == payrollRunId))
            .ToListAsync(cancellationToken);
    }

    public async Task<EmployeeGarnishment> AddAsync(EmployeeGarnishment entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.EmployeeGarnishments.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(EmployeeGarnishment entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.EmployeeGarnishments.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateRangeAsync(IReadOnlyList<EmployeeGarnishment> entities, CancellationToken cancellationToken = default)
    {
        if (entities.Count == 0)
            return;

        await using var context = _contextFactory.CreateContext();
        context.EmployeeGarnishments.UpdateRange(entities);
        await context.SaveChangesAsync(cancellationToken);
    }
}
