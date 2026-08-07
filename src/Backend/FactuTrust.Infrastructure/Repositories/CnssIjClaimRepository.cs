using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class CnssIjClaimRepository : ICnssIjClaimRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public CnssIjClaimRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<CnssIjClaim?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.CnssIjClaims.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<CnssIjClaim>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.CnssIjClaims.OrderByDescending(c => c.Year).ThenByDescending(c => c.Month).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CnssIjClaim>> ListByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.CnssIjClaims
            .AsNoTracking()
            .Where(c => c.EmployeeId == employeeId)
            .OrderByDescending(c => c.Year)
            .ThenByDescending(c => c.Month)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CnssIjClaim>> ListForPeriodAsync(int year, int? month, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var query = context.CnssIjClaims.AsNoTracking().Where(c => c.Year == year);
        if (month.HasValue)
            query = query.Where(c => c.Month == month.Value);

        return await query
            .OrderBy(c => c.Month)
            .ThenBy(c => c.EmployeeId)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsForLeaveAndPeriodAsync(Guid leaveRequestId, int year, int month, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.CnssIjClaims.AnyAsync(
            c => c.LeaveRequestId == leaveRequestId && c.Year == year && c.Month == month,
            cancellationToken);
    }

    public async Task<CnssIjClaim> AddAsync(CnssIjClaim entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.CnssIjClaims.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(CnssIjClaim entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.CnssIjClaims.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(CnssIjClaim entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.CnssIjClaims.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.CnssIjClaims.AnyAsync(c => c.Id == id, cancellationToken);
    }
}
