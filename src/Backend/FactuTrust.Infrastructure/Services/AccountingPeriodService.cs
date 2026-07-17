using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services;

public sealed class AccountingPeriodService : IAccountingPeriodService
{
    private readonly IAccountingPeriodRepository _periods;
    private readonly ITenantDbContextFactory _contextFactory;
    private readonly AccountingSettings _settings;

    public AccountingPeriodService(
        IAccountingPeriodRepository periods,
        ITenantDbContextFactory contextFactory,
        IOptions<AccountingSettings> settings)
    {
        _periods = periods;
        _contextFactory = contextFactory;
        _settings = settings.Value;
    }

    public async Task<Result<AccountingPeriod>> EnsureOpenPeriodAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var y = date.Year;
        var m = date.Month;

        // Verrouillage définitif (Lot H) : aucune écriture ne peut naître sur un exercice verrouillé.
        // Point de passage unique de toute création d'écriture (manuelle, auto, à-nouveaux, inventaire).
        if (_settings.DefinitiveLockEnabled && await IsYearLockedAsync(y, cancellationToken))
            return Result.Failure<AccountingPeriod>(Error.Validation("Lock",
                $"L'exercice {y} est verrouillé définitivement : aucune écriture ne peut y être enregistrée."));

        var p = await _periods.GetByYearMonthAsync(y, m, cancellationToken);
        if (p is null)
        {
            var start = new DateTime(y, m, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = new DateTime(y, m, DateTime.DaysInMonth(y, m), 0, 0, 0, DateTimeKind.Utc);
            p = AccountingPeriod.Create(y, m, start, end);
            p.SetAuditInfo("system", false);
            await _periods.AddAsync(p, cancellationToken);
        }

        if (p.IsClosed)
            return Result.Failure<AccountingPeriod>(Error.Validation("AccountingPeriod", "La période comptable est clôturée."));

        return Result.Success(p);
    }

    public async Task<Result> ClosePeriodWithLockAsync(Guid periodId, string closedBy, CancellationToken cancellationToken = default)
    {
        await using var strategyContext = _contextFactory.CreateIsolatedContext();
        var strategy = strategyContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var ctx = _contextFactory.CreateIsolatedContext();
            var relational = ctx.Database.IsRelational();
            await using var transaction = relational
                ? await ctx.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken)
                : null;

            try
            {
                // Lock the period row to prevent concurrent entry creation (SQL Server only ;
                // le provider InMemory des tests ne supporte ni FromSqlRaw ni les verrous).
                var period = relational
                    ? await ctx.AccountingPeriods
                        .FromSqlRaw("SELECT * FROM AccountingPeriods WITH (UPDLOCK, HOLDLOCK) WHERE Id = {0}", periodId)
                        .FirstOrDefaultAsync(cancellationToken)
                    : await ctx.AccountingPeriods
                        .FirstOrDefaultAsync(p => p.Id == periodId, cancellationToken);

                if (period is null)
                {
                    if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure(Error.NotFound("AccountingPeriod", periodId));
                }

                if (period.IsClosed)
                {
                    if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure(Error.Validation("AccountingPeriod", "La période est déjà clôturée."));
                }

                // Garde-fou légal (C4) : une période close fige ses chiffres — aucun brouillon
                // ne doit y subsister (même règle que la clôture annuelle). Sans effet quand le
                // workflow brouillard est désactivé.
                var drafts = await ctx.JournalEntries
                    .CountAsync(e => e.AccountingPeriodId == period.Id && e.Status == JournalEntryStatus.Brouillon,
                        cancellationToken);
                if (drafts > 0)
                {
                    if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure(Error.Validation("Brouillard",
                        $"{drafts} écriture(s) en brouillard sur la période {period.Month:00}/{period.FiscalYear}. Validez-les (ou supprimez-les) avant la clôture."));
                }

                period.Close(closedBy);

                // Verrouillage des écritures : Validee → Cloturee, dans la même transaction.
                await FlipPeriodEntriesStatusAsync(ctx, period.Id,
                    JournalEntryStatus.Validee, JournalEntryStatus.Cloturee, cancellationToken);

                await ctx.SaveChangesAsync(cancellationToken);
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);

                return Result.Success();
            }
            catch
            {
                if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task<Result<AccountingPeriod>> ReopenPeriodAsync(Guid periodId, CancellationToken cancellationToken = default)
    {
        await using var strategyContext = _contextFactory.CreateIsolatedContext();
        var strategy = strategyContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var ctx = _contextFactory.CreateIsolatedContext();
            var relational = ctx.Database.IsRelational();
            await using var transaction = relational
                ? await ctx.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken)
                : null;

            try
            {
                var period = await ctx.AccountingPeriods
                    .FirstOrDefaultAsync(p => p.Id == periodId, cancellationToken);

                if (period is null)
                {
                    if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure<AccountingPeriod>(Error.NotFound("AccountingPeriod", periodId));
                }

                if (!period.IsClosed)
                {
                    if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                    return Result.Success(period); // idempotent : déjà ouverte
                }

                // Verrouillage définitif (Lot H) : un exercice verrouillé ne se rouvre pas.
                if (_settings.DefinitiveLockEnabled
                    && await ctx.AccountingYearLocks.AnyAsync(l => l.FiscalYear == period.FiscalYear, cancellationToken))
                {
                    if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure<AccountingPeriod>(Error.Validation("Lock",
                        $"L'exercice {period.FiscalYear} est verrouillé définitivement : la réouverture est impossible."));
                }

                period.Reopen();

                // Symétrie de la clôture : les écritures redeviennent modifiables par extourne
                // (Cloturee → Validee).
                await FlipPeriodEntriesStatusAsync(ctx, period.Id,
                    JournalEntryStatus.Cloturee, JournalEntryStatus.Validee, cancellationToken);

                await ctx.SaveChangesAsync(cancellationToken);
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);

                return Result.Success(period);
            }
            catch
            {
                if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    /// <summary>Vrai si l'exercice est verrouillé définitivement (une ligne AccountingYearLock existe).</summary>
    private async Task<bool> IsYearLockedAsync(int fiscalYear, CancellationToken cancellationToken)
    {
        await using var ctx = _contextFactory.CreateIsolatedContext();
        return await ctx.AccountingYearLocks.AsNoTracking().AnyAsync(l => l.FiscalYear == fiscalYear, cancellationToken);
    }

    /// <summary>
    /// Bascule en masse le statut des écritures d'une période (clôture : Validee → Cloturee ;
    /// réouverture : Cloturee → Validee). <c>ExecuteUpdateAsync</c> en production (SQL) ;
    /// mise à jour suivie équivalente sous le provider InMemory des tests.
    /// </summary>
    private static async Task FlipPeriodEntriesStatusAsync(
        TenantDbContext ctx,
        Guid periodId,
        JournalEntryStatus from,
        JournalEntryStatus to,
        CancellationToken cancellationToken)
    {
        if (ctx.Database.IsRelational())
        {
            await ctx.JournalEntries
                .Where(e => e.AccountingPeriodId == periodId && e.Status == from)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, to), cancellationToken);
            return;
        }

        var entries = await ctx.JournalEntries
            .Where(e => e.AccountingPeriodId == periodId && e.Status == from)
            .ToListAsync(cancellationToken);
        foreach (var entry in entries)
        {
            if (to == JournalEntryStatus.Cloturee) entry.MarkPeriodClosed();
            else entry.MarkPeriodReopened();
        }
    }
}
