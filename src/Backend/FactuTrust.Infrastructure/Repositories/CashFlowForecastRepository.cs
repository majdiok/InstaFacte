using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.Treasury;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class CashFlowForecastRepository : ICashFlowForecastRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public CashFlowForecastRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<CashFlowForecastRun?> GetLatestComputedAsync(
        int horizonMonths,
        CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();

        return await ctx.CashFlowForecastRuns
            .AsNoTracking()
            .Include(r => r.Buckets)
            .Include(r => r.Scenarios)
            .Include(r => r.Insights)
            .Where(r => r.Status == CashFlowForecastRunStatus.Computed && r.HorizonMonths == horizonMonths)
            .OrderByDescending(r => r.ComputedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<CashFlowForecastRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();

        return await ctx.CashFlowForecastRuns
            .AsNoTracking()
            .Include(r => r.Buckets)
            .Include(r => r.Scenarios)
            .Include(r => r.Insights)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<CashFlowLineQueryResult> ListLinesAsync(
        CashFlowLineQueryCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();

        var query = ctx.CashFlowForecastLines
            .AsNoTracking()
            .Where(l => l.ForecastRunId == criteria.ForecastRunId);

        if (criteria.Direction.HasValue)
            query = query.Where(l => l.Direction == criteria.Direction.Value);

        if (criteria.SourceType.HasValue)
            query = query.Where(l => l.SourceType == criteria.SourceType.Value);

        if (criteria.ExpectedFrom.HasValue)
        {
            var from = criteria.ExpectedFrom.Value.Date;
            query = query.Where(l => l.ExpectedDate >= from);
        }

        if (criteria.ExpectedTo.HasValue)
        {
            var to = criteria.ExpectedTo.Value.Date;
            query = query.Where(l => l.ExpectedDate <= to);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Search))
        {
            var term = criteria.Search.Trim();
            query = query.Where(l =>
                EF.Functions.Like(l.Label, $"%{term}%") ||
                (l.ThirdPartyName != null && EF.Functions.Like(l.ThirdPartyName, $"%{term}%")) ||
                (l.SourceReference != null && EF.Functions.Like(l.SourceReference, $"%{term}%")));
        }

        // Total et somme calculés sur le filtre complet, pas sur la page : les totaux affichés
        // doivent décrire le jeu filtré, sinon ils changent au fil de la pagination.
        var totalCount = await query.CountAsync(cancellationToken);
        var totalAmount = totalCount == 0
            ? 0m
            : await query.SumAsync(l => l.WeightedAmount, cancellationToken);

        var page = Math.Max(criteria.Page, 1);
        var pageSize = Math.Clamp(criteria.PageSize, 1, 200);

        var items = await query
            .OrderBy(l => l.ExpectedDate)
            .ThenBy(l => l.Label)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new CashFlowLineQueryResult(items, totalCount, totalAmount);
    }

    public async Task AddAsync(CashFlowForecastRun run, CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();
        ctx.CashFlowForecastRuns.Add(run);
        await ctx.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(CashFlowForecastRun run, CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();
        ctx.CashFlowForecastRuns.Update(run);
        await ctx.SaveChangesAsync(cancellationToken);
    }

    public async Task DeletePreviousRunsAsync(
        int horizonMonths,
        Guid keepRunId,
        CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();

        // Suppression par entité (et non ExecuteDelete) pour que la cascade configurée sur les
        // quatre tables enfants s'applique de la même façon quel que soit le fournisseur.
        var stale = await ctx.CashFlowForecastRuns
            .Where(r => r.HorizonMonths == horizonMonths && r.Id != keepRunId)
            .ToListAsync(cancellationToken);

        if (stale.Count == 0) return;

        ctx.CashFlowForecastRuns.RemoveRange(stale);
        await ctx.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> CountRunsSinceAsync(DateTime since, CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();

        return await ctx.CashFlowForecastRuns
            .AsNoTracking()
            .CountAsync(r => r.ComputedAt >= since && r.ComputedByUserId != null, cancellationToken);
    }
}
