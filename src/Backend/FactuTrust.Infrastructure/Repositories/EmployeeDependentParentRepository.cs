using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class EmployeeDependentParentRepository : IEmployeeDependentParentRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public EmployeeDependentParentRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<EmployeeDependentParent>> GetActiveByEmployeeIdAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.EmployeeDependentParents
            .AsNoTracking()
            .Where(p => p.EmployeeId == employeeId && p.EndDate == null)
            .OrderBy(p => p.Kinship)
            .ThenBy(p => p.ParentCin)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EmployeeDependentParent>> GetActiveByEmployeeIdsAsync(
        IReadOnlyCollection<Guid> employeeIds,
        CancellationToken cancellationToken = default)
    {
        if (employeeIds.Count == 0)
            return Array.Empty<EmployeeDependentParent>();

        await using var context = _contextFactory.CreateContext();
        return await context.EmployeeDependentParents
            .AsNoTracking()
            .Where(p => employeeIds.Contains(p.EmployeeId) && p.EndDate == null)
            .OrderBy(p => p.EmployeeId)
            .ThenBy(p => p.Kinship)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EmployeeDependentParent>> ListAllActiveAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.EmployeeDependentParents
            .AsNoTracking()
            .Where(p => p.EndDate == null)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, Guid>> GetActiveCinIndexAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var rows = await context.EmployeeDependentParents
            .AsNoTracking()
            .Where(p => p.EndDate == null)
            .Select(p => new { p.ParentCin, p.EmployeeId })
            .ToListAsync(cancellationToken);

        // En cas de doublon (course avant index unique), on garde le premier déclaré.
        var index = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var row in rows)
            index.TryAdd(row.ParentCin, row.EmployeeId);

        return index;
    }

    public async Task<EmployeeDependentParent?> FindActiveConflictAsync(
        string normalizedParentCin,
        Guid? excludeEmployeeId = null,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var query = context.EmployeeDependentParents
            .AsNoTracking()
            .Where(p => p.EndDate == null && p.ParentCin == normalizedParentCin);

        if (excludeEmployeeId.HasValue)
            query = query.Where(p => p.EmployeeId != excludeEmployeeId.Value);

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task ReplaceActiveClaimsAsync(
        Guid employeeId,
        IReadOnlyList<EmployeeDependentParent> newClaims,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var existing = await context.EmployeeDependentParents
            .Where(p => p.EmployeeId == employeeId && p.EndDate == null)
            .ToListAsync(cancellationToken);

        var today = DateTime.UtcNow.Date;
        foreach (var claim in existing)
            claim.End(today);

        foreach (var claim in newClaims)
            context.EmployeeDependentParents.Add(claim);

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task EndAllActiveForEmployeeAsync(
        Guid employeeId,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var existing = await context.EmployeeDependentParents
            .Where(p => p.EmployeeId == employeeId && p.EndDate == null)
            .ToListAsync(cancellationToken);

        if (existing.Count == 0)
            return;

        foreach (var claim in existing)
            claim.End(endDate.Date);

        await context.SaveChangesAsync(cancellationToken);
    }
}
