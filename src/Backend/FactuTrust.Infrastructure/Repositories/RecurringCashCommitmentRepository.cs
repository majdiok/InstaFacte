using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.Treasury;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class RecurringCashCommitmentRepository : IRecurringCashCommitmentRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public RecurringCashCommitmentRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<RecurringCashCommitment>> ListAsync(
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();

        var query = ctx.RecurringCashCommitments.AsNoTracking();
        if (!includeInactive)
            query = query.Where(c => c.IsActive);

        return await query
            .OrderBy(c => c.Direction)
            .ThenBy(c => c.Label)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RecurringCashCommitment>> ListActiveForWindowAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();

        var windowStart = from.Date;
        var windowEnd = to.Date;

        return await ctx.RecurringCashCommitments
            .AsNoTracking()
            .Where(c => c.IsActive
                        && c.StartDate <= windowEnd
                        && (c.EndDate == null || c.EndDate >= windowStart))
            .OrderBy(c => c.Label)
            .ToListAsync(cancellationToken);
    }

    public async Task<RecurringCashCommitment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();
        return await ctx.RecurringCashCommitments.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task AddAsync(RecurringCashCommitment commitment, CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();
        ctx.RecurringCashCommitments.Add(commitment);
        await ctx.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(RecurringCashCommitment commitment, CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();
        ctx.RecurringCashCommitments.Update(commitment);
        await ctx.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(RecurringCashCommitment commitment, CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();
        ctx.RecurringCashCommitments.Remove(commitment);
        await ctx.SaveChangesAsync(cancellationToken);
    }
}

public sealed class CashFlowForecastSettingsRepository : ICashFlowForecastSettingsRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public CashFlowForecastSettingsRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<CashFlowForecastSettings?> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();

        // Une seule ligne par base tenant : on prend la plus ancienne pour rester déterministe
        // si un doublon devait apparaître.
        return await ctx.CashFlowForecastSettings
            .AsNoTracking()
            .OrderBy(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task SaveAsync(CashFlowForecastSettings settings, CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();

        var exists = await ctx.CashFlowForecastSettings
            .AsNoTracking()
            .AnyAsync(s => s.Id == settings.Id, cancellationToken);

        if (exists)
            ctx.CashFlowForecastSettings.Update(settings);
        else
            ctx.CashFlowForecastSettings.Add(settings);

        await ctx.SaveChangesAsync(cancellationToken);
    }
}
