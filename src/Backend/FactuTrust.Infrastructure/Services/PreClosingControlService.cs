using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Contrôles de pré-clôture : batterie de vérifications de révision d'un exercice. Tous les
/// contrôles opèrent sur le validé (hors brouillard), sauf le contrôle « brouillons » lui-même.
/// </summary>
public sealed class PreClosingControlService : IPreClosingControlService
{
    private readonly ITenantDbContextFactory _contextFactory;
    private readonly IFixedAssetRepository _fixedAssets;
    private readonly AccountingSettings _settings;

    public PreClosingControlService(
        ITenantDbContextFactory contextFactory,
        IFixedAssetRepository fixedAssets,
        IOptions<AccountingSettings> settings)
    {
        _contextFactory = contextFactory;
        _fixedAssets = fixedAssets;
        _settings = settings.Value;
    }

    public async Task<Result<PreClosingChecklistDto>> RunAsync(int fiscalYear, CancellationToken cancellationToken = default)
    {
        if (fiscalYear is < 2000 or > 2100)
            return Result.Failure<PreClosingChecklistDto>(Error.Validation("FiscalYear", "Exercice invalide."));

        await using var ctx = _contextFactory.CreateContext();
        var yearEnd = new DateTime(fiscalYear, 12, 31);
        var checks = new List<PreClosingCheckDto>();

        // ── Bloquant : écritures en brouillard sur l'exercice ─────────────────────
        var drafts = await ctx.JournalEntries
            .CountAsync(e => e.EntryDate.Year == fiscalYear && e.Status == JournalEntryStatus.Brouillon, cancellationToken);
        checks.Add(new PreClosingCheckDto
        {
            Code = "drafts",
            Title = "Écritures en brouillard",
            Severity = (int)PreClosingSeverity.Blocking,
            Count = drafts,
            Message = drafts == 0
                ? "Aucune écriture en brouillard."
                : $"{drafts} écriture(s) en brouillard à valider (ou supprimer) avant la clôture.",
            DeepLinkRoute = drafts == 0 ? null : "/accounting/journal"
        });

        // ── Bloquant : écritures validées non équilibrées (défensif) ──────────────
        var unbalanced = await ctx.JournalEntryLines.AsNoTracking()
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry.EntryDate.Year == fiscalYear
                        && l.JournalEntry.Status != JournalEntryStatus.Brouillon)
            .GroupBy(l => l.JournalEntryId)
            .Select(g => new
            {
                Debit = g.Sum(l => l.DebitAmount.Amount),
                Credit = g.Sum(l => l.CreditAmount.Amount)
            })
            .CountAsync(x => x.Debit != x.Credit, cancellationToken);
        checks.Add(new PreClosingCheckDto
        {
            Code = "unbalanced",
            Title = "Écritures non équilibrées",
            Severity = (int)PreClosingSeverity.Blocking,
            Count = unbalanced,
            Message = unbalanced == 0
                ? "Toutes les écritures validées sont équilibrées."
                : $"{unbalanced} écriture(s) validée(s) déséquilibrée(s) (débit ≠ crédit).",
            DeepLinkRoute = unbalanced == 0 ? null : "/accounting/journal"
        });

        // ── Avertissement : comptes d'attente 471 non soldés (cumul à fin d'exercice) ──
        var suspense = await ctx.JournalEntryLines.AsNoTracking()
            .Include(l => l.JournalEntry)
            .Where(l => l.AccountNumber.StartsWith("471")
                        && l.JournalEntry.EntryDate <= yearEnd
                        && l.JournalEntry.Status != JournalEntryStatus.Brouillon)
            .GroupBy(l => l.AccountNumber)
            .Select(g => new { Account = g.Key, Net = g.Sum(l => l.DebitAmount.Amount) - g.Sum(l => l.CreditAmount.Amount) })
            .ToListAsync(cancellationToken);
        var suspenseOpen = suspense.Count(x => Math.Round(x.Net, 3) != 0);
        checks.Add(new PreClosingCheckDto
        {
            Code = "suspense",
            Title = "Comptes d'attente non soldés",
            Severity = (int)PreClosingSeverity.Warning,
            Count = suspenseOpen,
            Message = suspenseOpen == 0
                ? "Aucun compte 471 en attente non soldé."
                : $"{suspenseOpen} compte(s) d'attente 471 au solde non nul à régulariser.",
            DeepLinkRoute = suspenseOpen == 0 ? null : "/accounting/balance"
        });

