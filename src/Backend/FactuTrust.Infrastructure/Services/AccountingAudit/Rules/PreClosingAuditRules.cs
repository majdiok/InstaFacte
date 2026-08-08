using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Audit;
using FactuTrust.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services.AccountingAudit.Rules;

internal static class AuditRuleContextExtensions
{
    public static AuditEvaluationContextImpl Ctx(this IAuditEvaluationContext ctx) =>
        (AuditEvaluationContextImpl)ctx;
}

public sealed class DraftEntriesAuditRule : AccountingAuditRuleBase
{
    public override string Code => "drafts";
    public override string ModuleCode => "integrity";
    public override int Category => (int)AnomalyCategory.Ecritures;
    public override int DefaultSeverity => (int)PreClosingSeverity.Blocking;

    public override async Task<IReadOnlyList<AnomalyCandidate>> EvaluateAsync(
        IAuditEvaluationContext ctx, CancellationToken cancellationToken)
    {
        var c = ctx.Ctx();
        var entries = await c.Db.JournalEntries.AsNoTracking()
            .Where(e => e.EntryDate.Year == ctx.FiscalYear && e.Status == JournalEntryStatus.Brouillon)
            .OrderBy(e => e.EntryDate)
            .ToListAsync(cancellationToken);
        if (entries.Count == 0) return Array.Empty<AnomalyCandidate>();

        var lines = entries.Select(e => new AnomalyLineCandidate(
            e.Id, null, e.EntryDate, null, e.Label, 0, 0, $"{e.JournalCode}-{e.EntryNumber}", null)).ToList();
        var amount = 0m;
        return
        [
            SingleGroup(Code, ModuleCode, Category, DefaultSeverity,
                "Écritures en brouillard",
                $"{entries.Count} écriture(s) en brouillard sans validation.",
                "Risque d'erreur significatif sur les états financiers et non-conformité.",
                null, amount, DateOnly.FromDateTime(ctx.PeriodFrom.ToDateTime(TimeOnly.MinValue)),
                DateOnly.FromDateTime(ctx.PeriodTo.ToDateTime(TimeOnly.MinValue)),
                lines,
                ["Valider ou supprimer les écritures en brouillard.", "Joindre les pièces justificatives manquantes."],
                "/accounting/journal")
        ];
    }
}

public sealed class UnbalancedEntriesAuditRule : AccountingAuditRuleBase
{
    public override string Code => "unbalanced";
    public override string ModuleCode => "integrity";
    public override int Category => (int)AnomalyCategory.Ecritures;
    public override int DefaultSeverity => (int)PreClosingSeverity.Blocking;

    public override async Task<IReadOnlyList<AnomalyCandidate>> EvaluateAsync(
        IAuditEvaluationContext ctx, CancellationToken cancellationToken)
    {
        var c = ctx.Ctx();
        var unbalanced = await c.Db.JournalEntryLines.AsNoTracking()
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry.EntryDate.Year == ctx.FiscalYear
                        && l.JournalEntry.Status != JournalEntryStatus.Brouillon)
            .GroupBy(l => l.JournalEntryId)
            .Select(g => new
            {
                EntryId = g.Key,
                Debit = g.Sum(l => l.DebitAmount.Amount),
                Credit = g.Sum(l => l.CreditAmount.Amount),
                Journal = g.First().JournalEntry.JournalCode,
                Number = g.First().JournalEntry.EntryNumber,
                Date = g.First().JournalEntry.EntryDate
            })
            .Where(x => x.Debit != x.Credit)
            .ToListAsync(cancellationToken);
        if (unbalanced.Count == 0) return Array.Empty<AnomalyCandidate>();

        var lines = unbalanced.Select(u => new AnomalyLineCandidate(
            u.EntryId, null, u.Date, null, null, u.Debit, u.Credit, $"{u.Journal}-{u.Number}", null)).ToList();
        return
        [
            SingleGroup(Code, ModuleCode, Category, DefaultSeverity,
                "Écritures non équilibrées",
                $"{unbalanced.Count} écriture(s) validée(s) déséquilibrée(s).",
                "Les états financiers sont faussés tant que le déséquilibre persiste.",
                null, unbalanced.Sum(u => Math.Abs(u.Debit - u.Credit)), null, null, lines,
                ["Corriger les montants débit/crédit.", "Contre-passer l'écriture si nécessaire."],
                "/accounting/journal")
        ];
    }
}

