using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Audit;
using FactuTrust.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services.AccountingAudit.Rules;

public sealed class OrphanAccountsAuditRule : AccountingAuditRuleBase
{
    public override string Code => "health-orphan-accounts";
    public override string ModuleCode => "integrity";
    public override int Category => (int)AnomalyCategory.Comptes;
    public override int DefaultSeverity => (int)PreClosingSeverity.Blocking;

    public override async Task<IReadOnlyList<AnomalyCandidate>> EvaluateAsync(
        IAuditEvaluationContext ctx, CancellationToken cancellationToken)
    {
        var c = ctx.Ctx();
        var usedAccounts = await c.Db.JournalEntryLines.AsNoTracking()
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry.EntryDate.Year == ctx.FiscalYear)
            .Select(l => l.AccountNumber)
            .Distinct()
            .ToListAsync(cancellationToken);
        var chartAccounts = await c.Db.ChartOfAccounts.AsNoTracking()
            .Select(a => a.AccountNumber)
            .ToListAsync(cancellationToken);
        var chartSet = new HashSet<string>(chartAccounts, StringComparer.Ordinal);
        var orphans = usedAccounts.Where(a => !chartSet.Contains(a)).ToList();
        if (orphans.Count == 0) return Array.Empty<AnomalyCandidate>();

        return orphans.Select(account => SingleGroup(
            Code, ModuleCode, Category, DefaultSeverity,
            "Compte hors plan comptable",
            $"Le compte {account} est mouvementé mais absent du plan comptable.",
            "Risque d'erreur sur les états et non-conformité.",
            account, 0, null, null,
            [new AnomalyLineCandidate(null, null, null, account, "Compte orphelin", 0, 0, null, null)],
            ["Créer le compte dans le plan comptable.", "Remplacer le compte sur les écritures."],
            "/accounting/chart")).ToList();
    }
}

public sealed class OutOfPeriodAuditRule : AccountingAuditRuleBase
{
    public override string Code => "health-out-of-period";
    public override string ModuleCode => "integrity";
    public override int Category => (int)AnomalyCategory.Ecritures;
    public override int DefaultSeverity => (int)PreClosingSeverity.Warning;

    public override async Task<IReadOnlyList<AnomalyCandidate>> EvaluateAsync(
        IAuditEvaluationContext ctx, CancellationToken cancellationToken)
    {
        var c = ctx.Ctx();
        var periods = await c.Db.AccountingPeriods.AsNoTracking()
            .Select(p => new { p.StartDate, p.EndDate })
            .ToListAsync(cancellationToken);
        var entries = await c.Db.JournalEntries.AsNoTracking()
            .Where(e => e.EntryDate.Year == ctx.FiscalYear)
            .Select(e => new { e.Id, e.EntryDate, e.JournalCode, e.EntryNumber, e.Label })
            .ToListAsync(cancellationToken);
        var outOfPeriod = entries
            .Where(e => !periods.Any(p => p.StartDate <= e.EntryDate && e.EntryDate <= p.EndDate))
            .ToList();
        if (outOfPeriod.Count == 0) return Array.Empty<AnomalyCandidate>();

        return
        [
            SingleGroup(Code, ModuleCode, Category, DefaultSeverity,
                "Écritures hors période",
                $"{outOfPeriod.Count} écriture(s) hors période comptable.",
                "Les écritures doivent être rattachées à une période ouverte ou clôturée.",
                null, 0, null, null,
                outOfPeriod.Select(e => new AnomalyLineCandidate(
                    e.Id, null, e.EntryDate, null, e.Label, 0, 0, $"{e.JournalCode}-{e.EntryNumber}", null)).ToList(),
                ["Créer la période comptable manquante.", "Corriger la date de l'écriture."],
                "/accounting/closing")
        ];
    }
}

