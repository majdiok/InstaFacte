using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Features.Accounting;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class JournalEntryRepository : IJournalEntryRepository
{
    private const string SourceCashOperation = "CashOperation";
    private const string SourceManualReversal = "ManualReversal";

    private readonly ITenantDbContextFactory _contextFactory;
    private readonly ILogger<JournalEntryRepository> _logger;

    public JournalEntryRepository(ITenantDbContextFactory contextFactory, ILogger<JournalEntryRepository>? logger = null)
    {
        _contextFactory = contextFactory;
        _logger = logger ?? NullLogger<JournalEntryRepository>.Instance;
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

    public async Task<JournalEntry?> GetActiveBySourceAsync(string sourceEntityType, Guid sourceEntityId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.JournalEntries
            .Include(j => j.Lines)
            .Include(j => j.AccountingPeriod)
            .Where(j => j.SourceEntityType == sourceEntityType
                        && j.SourceEntityId == sourceEntityId
                        && !j.IsReversed)
            .OrderByDescending(j => j.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> ExistsActiveBySourceTypeAsync(string sourceEntityType, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.JournalEntries
            .AnyAsync(j => j.SourceEntityType == sourceEntityType && !j.IsReversed, cancellationToken);
    }

    public async Task<JournalEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.JournalEntries
            .Include(j => j.Lines)
            .Include(j => j.AccountingPeriod)
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
    }

    /// <summary>
    /// Voir doc XML de l'interface (plan §D7). Contexte via <c>CreateContext()</c> pour s'enrôler
    /// dans la transaction ambiante de l'appelant (<c>ITenantUnitOfWork</c>) — indispensable pour
    /// que le verrou soit tenu jusqu'au commit/rollback de l'unité de travail globale.
    /// </summary>
    public async Task<JournalEntry?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();

        if (!context.Database.IsRelational())
        {
            // Repli InMemory (tests) : ni FromSqlRaw ni verrous ne sont supportés par ce provider ;
            // comportement identique à GetByIdAsync.
            return await context.JournalEntries
                .Include(j => j.Lines)
                .Include(j => j.AccountingPeriod)
                .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
        }

        // Verrou exclusif sur la ligne JournalEntries : bloque toute lecture ordinaire concurrente
        // (lecture de décision de la validation) jusqu'à notre commit/rollback (plan §D7/§1.2).
        var entity = await context.JournalEntries
            .FromSqlRaw("SELECT * FROM JournalEntries WITH (XLOCK, HOLDLOCK) WHERE Id = {0}", id)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity is null)
            return null;

        // Verrou exclusif sur les lignes existantes : bloque ValidateLinesAsync (lettrage) le temps
        // de notre transaction. Requête séparée (le hint ne porte que sur JournalEntryLines) ; les
        // lignes chargées se rattachent automatiquement à entity.Lines via le suivi de modifications
        // d'EF Core (entité déjà suivie dans le même contexte, clé étrangère JournalEntryId connue).
        await context.JournalEntryLines
            .FromSqlRaw("SELECT * FROM JournalEntryLines WITH (XLOCK, HOLDLOCK) WHERE JournalEntryId = {0}", id)
            .ToListAsync(cancellationToken);

        // La période comptable est lue sans verrou spécifique : la clôture de période refuse de
        // toute façon toute période contenant encore des brouillons, sous son propre verrou
        // Serializable (AccountingPeriodService.ClosePeriodWithLockAsync).
        await context.Entry(entity).Reference(j => j.AccountingPeriod).LoadAsync(cancellationToken);

        return entity;
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
        // Contexte isolé : la réservation de numéro garde SA transaction, même à l'intérieur
        // d'une unité de travail ambiante (un rollback externe « brûle » le numéro — trou
        // signalé par les contrôles d'intégrité — mais ne casse jamais la séquence).
        await using var context = _contextFactory.CreateIsolatedContext();
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

    public async Task<decimal> SumDebitsByAccountPrefixAsync(string accountPrefix, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var prefix = accountPrefix.Trim();
        var fromDate = from.Date;
        var toDate = to.Date;

        return await context.JournalEntryLines
            .Where(l => l.AccountNumber == prefix || l.AccountNumber.StartsWith(prefix))
            .Where(l => l.JournalEntry!.EntryDate >= fromDate && l.JournalEntry.EntryDate <= toDate)
            .Where(l => !l.JournalEntry!.IsReversed)
            // Consommé par la déclaration mensuelle (RS loyers) : jamais de brouillons.
            .Where(l => l.JournalEntry!.Status != Domain.Enums.JournalEntryStatus.Brouillon)
            .SumAsync(l => l.DebitAmount.Amount, cancellationToken);
    }

    /// <summary>
    /// Déclaration TVA assise sur le JOURNAL (plan §6.7) : on somme ce qui a été réellement
    /// comptabilisé sur 436711/707, pas les opérations de caisse elles-mêmes (l'opération est
    /// sauvegardée avant la publication comptable, un échec de génération est seulement journalisé,
    /// l'annulation ne reverse jamais l'écriture, et le flag TVA peut basculer entre la sauvegarde et
    /// la génération — compter les opérations casserait « compta = déclaration »).
    ///
    /// Aucun filtre de statut : le Brouillon est INCLUS volontairement (cf. plan §6.7) — la TVA
    /// collectée facturière ne filtre jamais sur le statut de l'écriture (seulement sur celui de la
    /// facture) ; exclure la caisse en brouillon créerait une asymétrie entre les deux sources du
    /// même total collecté, et avec BrouillardEnabled actif TOUTES les écritures JC naissent en
    /// brouillon jusqu'à validation par l'expert — les filtrer ferait disparaître silencieusement
    /// la TVA caisse de la déclaration.
    ///
    /// Mouvements SIGNÉS (v2.1) : une écriture caisse extournée reste comptée dans SA période
    /// (positif, malgré IsReversed=true) et son extourne manuelle (SourceEntityType="ManualReversal")
    /// est comptée en négatif dans SA propre période (potentiellement ultérieure) — net nul une fois
    /// les deux périodes déclarées. Un filtre `!IsReversed` ferait disparaître rétroactivement la TVA
    /// de la période d'origine sans mouvement négatif en face (bug corrigé en v2.1).
    ///
    /// Modification des brouillons par le cabinet (plan v3 §1.3/§6.7, D2) : les brouillons
    /// CashOperation/ManualReversal sont désormais éditables par le cabinet en contexte délégué
    /// (UpdateDraftJournalEntryCommandHandler, y compris sous verrou XLOCK/HOLDLOCK pendant
    /// l'édition — GetByIdForUpdateAsync). Cette méthode reste inchangée en comportement : les
    /// montants sont TOUJOURS sommés depuis les lignes RÉELLEMENT présentes sur l'écriture au
    /// moment de l'appel — une édition cabinet se reflète donc immédiatement ici, au même titre que
    /// n'importe quelle autre modification de brouillon (le brouillard est par nature provisoire).
    /// La ventilation par taux continue de se résoudre depuis CashOperation.VatRate de l'opération
    /// source (pas depuis les lignes éditées) : dériver le taux des lignes serait ambigu (agrégat
    /// 707/436711 unique, arrondis, multi-taux non inférable) — décision D2, alternative écartée.
    /// Portée de l'impact d'une édition : elle affecte le recalcul LIVE consommé ICI (tableau de
    /// bord, GetVatDeclarationQuery en mode Live, prochaine SaveVatDeclarationCommand) — jamais une
    /// déclaration déjà ENREGISTRÉE et affichée en mode Declared, qui n'est jamais réécrite en
    /// dehors d'un enregistrement explicite (brouillon rafraîchi en place) ou d'une rectificative
    /// (soumise). Si l'édition retire la ligne 436711, l'écriture sort des candidats ci-dessous —
    /// assumé, cohérent avec la suppression déjà permise du même brouillon.
    /// </summary>
    public async Task<IReadOnlyList<CashSaleVatPosting>> GetPostedCashSaleVatByRateAsync(
        DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var fromDate = from.Date;
        var toDate = to.Date;

        // Candidats bruts : toutes les écritures de la période portant du 436711, qu'elles soient
        // d'origine caisse ou des extournes manuelles (le filtre "l'origine est bien une écriture
        // caisse" pour les extournes est appliqué ci-dessous, une fois les origines chargées).
        var candidates = await context.JournalEntries
            .Include(j => j.Lines)
            .Where(j => j.EntryDate >= fromDate && j.EntryDate <= toDate)
            .Where(j => j.SourceEntityType == SourceCashOperation || j.SourceEntityType == SourceManualReversal)
            .Where(j => j.Lines.Any(l => l.AccountNumber == TunisianPostingAccounts.VatCollected))
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
            return Array.Empty<CashSaleVatPosting>();

        var cashEntries = candidates.Where(j => j.SourceEntityType == SourceCashOperation).ToList();
        var reversalCandidates = candidates.Where(j => j.SourceEntityType == SourceManualReversal).ToList();

        // Résolution des origines des extournes : un seul niveau de chaîne est supporté (v2.1, §6.7).
        var originIds = reversalCandidates
            .Where(r => r.ReversesEntryId.HasValue)
            .Select(r => r.ReversesEntryId!.Value)
            .Distinct()
            .ToList();

        var origins = originIds.Count == 0
            ? new List<JournalEntry>()
            : await context.JournalEntries
                .Where(j => originIds.Contains(j.Id))
                .ToListAsync(cancellationToken);
        var originsById = origins.ToDictionary(o => o.Id);

        // Chaque paire (écriture porteuse de 436711, identifiant de l'opération de caisse source à
        // utiliser pour résoudre le taux). Les écritures d'origine se résolvent directement ; les
        // extournes suivent ReversesEntryId → écriture d'origine → SourceEntityId.
        var resolvedEntries = new List<(JournalEntry Entry, Guid? CashOperationId)>();

        foreach (var entry in cashEntries)
            resolvedEntries.Add((entry, entry.SourceEntityId));

        foreach (var reversal in reversalCandidates)
        {
            if (reversal.ReversesEntryId is not { } originId || !originsById.TryGetValue(originId, out var origin))
            {
                // (d) Extourne dont l'origine n'a pas été trouvée (cas limite) → exclue.
                _logger.LogWarning(
                    "GetPostedCashSaleVatByRateAsync : extourne {EntryId} ignorée (écriture d'origine introuvable).",
                    reversal.Id);
                continue;
            }

            if (origin.SourceEntityType == SourceManualReversal)
            {
                // Extourne-d'extourne : un seul niveau de chaîne est supporté (limitation documentée
                // au plan §6.7/§10). Ignorée avec warning.
                _logger.LogWarning(
                    "GetPostedCashSaleVatByRateAsync : extourne {EntryId} ignorée (extourne-d'extourne, un seul niveau de chaîne est supporté).",
                    reversal.Id);
                continue;
            }

            if (origin.SourceEntityType != SourceCashOperation)
            {
                // (d) Extourne d'une écriture qui n'est pas d'origine caisse → hors périmètre.
                continue;
            }

            resolvedEntries.Add((reversal, origin.SourceEntityId));
        }

        if (resolvedEntries.Count == 0)
            return Array.Empty<CashSaleVatPosting>();

        var cashOperationIds = resolvedEntries
            .Where(r => r.CashOperationId.HasValue)
            .Select(r => r.CashOperationId!.Value)
            .Distinct()
            .ToList();

        var operations = cashOperationIds.Count == 0
            ? new List<CashOperation>()
            : await context.CashOperations
                .Where(o => cashOperationIds.Contains(o.Id))
                .ToListAsync(cancellationToken);
        var operationsById = operations.ToDictionary(o => o.Id);

        var totalsByRate = new Dictionary<int, (decimal HtBase, decimal VatAmount)>();

        foreach (var (entry, cashOperationId) in resolvedEntries)
        {
            CashOperation? op = null;
            if (cashOperationId is { } opId)
                operationsById.TryGetValue(opId, out op);

            if (op is null || op.VatRate is null)
            {
                // Cas défensif (ne devrait pas exister) : une écriture 436711 d'origine caisse dont
                // l'opération source est introuvable ou sans VatRate. Ignorée + warning (plan §6.7).
                _logger.LogWarning(
                    "GetPostedCashSaleVatByRateAsync : écriture {EntryId} ignorée (opération de caisse source introuvable ou sans VatRate).",
                    entry.Id);
                continue;
            }

            var rate = (int)op.VatRate.Value;
            var vatDelta = entry.Lines
                .Where(l => l.AccountNumber == TunisianPostingAccounts.VatCollected)
                .Sum(l => l.CreditAmount.Amount - l.DebitAmount.Amount);
            var htDelta = entry.Lines
                .Where(l => l.AccountNumber == TunisianPostingAccounts.SalesOfGoods)
                .Sum(l => l.CreditAmount.Amount - l.DebitAmount.Amount);

            totalsByRate.TryGetValue(rate, out var current);
            totalsByRate[rate] = (current.HtBase + htDelta, current.VatAmount + vatDelta);
        }

        return totalsByRate
            .Select(kv => new CashSaleVatPosting(kv.Key, kv.Value.HtBase, kv.Value.VatAmount))
            .ToList();
    }
}