public sealed class SuspenseAccountsAuditRule : AccountingAuditRuleBase
{
    public override string Code => "suspense";
    public override string ModuleCode => "suspense";
    public override int Category => (int)AnomalyCategory.Comptes;
    public override int DefaultSeverity => (int)PreClosingSeverity.Warning;

    public override async Task<IReadOnlyList<AnomalyCandidate>> EvaluateAsync(
        IAuditEvaluationContext ctx, CancellationToken cancellationToken)
    {
        var c = ctx.Ctx();
        var suspense = await c.Db.JournalEntryLines.AsNoTracking()
            .Include(l => l.JournalEntry)
            .Where(l => l.AccountNumber.StartsWith("471")
                        && l.JournalEntry.EntryDate <= ctx.YearEnd
                        && l.JournalEntry.Status != JournalEntryStatus.Brouillon)
            .GroupBy(l => l.AccountNumber)
            .Select(g => new { Account = g.Key, Net = g.Sum(l => l.DebitAmount.Amount) - g.Sum(l => l.CreditAmount.Amount) })
            .ToListAsync(cancellationToken);
        var open = suspense.Where(x => Math.Round(x.Net, 3) != 0).ToList();
        if (open.Count == 0) return Array.Empty<AnomalyCandidate>();

        return open.Select(a => SingleGroup(
            Code, ModuleCode, Category, DefaultSeverity,
            "Comptes d'attente non soldés",
            $"Compte {a.Account} au solde non nul ({a.Net:N3} TND).",
            "Les comptes d'attente doivent être soldés en fin d'exercice.",
            a.Account, Math.Abs(a.Net), null, null,
            [new AnomalyLineCandidate(null, null, null, a.Account, "Solde compte d'attente", a.Net > 0 ? a.Net : 0, a.Net < 0 ? -a.Net : 0, null, null)],
            ["Identifier l'origine du solde.", "Passer l'écriture de régularisation."],
            "/accounting/balance")).ToList();
    }
}

public sealed class UnletteredLinesAuditRule : AccountingAuditRuleBase
{
    public override string Code => "unlettered";
    public override string ModuleCode => "lettering";
    public override int Category => (int)AnomalyCategory.Lettrage;
    public override int DefaultSeverity => (int)PreClosingSeverity.Warning;

    public override async Task<IReadOnlyList<AnomalyCandidate>> EvaluateAsync(
        IAuditEvaluationContext ctx, CancellationToken cancellationToken)
    {
        var c = ctx.Ctx();
        var threshold = ctx.YearEnd.AddDays(-Math.Max(1, ctx.Settings.UnletteredAgeThresholdDays));
        var lines = await c.Db.JournalEntryLines.AsNoTracking()
            .Include(l => l.JournalEntry)
            .Where(l => (l.AccountNumber.StartsWith("4111") || l.AccountNumber.StartsWith("4011"))
                        && string.IsNullOrEmpty(l.LetteringCode)
                        && l.JournalEntry.EntryDate <= threshold
                        && l.JournalEntry.Status != JournalEntryStatus.Brouillon)
            .Select(l => new { l.Id, l.JournalEntryId, l.JournalEntry.EntryDate, l.AccountNumber, l.Label, Debit = l.DebitAmount.Amount, Credit = l.CreditAmount.Amount })
            .Take(500)
            .ToListAsync(cancellationToken);
        if (lines.Count == 0) return Array.Empty<AnomalyCandidate>();

        var total = await c.Db.JournalEntryLines.AsNoTracking()
            .Include(l => l.JournalEntry)
            .CountAsync(l => (l.AccountNumber.StartsWith("4111") || l.AccountNumber.StartsWith("4011"))
                             && string.IsNullOrEmpty(l.LetteringCode)
                             && l.JournalEntry.EntryDate <= threshold
                             && l.JournalEntry.Status != JournalEntryStatus.Brouillon, cancellationToken);

        return
        [
            SingleGroup(Code, ModuleCode, Category, DefaultSeverity,
                "Lettrage en suspens",
                $"{total} ligne(s) de tiers non lettrée(s) de plus de {ctx.Settings.UnletteredAgeThresholdDays} jours.",
                "Risque d'erreur sur les soldes clients/fournisseurs.",
                "4111/4011", lines.Sum(l => l.Debit + l.Credit), null, null,
                lines.Select(l => new AnomalyLineCandidate(l.JournalEntryId, l.Id, l.EntryDate, l.AccountNumber, l.Label, l.Debit, l.Credit, null, null)).ToList(),
                ["Lettrer les règlements avec les factures.", "Vérifier les paiements non affectés."],
                "/accounting/lettering")
        ];
    }
}

