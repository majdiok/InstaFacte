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
/// Centre de contrôle d'intégrité comptable, LECTURE SEULE. Pour un exercice donné, réutilise
/// <b>à l'identique</b> les contrôles de pré-clôture (garantissant la parité) puis ajoute des
/// contrôles d'anomalies structurelles. Aucune mutation.
/// </summary>
public sealed class AccountingHealthService : IAccountingHealthService
{
    private readonly ITenantDbContextFactory _contextFactory;
    private readonly IPreClosingControlService _preClosing;
    private readonly AccountingSettings _settings;

    public AccountingHealthService(
        ITenantDbContextFactory contextFactory,
        IPreClosingControlService preClosing,
        IOptions<AccountingSettings> settings)
    {
        _contextFactory = contextFactory;
        _preClosing = preClosing;
        _settings = settings.Value;
    }

    public async Task<Result<AccountingHealthReportDto>> RunAsync(int? fiscalYear, CancellationToken cancellationToken = default)
    {
        if (!_settings.AccountingHealthEnabled)
            return Result.Failure<AccountingHealthReportDto>(
                Error.Validation("Health", "Le centre de contrôle d'intégrité n'est pas activé."));

        if (fiscalYear is < 2000 or > 2100)
            return Result.Failure<AccountingHealthReportDto>(Error.Validation("FiscalYear", "Exercice invalide."));

        var checks = new List<PreClosingCheckDto>();

        // Parité garantie : quand un exercice est ciblé, on réutilise TELS QUELS les contrôles de
        // pré-clôture (mêmes requêtes, mêmes comptes). Le centre de santé ne les réimplémente pas.
        if (fiscalYear.HasValue)
        {
            var preClosing = await _preClosing.RunAsync(fiscalYear.Value, cancellationToken);
            if (preClosing.IsFailure)
                return Result.Failure<AccountingHealthReportDto>(preClosing.Error);
            checks.AddRange(preClosing.Value.Checks);
        }

        // Contrôles d'intégrité structurelle (additifs) — portée : l'exercice ciblé, ou tout
        // l'historique quand fiscalYear est null. Ils examinent toutes les écritures persistées
        // (brouillons inclus) car ce sont des anomalies de données, pas des mesures financières.
        checks.AddRange(await BuildStructuralChecksAsync(fiscalYear, cancellationToken));

        var hasAnomalies = checks.Any(c => c.Count > 0);
        return Result.Success(new AccountingHealthReportDto
        {
            FiscalYear = fiscalYear,
            GeneratedAt = DateTime.UtcNow,
            HasAnomalies = hasAnomalies,
            Checks = checks
        });
    }

    private async Task<List<PreClosingCheckDto>> BuildStructuralChecksAsync(int? fiscalYear, CancellationToken ct)
    {
        await using var ctx = _contextFactory.CreateContext();
        var checks = new List<PreClosingCheckDto>();

        IQueryable<Domain.Entities.JournalEntry> Entries()
        {
            var q = ctx.JournalEntries.AsNoTracking();
            return fiscalYear.HasValue ? q.Where(e => e.EntryDate.Year == fiscalYear.Value) : q;
        }

        IQueryable<Domain.Entities.JournalEntryLine> Lines()
        {
            var q = ctx.JournalEntryLines.AsNoTracking().Include(l => l.JournalEntry);
            return fiscalYear.HasValue ? q.Where(l => l.JournalEntry.EntryDate.Year == fiscalYear.Value) : q;
        }

        // ── Comptes référencés hors plan comptable ────────────────────────────────
        var usedAccounts = await Lines().Select(l => l.AccountNumber).Distinct().ToListAsync(ct);
        var chartAccounts = await ctx.ChartOfAccounts.AsNoTracking().Select(c => c.AccountNumber).ToListAsync(ct);
        var chartSet = new HashSet<string>(chartAccounts, StringComparer.Ordinal);
        var orphanAccounts = usedAccounts.Count(a => !chartSet.Contains(a));
        checks.Add(new PreClosingCheckDto
        {
            Code = "health-orphan-accounts",
            Title = "Comptes hors plan comptable",
            Severity = (int)PreClosingSeverity.Blocking,
            Count = orphanAccounts,
            Message = orphanAccounts == 0
                ? "Toutes les écritures pointent vers des comptes du plan comptable."
                : $"{orphanAccounts} compte(s) mouvementé(s) absent(s) du plan comptable.",
            DeepLinkRoute = orphanAccounts == 0 ? null : "/accounting/chart"
        });

        // ── Écritures hors de toute période comptable ─────────────────────────────
        var periods = await ctx.AccountingPeriods.AsNoTracking()
            .Select(p => new { p.StartDate, p.EndDate })
            .ToListAsync(ct);
        var entryDates = await Entries().Select(e => e.EntryDate).ToListAsync(ct);
        var outOfPeriod = entryDates.Count(d => !periods.Any(p => p.StartDate <= d && d <= p.EndDate));
        checks.Add(new PreClosingCheckDto
        {
            Code = "health-out-of-period",
            Title = "Écritures hors période",
            Severity = (int)PreClosingSeverity.Warning,
            Count = outOfPeriod,
            Message = outOfPeriod == 0
                ? "Toutes les écritures sont rattachées à une période comptable ouverte ou clôturée."
                : $"{outOfPeriod} écriture(s) dont la date n'est couverte par aucune période comptable.",
            DeepLinkRoute = outOfPeriod == 0 ? null : "/accounting/closing"
        });

        // ── Doublons de numéro de pièce (même journal + même numéro) ──────────────
        var pieceDuplicates = await Entries()
            .GroupBy(e => new { e.JournalCode, e.EntryNumber })
            .Select(g => g.Count())
            .CountAsync(c => c > 1, ct);
        checks.Add(new PreClosingCheckDto
        {
            Code = "health-piece-duplicates",
            Title = "Doublons de numéro de pièce",
            Severity = (int)PreClosingSeverity.Warning,
            Count = pieceDuplicates,
            Message = pieceDuplicates == 0
                ? "Aucun numéro de pièce dupliqué au sein d'un journal."
                : $"{pieceDuplicates} couple(s) journal+numéro de pièce en doublon.",
            DeepLinkRoute = pieceDuplicates == 0 ? null : "/accounting/entry-search"
        });

        // ── Tiers portés par un compte non auxiliarisable ─────────────────────────
        // Une ligne portant un ThirdPartyId doit être sur un compte de tiers (classe 40/41).
        var mislinkedThirdParty = await Lines()
            .CountAsync(l => l.ThirdPartyId != null
                             && !l.AccountNumber.StartsWith("41")
                             && !l.AccountNumber.StartsWith("40"), ct);
        checks.Add(new PreClosingCheckDto
        {
            Code = "health-thirdparty-mislink",
            Title = "Tiers sur compte non auxiliarisable",
            Severity = (int)PreClosingSeverity.Info,
            Count = mislinkedThirdParty,
            Message = mislinkedThirdParty == 0
                ? "Toutes les lignes avec tiers sont sur un compte client/fournisseur (40/41)."
                : $"{mislinkedThirdParty} ligne(s) rattachée(s) à un tiers mais sur un compte non-tiers.",
            DeepLinkRoute = mislinkedThirdParty == 0 ? null : "/accounting/entry-search"
        });

        return checks;
    }
}
