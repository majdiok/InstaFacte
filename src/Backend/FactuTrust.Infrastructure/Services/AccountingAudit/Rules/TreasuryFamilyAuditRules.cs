using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Common.Interfaces.Treasury;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Audit;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services.AccountingAudit.Rules;

/// <summary>
/// Caisse créditrice.
///
/// <para>Une caisse ne peut pas être à découvert : on ne sort pas d'un tiroir plus d'espèces qu'il
/// n'en contient. Un solde cumulé négatif signale donc soit un encaissement oublié, soit une sortie
/// enregistrée à une date antérieure à son encaissement, soit une caisse alimentée hors
/// comptabilité. Les trois sont bloquants — c'est le premier point qu'un contrôleur fiscal
/// regarde.</para>
///
/// <para>Le calcul est un <b>cumul chronologique</b>, pas un solde de fin d'exercice : une caisse
/// peut boucler juste au 31 décembre tout en étant passée en négatif quinze fois dans l'année.
/// Seul le cumul jour par jour le montre.</para>
/// </summary>
public sealed class CashNegativeBalanceAuditRule : AccountingAuditRuleBase
{
    /// <summary>Journées en négatif détaillées par compte. Au-delà, le problème est systémique.</summary>
    private const int MaxDaysPerAccount = 60;

    public override string Code => "cash-negative";
    public override string ModuleCode => "treasury";
    public override int Category => (int)AnomalyCategory.Tresorerie;
    public override int DefaultSeverity => (int)PreClosingSeverity.Blocking;

    public override async Task<IReadOnlyList<AnomalyCandidate>> EvaluateAsync(
        IAuditEvaluationContext ctx, CancellationToken cancellationToken)
    {
        var c = ctx.Ctx();

        // Toutes les lignes de trésorerie jusqu'à la fin de l'exercice : le solde d'ouverture de
        // l'exercice compte, sinon la première journée paraîtrait à tort négative.
        var movements = await c.Db.JournalEntryLines.AsNoTracking()
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry.EntryDate <= ctx.YearEnd
                        && l.JournalEntry.Status != JournalEntryStatus.Brouillon
                        && l.AccountNumber.StartsWith(TreasuryAccountRoots.Cash))
            .Select(l => new
            {
                l.AccountNumber,
                l.JournalEntry.EntryDate,
                Debit = l.DebitAmount.Amount,
                Credit = l.CreditAmount.Amount
            })
            .ToListAsync(cancellationToken);

        if (movements.Count == 0) return Array.Empty<AnomalyCandidate>();

        var results = new List<AnomalyCandidate>();

        foreach (var account in movements.GroupBy(m => m.AccountNumber, StringComparer.Ordinal))
        {
            var running = 0m;
            var negativeDays = new List<(DateTime Date, decimal Balance)>();

            foreach (var day in account.GroupBy(m => m.EntryDate.Date).OrderBy(g => g.Key))
            {
                running = MillimeRounding.Round(running + day.Sum(m => m.Debit - m.Credit));

                // Seules les journées DE L'EXERCICE sont signalées ; les précédentes n'ont servi
                // qu'à établir le solde d'ouverture.
                if (running < 0 && day.Key.Year == ctx.FiscalYear)
                    negativeDays.Add((day.Key, running));
            }

            if (negativeDays.Count == 0) continue;

            var worst = negativeDays.MinBy(d => d.Balance);

            results.Add(SingleGroup(
                Code, ModuleCode, Category, DefaultSeverity,
                "Caisse créditrice",
                $"Compte {account.Key} : solde négatif sur {negativeDays.Count} journée(s), " +
                $"au plus bas {worst.Balance:N3} TND le {worst.Date:dd/MM/yyyy}.",
                "Une caisse ne peut pas être à découvert : encaissement manquant ou sortie antidatée.",
                accountRef: account.Key,
                amount: Math.Abs(worst.Balance),
                periodFrom: DateOnly.FromDateTime(negativeDays[0].Date),
                periodTo: DateOnly.FromDateTime(negativeDays[^1].Date),
                lines: negativeDays.Take(MaxDaysPerAccount).Select(d => new AnomalyLineCandidate(
                    null, null, d.Date, account.Key,
                    $"Solde de caisse au {d.Date:dd/MM/yyyy}",
                    0, Math.Abs(d.Balance), null, null)).ToList(),
                recommendations:
                [
                    "Rechercher les encaissements non enregistrés sur la période.",
                    "Vérifier les dates des sorties de caisse : une antidate suffit à créer le découvert."
                ],
                deepLinkRoute: "/accounting/ledger",
                discriminator: account.Key));
        }

        return results;
    }
}