public sealed class DepreciationAuditRule : AccountingAuditRuleBase
{
    private readonly IFixedAssetRepository _fixedAssets;

    public DepreciationAuditRule(IFixedAssetRepository fixedAssets) => _fixedAssets = fixedAssets;

    public override string Code => "depreciation";
    public override string ModuleCode => "fixed-assets";
    public override int Category => (int)AnomalyCategory.Immobilisations;
    public override int DefaultSeverity => (int)PreClosingSeverity.Warning;

    public override async Task<IReadOnlyList<AnomalyCandidate>> EvaluateAsync(
        IAuditEvaluationContext ctx, CancellationToken cancellationToken)
    {
        var unposted = await _fixedAssets.GetUnpostedScheduleLinesForYearAsync(ctx.FiscalYear, cancellationToken);
        if (unposted.Count == 0) return Array.Empty<AnomalyCandidate>();
        return
        [
            SingleGroup(Code, ModuleCode, Category, DefaultSeverity,
                "Dotations aux amortissements non passées",
                $"{unposted.Count} dotation(s) d'amortissement non comptabilisée(s).",
                "Sous-estimation des charges et non-conformité fiscale.",
                null, unposted.Sum(u => u.DepreciationAmount), null, null,
                unposted.Select(u => new AnomalyLineCandidate(
                    null, null,
                    u.PeriodMonth.HasValue ? new DateTime(u.FiscalYear, u.PeriodMonth.Value, 1) : new DateTime(u.FiscalYear, 12, 31),
                    null, $"Immobilisation {u.FixedAssetId:N}", u.DepreciationAmount, 0, null, null)).ToList(),
                ["Lancer le calcul des dotations.", "Comptabiliser les écritures d'amortissement."],
                "/accounting/fixed-assets/depreciation-run")
        ];
    }
}

public sealed class VatMissingDeclarationAuditRule : AccountingAuditRuleBase
{
    public override string Code => "vat";
    public override string ModuleCode => "vat";
    public override int Category => (int)AnomalyCategory.Tva;
    public override int DefaultSeverity => (int)PreClosingSeverity.Warning;

    public override async Task<IReadOnlyList<AnomalyCandidate>> EvaluateAsync(
        IAuditEvaluationContext ctx, CancellationToken cancellationToken)
    {
        var c = ctx.Ctx();
        var vatMonths = await c.Db.JournalEntryLines.AsNoTracking()
            .Include(l => l.JournalEntry)
            .Where(l => l.AccountNumber.StartsWith("43671")
                        && l.JournalEntry.EntryDate.Year == ctx.FiscalYear
                        && l.JournalEntry.Status != JournalEntryStatus.Brouillon)
            .Select(l => l.JournalEntry.EntryDate.Month)
            .Distinct()
            .ToListAsync(cancellationToken);
        var declaredMonths = await c.Db.VatDeclarations.AsNoTracking()
            .Where(d => d.Year == ctx.FiscalYear)
            .Select(d => d.Month)
            .ToListAsync(cancellationToken);
        var missing = vatMonths.Except(declaredMonths).OrderBy(m => m).ToList();
        if (missing.Count == 0) return Array.Empty<AnomalyCandidate>();

        return
        [
            SingleGroup(Code, ModuleCode, Category, DefaultSeverity,
                "Déclarations de TVA manquantes",
                $"{missing.Count} mois avec TVA collectée sans déclaration.",
                "Risque de pénalités fiscales.",
                "43671", 0, null, null,
                missing.Select(m => new AnomalyLineCandidate(null, null, new DateTime(ctx.FiscalYear, m, 1), "43671", $"Mois {m:D2}", 0, 0, null, null)).ToList(),
                ["Enregistrer la déclaration mensuelle.", "Vérifier la cohérence compta / déclaration."],
                "/accounting/vat-declaration")
        ];
    }
}

