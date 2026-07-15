using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class JournalEntryRepository : IJournalEntryRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public JournalEntryRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<JournalEntry?> GetBySourceAsync(string sourceEntityType, Guid sourceEntityId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.JournalEntries
            .Include(j => j.Lines)
            .Include(j => j.AccountingPeriod)
            .FirstOrDefaultAsync(
                j => j.SourceEntityType == sourceEntityType && j.SourceEntityId == sourceEntityId,
                cancellationToken);
    }

    public async Task<JournalEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.JournalEntries
            .Include(j => j.Lines)
            .Include(j => j.AccountingPeriod)
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<JournalEntry>> GetDraftsByPeriodAsync(Guid periodId, string? journalCode, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var query = context.JournalEntries
            .Include(j => j.Lines)
            .Where(j => j.AccountingPeriodId == periodId && j.Status == JournalEntryStatus.Brouillon);

        if (!string.IsNullOrWhiteSpace(journalCode))
        {
            var code = journalCode.Trim().ToUpperInvariant();
            query = query.Where(j => j.JournalCode == code);
        }

        return await query
            .OrderBy(j => j.EntryDate)
            .ThenBy(j => j.EntryNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountDraftsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.JournalEntries
            .CountAsync(j => j.Status == JournalEntryStatus.Brouillon, cancellationToken);
    }

    public async Task<int> CountDraftsByFiscalYearAsync(int fiscalYear, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.JournalEntries
            .CountAsync(j => j.Status == JournalEntryStatus.Brouillon && j.EntryDate.Year == fiscalYear, cancellationToken);
    }

    public async Task<JournalEntry> AddAsync(JournalEntry entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.JournalEntries.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(JournalEntry entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.JournalEntries.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(JournalEntry entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.JournalEntries.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Result> MutateAsync(Guid id, Func<JournalEntry, Result> mutate, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var entity = await context.JournalEntries
            .Include(j => j.Lines)
            .Include(j => j.AccountingPeriod)
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound("JournalEntry", id));

        var result = mutate(entity);
        if (result.IsFailure)
            return result;

        // L'entité est SUIVIE : un remplacement de lignes (UpdateDraftLines) est reconcilié
        // correctement (anciennes lignes supprimées, nouvelles insérées).
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<int> ReserveNextEntryNumberAsync(string journalCode, int fiscalYear, CancellationToken cancellationToken = default)
    {
        var code = journalCode.Trim().ToUpperInvariant();
        await using var context = _contextFactory.CreateContext();
        var strategy = context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var seq = await context.JournalEntrySequences
                    .FirstOrDefaultAsync(s => s.JournalCode == code && s.FiscalYear == fiscalYear, cancellationToken);

                if (seq is null)
                {
                    seq = JournalEntrySequence.Create(code, fiscalYear);
                    seq.SetAuditInfo("system", isUpdate: false);
                    context.JournalEntrySequences.Add(seq);
                    await context.SaveChangesAsync(cancellationToken);
                }

                var n = seq.Next();
                context.JournalEntrySequences.Update(seq);
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return n;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task<decimal> SumDebitsByAccountAsync(string accountNumber, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var acc = accountNumber.Trim();
        var fromDate = from.Date;
        var toDate = to.Date;

        return await context.JournalEntryLines
            .Where(l => l.AccountNumber == acc)
            .Where(l => l.JournalEntry!.EntryDate >= fromDate && l.JournalEntry.EntryDate <= toDate)
            .Where(l => !l.JournalEntry!.IsReversed)
            // Consommé par la déclaration TVA (sortie légale) : jamais de brouillons.
            .Where(l => l.JournalEntry!.Status != Domain.Enums.JournalEntryStatus.Brouillon)
            .SumAsync(l => l.DebitAmount.Amount, cancellationToken);
    }
}
