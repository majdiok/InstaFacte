using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for employee salary advances.
/// </summary>
public sealed class EmployeeAdvanceRepository : IEmployeeAdvanceRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public EmployeeAdvanceRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<EmployeeAdvance?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.EmployeeAdvances.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<EmployeeAdvance>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.EmployeeAdvances
            .OrderByDescending(a => a.Date)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EmployeeAdvance>> ListByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.EmployeeAdvances
            .Where(a => a.EmployeeId == employeeId)
            .OrderByDescending(a => a.Date)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EmployeeAdvance>> ListOutstandingAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.EmployeeAdvances
            .Where(a => a.EmployeeId == employeeId && !a.IsSettled)
            .OrderBy(a => a.Date)
            .ToListAsync(cancellationToken);
    }

    public async Task<EmployeeAdvance> AddAsync(EmployeeAdvance entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.EmployeeAdvances.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(EmployeeAdvance entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.EmployeeAdvances.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(EmployeeAdvance entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.EmployeeAdvances.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.EmployeeAdvances.AnyAsync(a => a.Id == id, cancellationToken);
    }
}
