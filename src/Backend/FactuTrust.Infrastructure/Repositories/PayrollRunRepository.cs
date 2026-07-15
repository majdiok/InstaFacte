using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for the PayrollRun aggregate.
/// </summary>
public sealed class PayrollRunRepository : IPayrollRunRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public PayrollRunRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<PayrollRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PayrollRuns.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<PayrollRun?> GetByIdWithPayslipsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PayrollRuns
            .Include(r => r.Payslips)
            .ThenInclude(p => p.Lines)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<PayrollRun?> GetByPeriodAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PayrollRuns
            .FirstOrDefaultAsync(r => r.Year == year && r.Month == month, cancellationToken);
    }

    public async Task<Payslip?> GetPayslipByIdAsync(Guid payslipId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.Payslips
            .AsNoTracking()
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.Id == payslipId, cancellationToken);
    }

    public async Task<IReadOnlyList<PayrollRun>> ListAsync(int? year = null, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var query = context.PayrollRuns.AsNoTracking().AsQueryable();
        if (year.HasValue)
            query = query.Where(r => r.Year == year.Value);
        return await query
            .OrderByDescending(r => r.Year).ThenByDescending(r => r.Month)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PayrollRun>> ListByQuarterWithPayslipsAsync(int year, int quarter, CancellationToken cancellationToken = default)
    {
        var firstMonth = (quarter - 1) * 3 + 1;
        var lastMonth = firstMonth + 2;

        await using var context = _contextFactory.CreateContext();
        return await context.PayrollRuns
            .AsNoTracking()
            .Include(r => r.Payslips)
            .Where(r => r.Year == year && r.Month >= firstMonth && r.Month <= lastMonth)
            .OrderBy(r => r.Month)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsForPeriodAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PayrollRuns.AnyAsync(r => r.Year == year && r.Month == month, cancellationToken);
    }

    public async Task<bool> HasValidatedOrClosedRunForMonthAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PayrollRuns.AnyAsync(
            r => r.Year == year
                 && r.Month == month
                 && (r.Status == PayrollRunStatus.Validated || r.Status == PayrollRunStatus.Closed),
            cancellationToken);
    }

    public async Task<PayrollRun> AddAsync(PayrollRun run, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.PayrollRuns.Add(run);
        await context.SaveChangesAsync(cancellationToken);
        return run;
    }

    public async Task PersistCalculationAsync(PayrollRun run, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();

        // Remove any previously computed payslips (and their lines) for this run.
        var existingPayslipIds = await context.Payslips
            .Where(p => p.PayrollRunId == run.Id)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        if (existingPayslipIds.Count > 0)
        {
            await context.PayslipLines
                .Where(l => existingPayslipIds.Contains(l.PayslipId))
                .ExecuteDeleteAsync(cancellationToken);
            await context.Payslips
                .Where(p => p.PayrollRunId == run.Id)
                .ExecuteDeleteAsync(cancellationToken);
        }

        var tracked = await context.PayrollRuns.FirstOrDefaultAsync(r => r.Id == run.Id, cancellationToken);
        if (tracked is null)
        {
            // Brand new run: insert the whole graph (run + payslips + lines).
            context.PayrollRuns.Add(run);
        }
        else
        {
            context.Entry(tracked).CurrentValues.SetValues(run);
            foreach (var payslip in run.Payslips)
                context.Payslips.Add(payslip);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateScalarAsync(PayrollRun run, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        // Attach the passed instance (which carries any domain events) and mark only the run row
        // as modified. The run is loaded without payslips for status transitions, so no cascade flood.
        context.PayrollRuns.Attach(run);
        context.Entry(run).State = EntityState.Modified;
        await context.SaveChangesAsync(cancellationToken);
    }
}