        // ── Avertissement : lignes de tiers non lettrées et anciennes ─────────────
        var threshold = yearEnd.AddDays(-Math.Max(1, _settings.UnletteredAgeThresholdDays));
        var unlettered = await ctx.JournalEntryLines.AsNoTracking()
            .Include(l => l.JournalEntry)
            .CountAsync(l => (l.AccountNumber.StartsWith("4111") || l.AccountNumber.StartsWith("4011"))
                             && string.IsNullOrEmpty(l.LetteringCode)
                             && l.JournalEntry.EntryDate <= threshold
                             && l.JournalEntry.Status != JournalEntryStatus.Brouillon,
                cancellationToken);
        checks.Add(new PreClosingCheckDto
        {
            Code = "unlettered",
            Title = "Lettrage en suspens",
            Severity = (int)PreClosingSeverity.Warning,
            Count = unlettered,
            Message = unlettered == 0
                ? "Aucune ligne de tiers ancienne non lettrée."
                : $"{unlettered} ligne(s) de tiers (4111/4011) non lettrée(s) de plus de {_settings.UnletteredAgeThresholdDays} jours.",
            DeepLinkRoute = unlettered == 0 ? null : "/accounting/lettering"
        });

        // ── Avertissement : dotations aux amortissements non passées ──────────────
        var unposted = await _fixedAssets.GetUnpostedScheduleLinesForYearAsync(fiscalYear, cancellationToken);
        var unpostedCount = unposted.Count;
        checks.Add(new PreClosingCheckDto
        {
            Code = "depreciation",
            Title = "Dotations aux amortissements non passées",
            Severity = (int)PreClosingSeverity.Warning,
            Count = unpostedCount,
            Message = unpostedCount == 0
                ? "Toutes les dotations de l'exercice sont passées."
                : $"{unpostedCount} dotation(s) d'amortissement de l'exercice non comptabilisée(s).",
            DeepLinkRoute = unpostedCount == 0 ? null : "/accounting/fixed-assets/depreciation-run"
        });

        // ── Avertissement : mois avec activité TVA collectée mais sans déclaration ──
        var vatMonths = await ctx.JournalEntryLines.AsNoTracking()
            .Include(l => l.JournalEntry)
            .Where(l => l.AccountNumber.StartsWith("43671")
                        && l.JournalEntry.EntryDate.Year == fiscalYear
                        && l.JournalEntry.Status != JournalEntryStatus.Brouillon)
            .Select(l => l.JournalEntry.EntryDate.Month)
            .Distinct()
            .ToListAsync(cancellationToken);
        var declaredMonths = await ctx.VatDeclarations.AsNoTracking()
            .Where(d => d.Year == fiscalYear)
            .Select(d => d.Month)
            .ToListAsync(cancellationToken);
        var missingVat = vatMonths.Except(declaredMonths).Count();
        checks.Add(new PreClosingCheckDto
        {
            Code = "vat",
            Title = "Déclarations de TVA manquantes",
            Severity = (int)PreClosingSeverity.Warning,
            Count = missingVat,
            Message = missingVat == 0
                ? "Chaque mois avec TVA collectée a sa déclaration."
                : $"{missingVat} mois de l'exercice ont de la TVA collectée sans déclaration enregistrée.",
            DeepLinkRoute = missingVat == 0 ? null : "/accounting/vat-declaration"
        });

        // ── Avertissement : périodes de l'exercice non clôturées ──────────────────
        var openPeriods = await ctx.AccountingPeriods.AsNoTracking()
            .CountAsync(p => p.FiscalYear == fiscalYear && !p.IsClosed, cancellationToken);
        checks.Add(new PreClosingCheckDto
        {
            Code = "open-periods",
            Title = "Périodes non clôturées",
            Severity = (int)PreClosingSeverity.Warning,
            Count = openPeriods,
            Message = openPeriods == 0
                ? "Toutes les périodes de l'exercice sont clôturées."
                : $"{openPeriods} période(s) mensuelle(s) de l'exercice encore ouverte(s).",
            DeepLinkRoute = openPeriods == 0 ? null : "/accounting/closing"
        });

        // ── Info : trous dans les séquences de pièces par journal ─────────────────
        var sequences = await ctx.JournalEntries.AsNoTracking()
            .Where(e => e.EntryDate.Year == fiscalYear && e.Status != JournalEntryStatus.Brouillon)
            .GroupBy(e => e.JournalCode)
            .Select(g => new { Journal = g.Key, Min = g.Min(e => e.EntryNumber), Max = g.Max(e => e.EntryNumber), Count = g.Count() })
            .ToListAsync(cancellationToken);
        var gaps = sequences.Count(s => s.Max - s.Min + 1 != s.Count);
        checks.Add(new PreClosingCheckDto
        {
            Code = "sequence-gaps",
            Title = "Continuité de la numérotation",
            Severity = (int)PreClosingSeverity.Info,
            Count = gaps,
            Message = gaps == 0
                ? "Numérotation des pièces continue sur tous les journaux."
                : $"{gaps} journal(aux) présentent une discontinuité de numérotation (pièces annulées ?).",
            DeepLinkRoute = gaps == 0 ? null : "/accounting/entry-search"
        });

        var hasBlocking = checks.Any(c => c.Severity == (int)PreClosingSeverity.Blocking && c.Count > 0);

        return Result.Success(new PreClosingChecklistDto
        {
            FiscalYear = fiscalYear,
            GeneratedAt = DateTime.UtcNow,
            HasBlocking = hasBlocking,
            Checks = checks
        });
    }
}