public sealed class PieceDuplicatesAuditRule : AccountingAuditRuleBase
{
    public override string Code => "health-piece-duplicates";
    public override string ModuleCode => "integrity";
    public override int Category => (int)AnomalyCategory.Ecritures;
    public override int DefaultSeverity => (int)PreClosingSeverity.Warning;

    public override async Task<IReadOnlyList<AnomalyCandidate>> EvaluateAsync(
        IAuditEvaluationContext ctx, CancellationToken cancellationToken)
    {
        var c = ctx.Ctx();
        var duplicates = await c.Db.JournalEntries.AsNoTracking()
            .Where(e => e.EntryDate.Year == ctx.FiscalYear)
            .GroupBy(e => new { e.JournalCode, e.EntryNumber })
            .Where(g => g.Count() > 1)
            .Select(g => new { g.Key.JournalCode, g.Key.EntryNumber, Count = g.Count() })
            .ToListAsync(cancellationToken);
        if (duplicates.Count == 0) return Array.Empty<AnomalyCandidate>();

        return duplicates.Select(d => SingleGroup(
            Code, ModuleCode, Category, DefaultSeverity,
            "Doublon de numéro de pièce",
            $"Journal {d.JournalCode} — pièce {d.EntryNumber} en {d.Count} exemplaires.",
            "Risque de confusion et d'audit défavorable.",
            null, 0, null, null,
            [new AnomalyLineCandidate(null, null, null, null, $"{d.JournalCode}-{d.EntryNumber}", 0, 0, null, null)],
            ["Identifier la pièce correcte.", "Renommer ou supprimer le doublon."],
            "/accounting/entry-search")).ToList();
    }
}

public sealed class ThirdPartyMislinkAuditRule : AccountingAuditRuleBase
{
    public override string Code => "health-thirdparty-mislink";
    public override string ModuleCode => "integrity";
    public override int Category => (int)AnomalyCategory.Comptes;
    public override int DefaultSeverity => (int)PreClosingSeverity.Info;

    public override async Task<IReadOnlyList<AnomalyCandidate>> EvaluateAsync(
        IAuditEvaluationContext ctx, CancellationToken cancellationToken)
    {
        var c = ctx.Ctx();
        var lines = await c.Db.JournalEntryLines.AsNoTracking()
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry.EntryDate.Year == ctx.FiscalYear
                        && l.ThirdPartyId != null
                        && !l.AccountNumber.StartsWith("41")
                        && !l.AccountNumber.StartsWith("40"))
            .Select(l => new { l.Id, l.JournalEntryId, l.JournalEntry.EntryDate, l.AccountNumber, l.Label, Debit = l.DebitAmount.Amount, Credit = l.CreditAmount.Amount })
            .Take(200)
            .ToListAsync(cancellationToken);
        if (lines.Count == 0) return Array.Empty<AnomalyCandidate>();

        var total = await c.Db.JournalEntryLines.AsNoTracking()
            .Include(l => l.JournalEntry)
            .CountAsync(l => l.JournalEntry.EntryDate.Year == ctx.FiscalYear
                             && l.ThirdPartyId != null
                             && !l.AccountNumber.StartsWith("41")
                             && !l.AccountNumber.StartsWith("40"), cancellationToken);

        return
        [
            SingleGroup(Code, ModuleCode, Category, DefaultSeverity,
                "Tiers sur compte non auxiliarisable",
                $"{total} ligne(s) avec tiers sur compte hors 40/41.",
                "Les tiers doivent être sur des comptes clients/fournisseurs.",
                null, lines.Sum(l => l.Debit + l.Credit), null, null,
                lines.Select(l => new AnomalyLineCandidate(l.JournalEntryId, l.Id, l.EntryDate, l.AccountNumber, l.Label, l.Debit, l.Credit, null, null)).ToList(),
                ["Corriger le compte ou retirer le tiers.", "Utiliser le plan tiers."],
                "/accounting/entry-search")
        ];
    }
}
