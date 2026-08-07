using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class TerminationSettlementRepository : ITerminationSettlementRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public TerminationSettlementRepository(ITenantDbContextFactory contextFactory) => _contextFactory = contextFactory;

    public async Task<TerminationSettlement?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.TerminationSettlements.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<TerminationSettlement?> GetByEmployeeAndMonthAsync(
        Guid employeeId, int year, int month, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.TerminationSettlements.FirstOrDefaultAsync(
            s => s.EmployeeId == employeeId && s.Year == year && s.Month == month, cancellationToken);
    }

    public async Task<IReadOnlyList<TerminationSettlement>> ListForMonthAsync(
        int year, int month, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.TerminationSettlements
            .AsNoTracking()
            .Where(s => s.Year == year && s.Month == month)
            .OrderBy(s => s.EmployeeId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TerminationSettlement>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.TerminationSettlements
            .AsNoTracking()
            .OrderByDescending(s => s.TerminationDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<TerminationSettlement> AddAsync(TerminationSettlement entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.TerminationSettlements.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(TerminationSettlement entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.TerminationSettlements.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(TerminationSettlement entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.TerminationSettlements.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }
}
