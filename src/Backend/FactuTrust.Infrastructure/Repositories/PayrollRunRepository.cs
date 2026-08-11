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

    public async Task<PayrollRun?> GetByIdWithPayslipsForPaymentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PayrollRuns
            .Include(r => r.Payslips)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
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

    public async Task<IReadOnlyList<PayrollRunWithPayslipCount>> ListWithPayslipCountsAsync(
        int? year = null,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var query = context.PayrollRuns.AsNoTracking().AsQueryable();
        if (year.HasValue)
            query = query.Where(r => r.Year == year.Value);

        // Le compte est projeté par le serveur : aucun bulletin, aucune ligne ne remonte.
        var rows = await query
            .OrderByDescending(r => r.Year).ThenByDescending(r => r.Month)
            .Select(r => new { Run = r, PayslipCount = r.Payslips.Count })
            .ToListAsync(cancellationToken);

        return rows
            .Select(x => new PayrollRunWithPayslipCount(x.Run, x.PayslipCount))
            .ToList();
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
            // La DTS est une déclaration officielle : seuls les cycles arrêtés y figurent.
            .Where(r => r.Status == PayrollRunStatus.Validated || r.Status == PayrollRunStatus.Closed)
            .OrderBy(r => r.Month)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Payslip>> ListSettledPayslipsForYearAsync(
        int year,
        int untilMonthExclusive,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PayrollRuns
            .AsNoTracking()
            .Where(r => r.Year == year && r.Month < untilMonthExclusive)
            // Comme la DTS : seuls les cycles arrêtés alimentent le cumul, sans quoi une
            // régularisation proposée changerait au gré des recalculs des mois ouverts.
            .Where(r => r.Status == PayrollRunStatus.Validated || r.Status == PayrollRunStatus.Closed)
            .SelectMany(r => r.Payslips)
            .OrderBy(p => p.Month)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Payslip>> ListSettledPayslipsForEmployeeAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.Payslips
            .AsNoTracking()
            .Include(p => p.Lines)
            .Where(p => p.EmployeeId == employeeId)
            .Where(p => context.PayrollRuns.Any(r =>
                r.Id == p.PayrollRunId
                && (r.Status == PayrollRunStatus.Validated || r.Status == PayrollRunStatus.Closed)))
            .OrderBy(p => p.Year)
            .ThenBy(p => p.Month)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PayrollRun>> ListByMonthRangeWithPayslipsAsync(
        int year,
        int fromMonth,
        int toMonth,
        bool includeCalculated,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PayrollRuns
            .AsNoTracking()
            // Les états de contrôle agrègent par salarié : les lignes de bulletin ne sont pas
            // chargées (évite une jointure cartésienne sur toute la plage).
            .Include(r => r.Payslips)
            .Where(r => r.Year == year && r.Month >= fromMonth && r.Month <= toMonth)
            // Un cycle Brouillon n'a aucun bulletin persisté : il est toujours hors périmètre.
            .Where(r => r.Status == PayrollRunStatus.Validated
                        || r.Status == PayrollRunStatus.Closed
                        || (includeCalculated && r.Status == PayrollRunStatus.Calculated))
            .OrderBy(r => r.Month)
            .ToListAsync(cancellationToken);
    }

    public async Task<PayrollRun?> GetByPeriodWithPayslipsAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PayrollRuns
            .AsNoTracking()
            .Include(r => r.Payslips)
            .FirstOrDefaultAsync(r => r.Year == year && r.Month == month, cancellationToken);
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
