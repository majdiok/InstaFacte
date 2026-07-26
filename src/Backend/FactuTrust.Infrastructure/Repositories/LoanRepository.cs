using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class LoanRepository : ILoanRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public LoanRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Loan?> GetByIdAsync(Guid id, bool includeSchedule = false, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        IQueryable<Loan> query = context.Loans;
        if (includeSchedule)
            query = query.Include(l => l.ScheduleLines.OrderBy(s => s.InstallmentNumber));
        return await query.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
    }

    public async Task<Loan?> GetByLoanNumberAsync(string loanNumber, CancellationToken cancellationToken = default)
    {
        var number = loanNumber.Trim();
        await using var context = _contextFactory.CreateContext();
        return await context.Loans.AsNoTracking().FirstOrDefaultAsync(l => l.LoanNumber == number, cancellationToken);
    }

    public async Task<(IReadOnlyList<Loan> Items, int TotalCount)> SearchAsync(
        int page, int pageSize, string? search, int? status, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var query = context.Loans.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(l =>
                l.LoanNumber.Contains(term) || l.Label.Contains(term) || l.LenderName.Contains(term));
        }
        if (status.HasValue)
            query = query.Where(l => (int)l.Status == status.Value);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(l => l.StartDate)
            .ThenBy(l => l.LoanNumber)
            .Skip(Math.Max(0, page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<int> CountByYearPrefixAsync(int year, CancellationToken cancellationToken = default)
    {
        var prefix = $"EMP-{year}-";
        await using var context = _contextFactory.CreateContext();
        return await context.Loans.AsNoTracking().CountAsync(l => l.LoanNumber.StartsWith(prefix), cancellationToken);
    }

    public async Task<Loan> AddAsync(Loan entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.Loans.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(Loan entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.Loans.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }
}
