using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Ancrage d'exercice partagé entre <see cref="AccountingReportingService"/> (grand livre, balance,
/// états NCT — T7) et <see cref="AccountingService"/> (génération des à-nouveaux — T8), extrait pour
/// éviter la duplication de la logique d'ancrage entre les deux services.
/// <para>
/// Quand l'exercice porte une écriture d'à-nouveau (<c>SourceOpeningBalance</c>), celle-ci RÉSUME les
/// soldes des exercices antérieurs. L'ouverture doit alors partir de cette écriture, et non de tout
/// l'historique : sinon les soldes reportés sont comptés deux fois — une fois par l'historique
/// conservé en base, une fois par l'à-nouveau lui-même.
/// </para>
/// <para>
/// Sans à-nouveau sur l'exercice, l'ancrage est NEUTRE (<see cref="FiscalAnchor.OpeningEntryId"/> nul)
/// et les calculs conservent le comportement cumulatif historique.
/// </para>
/// </summary>
internal static class FiscalAnchorHelper
{
    public readonly record struct FiscalAnchor(DateTime FiscalYearStart, Guid? OpeningEntryId)
    {
        /// <summary>Vrai quand l'exercice porte un à-nouveau : l'ouverture s'ancre sur lui.</summary>
        public bool IsAnchored => OpeningEntryId.HasValue;
    }

    public static async Task<FiscalAnchor> ResolveFiscalAnchorAsync(
        Persistence.TenantDbContext ctx, DateTime from, CancellationToken ct)
    {
        var fiscalYearStart = new DateTime(from.Year, 1, 1);
        var fiscalYearEnd = new DateTime(from.Year, 12, 31);

        // L'à-nouveau est unique par exercice (idempotence garantie à la génération).
        var openingEntryId = await ctx.JournalEntries.AsNoTracking()
            .Where(e => e.SourceEntityType == AccountingService.SourceOpeningBalance
                        && e.EntryDate >= fiscalYearStart
                        && e.EntryDate <= fiscalYearEnd)
            .Select(e => (Guid?)e.Id)
            .FirstOrDefaultAsync(ct);

        return new FiscalAnchor(fiscalYearStart, openingEntryId);
    }

    /// <summary>
    /// Lignes constituant l'ouverture : l'à-nouveau de l'exercice plus les mouvements de l'exercice
    /// antérieurs à la période (ancrage actif), ou tout l'historique antérieur (comportement
    /// historique, ancrage neutre).
    /// </summary>
    public static IQueryable<JournalEntryLine> OpeningLines(
        IQueryable<JournalEntryLine> source, FiscalAnchor anchor, DateTime from)
    {
        if (!anchor.IsAnchored)
            return source.Where(l => l.JournalEntry.EntryDate < from);

        var openingEntryId = anchor.OpeningEntryId!.Value;
        var fiscalYearStart = anchor.FiscalYearStart;
        return source.Where(l => l.JournalEntryId == openingEntryId
                                 || (l.JournalEntry.EntryDate >= fiscalYearStart
                                     && l.JournalEntry.EntryDate < from));
    }

    /// <summary>
    /// Lignes de mouvement de la période. Quand l'ancrage est actif, les écritures d'à-nouveau en
    /// sont exclues : celle de l'exercice est déjà comptée dans l'ouverture, et celles des exercices
    /// suivants (période à cheval sur plusieurs exercices) ne sont que des reports.
    /// </summary>
    public static IQueryable<JournalEntryLine> MovementLines(
        IQueryable<JournalEntryLine> source, FiscalAnchor anchor, DateTime from, DateTime to)
    {
        var movements = source.Where(l => l.JournalEntry.EntryDate >= from && l.JournalEntry.EntryDate <= to);
        return anchor.IsAnchored
            ? movements.Where(l => l.JournalEntry.SourceEntityType != AccountingService.SourceOpeningBalance)
            : movements;
    }

    /// <summary>Solde agrégé d'un compte (+ tiers), signé débit − crédit (T8).</summary>
    public readonly record struct AnchoredBalance(
        string AccountNumber, Guid? ThirdPartyId, ThirdPartyKind ThirdPartyKind, decimal TotalDebit, decimal TotalCredit);

    /// <summary>
    /// Soldes de clôture au 31/12/<paramref name="fiscalYear"/>, ancrés sur l'à-nouveau de
    /// l'exercice s'il existe (T8, corrige le double comptage de <c>GenerateOpeningEntriesAsync</c>) :
    /// classes 1-5 = ouverture ancrée (<see cref="OpeningLines"/>) + mouvements de l'exercice
    /// (<see cref="MovementLines"/>) ; classes 6-7 = mouvements de l'exercice SEULS, jamais
    /// d'ouverture — le résultat ne se cumule pas d'un exercice à l'autre, il est transféré en
    /// 131/135 par l'à-nouveau qui clôt l'exercice précédent. Exclut toujours les brouillons.
    /// Groupé par compte + tiers (nécessaire pour porter le tiers sur la balance auxiliaire).
    /// </summary>
    public static async Task<IReadOnlyList<AnchoredBalance>> ComputeAnchoredClosingBalancesAsync(
        Persistence.TenantDbContext ctx, int fiscalYear, CancellationToken cancellationToken)
    {
        var fiscalYearStart = new DateTime(fiscalYear, 1, 1);
        var fiscalYearEnd = new DateTime(fiscalYear, 12, 31);
        var anchor = await ResolveFiscalAnchorAsync(ctx, fiscalYearStart, cancellationToken);

        IQueryable<JournalEntryLine> Base() => ctx.JournalEntryLines.AsNoTracking()
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry.Status != JournalEntryStatus.Brouillon);

        static async Task<List<AnchoredBalance>> GroupAsync(IQueryable<JournalEntryLine> q, CancellationToken ct) =>
            await q.GroupBy(l => new { l.AccountNumber, l.ThirdPartyId, l.ThirdPartyKind })
                .Select(g => new AnchoredBalance(
                    g.Key.AccountNumber, g.Key.ThirdPartyId, g.Key.ThirdPartyKind,
                    g.Sum(l => l.DebitAmount.Amount), g.Sum(l => l.CreditAmount.Amount)))
                .ToListAsync(ct);

        var opening = await GroupAsync(OpeningLines(Base(), anchor, fiscalYearStart), cancellationToken);
        var movement = await GroupAsync(MovementLines(Base(), anchor, fiscalYearStart, fiscalYearEnd), cancellationToken);

        var merged = new Dictionary<(string, Guid?, ThirdPartyKind), (decimal Debit, decimal Credit)>();
        void Add(AnchoredBalance b)
        {
            var key = (b.AccountNumber, b.ThirdPartyId, b.ThirdPartyKind);
            merged.TryGetValue(key, out var cur);
            merged[key] = (cur.Debit + b.TotalDebit, cur.Credit + b.TotalCredit);
        }

        static char Cls(string a) => a.Length > 0 ? a[0] : ' ';

        // Classes 6-7 : jamais d'ouverture (voir doc ci-dessus) — on ignore la composante
        // « opening » pour ces comptes, seule la composante « movement » de l'exercice compte.
        foreach (var b in opening)
        {
            var cls = Cls(b.AccountNumber);
            if (cls == '6' || cls == '7') continue;
            Add(b);
        }
        foreach (var b in movement)
            Add(b);

        return merged
            .Select(kv => new AnchoredBalance(kv.Key.Item1, kv.Key.Item2, kv.Key.Item3, kv.Value.Debit, kv.Value.Credit))
            .ToList();
    }
}