public sealed class OpenPeriodsAuditRule : AccountingAuditRuleBase
{
    public override string Code => "open-periods";
    public override string ModuleCode => "integrity";
    public override int Category => (int)AnomalyCategory.Integrite;
    public override int DefaultSeverity => (int)PreClosingSeverity.Warning;

    public override async Task<IReadOnlyList<AnomalyCandidate>> EvaluateAsync(
        IAuditEvaluationContext ctx, CancellationToken cancellationToken)
    {
        var c = ctx.Ctx();
        var open = await c.Db.AccountingPeriods.AsNoTracking()
            .Where(p => p.FiscalYear == ctx.FiscalYear && !p.IsClosed)
            .ToListAsync(cancellationToken);
        if (open.Count == 0) return Array.Empty<AnomalyCandidate>();

        return
        [
            SingleGroup(Code, ModuleCode, Category, DefaultSeverity,
                "Périodes non clôturées",
                $"{open.Count} période(s) mensuelle(s) encore ouverte(s).",
                "La clôture annuelle nécessite des périodes mensuelles clôturées.",
                null, 0, null, null,
                open.Select(p => new AnomalyLineCandidate(null, null, p.StartDate, null, $"Période {p.StartDate:MM/yyyy}", 0, 0, null, null)).ToList(),
                ["Clôturer les périodes mensuelles.", "Vérifier les écritures de la période."],
                "/accounting/closing")
        ];
    }
}

public sealed class SequenceGapsAuditRule : AccountingAuditRuleBase
{
    public override string Code => "sequence-gaps";
    public override string ModuleCode => "integrity";
    public override int Category => (int)AnomalyCategory.Integrite;
    public override int DefaultSeverity => (int)PreClosingSeverity.Info;

    public override async Task<IReadOnlyList<AnomalyCandidate>> EvaluateAsync(
        IAuditEvaluationContext ctx, CancellationToken cancellationToken)
    {
        var c = ctx.Ctx();
        var sequences = await c.Db.JournalEntries.AsNoTracking()
            .Where(e => e.EntryDate.Year == ctx.FiscalYear && e.Status != JournalEntryStatus.Brouillon)
            .GroupBy(e => e.JournalCode)
            .Select(g => new { Journal = g.Key, Min = g.Min(e => e.EntryNumber), Max = g.Max(e => e.EntryNumber), Count = g.Count() })
            .ToListAsync(cancellationToken);
        var gaps = sequences.Where(s => s.Max - s.Min + 1 != s.Count).ToList();
        if (gaps.Count == 0) return Array.Empty<AnomalyCandidate>();

        return
        [
            SingleGroup(Code, ModuleCode, Category, DefaultSeverity,
                "Continuité de la numérotation",
                $"{gaps.Count} journal(aux) avec discontinuité de numérotation.",
                "Peut indiquer des pièces annulées ou des trous de numérotation.",
                null, 0, null, null,
                gaps.Select(g => new AnomalyLineCandidate(null, null, null, null, $"Journal {g.Journal}", 0, 0, $"{g.Min}-{g.Max}", null)).ToList(),
                ["Vérifier les pièces annulées.", "Documenter les trous de numérotation."],
                "/accounting/entry-search")
        ];
    }
}