/// <summary>
/// Encaissement sans rattachement documentaire.
///
/// <para>Une entrée de trésorerie doit se rattacher à quelque chose : une facture client, un
/// règlement lettré, un apport, un emprunt. Une entrée qui n'a ni document source ni contrepartie
/// client lettrée est un produit non justifié — au mieux une omission de lettrage, au pire une
/// recette hors comptabilité.</para>
///
/// <para><b>Ce que la règle ne fait pas.</b> Elle n'affirme pas la fraude : elle isole les entrées
/// que rien ne relie à une pièce. Les origines connues et légitimes — paie, emprunt, virement
/// interne, opération de caisse rattachée — sont exclues explicitement, faute de quoi la règle
/// signalerait la moitié du journal de banque.</para>
/// </summary>
public sealed class CashInWithoutDocumentAuditRule : AccountingAuditRuleBase
{
    private const int MaxDetailLines = 200;

    /// <summary>Origines d'écriture dont l'entrée de trésorerie est justifiée par construction.</summary>
    private static readonly string[] JustifiedSources =
    [
        AccountingService.SourceInvoice,
        AccountingService.SourcePayment,
        AccountingService.SourceSupplierPayment,
        AccountingService.SourceBankDeposit,
        AccountingService.SourceCashOperation,
        AccountingService.SourceOpeningBalance,
        AccountingService.SourcePayrollPayment,
        AccountingService.SourceCnssContributionPayment,
        AccountingService.SourceEffetSettlement,
        AccountingService.SourceFixedAssetDisposal
    ];

    public override string Code => "cash-in-without-invoice";
    public override string ModuleCode => "treasury";
    public override int Category => (int)AnomalyCategory.Tresorerie;
    public override int DefaultSeverity => (int)PreClosingSeverity.Warning;

    public override async Task<IReadOnlyList<AnomalyCandidate>> EvaluateAsync(
        IAuditEvaluationContext ctx, CancellationToken cancellationToken)
    {
        var c = ctx.Ctx();

        // Écritures de l'exercice portant au moins une entrée de trésorerie (débit d'un compte 5
        // hors virements internes 58, qui ne créent aucune richesse).
        var candidates = await c.Db.JournalEntries.AsNoTracking()
            .Include(e => e.Lines)
            .Where(e => e.EntryDate.Year == ctx.FiscalYear
                        && e.Status != JournalEntryStatus.Brouillon
                        && (e.SourceEntityType == null || !JustifiedSources.Contains(e.SourceEntityType))
                        && e.Lines.Any(l => l.AccountNumber.StartsWith("5")
                                            && !l.AccountNumber.StartsWith("58")
                                            && l.DebitAmount.Amount > 0))
            .Select(e => new
            {
                e.Id,
                e.EntryDate,
                e.JournalCode,
                e.EntryNumber,
                e.Label,
                CashIn = e.Lines
                    .Where(l => l.AccountNumber.StartsWith("5")
                                && !l.AccountNumber.StartsWith("58"))
                    .Sum(l => l.DebitAmount.Amount),
                // Une contrepartie client lettrée justifie l'encaissement.
                HasLetteredClient = e.Lines.Any(l => l.AccountNumber.StartsWith("411")
                                                     && l.LetteringCode != null
                                                     && l.LetteringCode != ""),
                // Un tiers rattaché suffit à rendre l'origine traçable.
                HasThirdParty = e.Lines.Any(l => l.ThirdPartyId != null)
            })
            .Take(MaxDetailLines * 2)
            .ToListAsync(cancellationToken);

        var unexplained = candidates
            .Where(e => e.CashIn > 0 && !e.HasLetteredClient && !e.HasThirdParty)
            .Take(MaxDetailLines)
            .ToList();

        if (unexplained.Count == 0) return Array.Empty<AnomalyCandidate>();

        return
        [
            SingleGroup(Code, ModuleCode, Category, DefaultSeverity,
                "Encaissement sans pièce ni tiers",
                $"{unexplained.Count} écriture(s) portent une entrée de trésorerie sans document " +
                "source, sans tiers rattaché et sans contrepartie client lettrée.",
                "Produit non justifié : risque de recette non déclarée en cas de contrôle.",
                accountRef: "5",
                amount: MillimeRounding.Round(unexplained.Sum(e => e.CashIn)),
                periodFrom: null,
                periodTo: null,
                lines: unexplained.Select(e => new AnomalyLineCandidate(
                    e.Id, null, e.EntryDate, "5", e.Label, e.CashIn, 0,
                    $"{e.JournalCode}-{e.EntryNumber}", null)).ToList(),
                recommendations:
                [
                    "Rattacher l'encaissement à sa facture et lettrer.",
                    "Documenter l'origine des apports et remboursements sans facture."
                ],
                deepLinkRoute: "/accounting/entry-search")
        ];
    }
}
