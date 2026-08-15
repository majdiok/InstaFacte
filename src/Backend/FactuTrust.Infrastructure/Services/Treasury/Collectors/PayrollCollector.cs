using FactuTrust.Application.Common.Interfaces.Treasury;
using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services.Treasury.Collectors;

/// <summary>
/// Décaissements de paie : net à payer des cycles arrêtés, cycles à venir projetés, et charges
/// sociales et retenues quand l'échéancier fiscal ne les porte pas déjà.
/// </summary>
/// <remarks>
/// <para>
/// <b>Anti-double-comptage, règle n° 1 — dette constatée contre cycle projeté.</b> Un mois qui
/// possède déjà un cycle de paie (quel que soit son statut) n'est jamais projeté : seul le reste
/// dû réel de ce cycle est retenu. La projection ne concerne que les mois sans cycle.
/// </para>
/// <para>
/// <b>Anti-double-comptage, règle n° 2 — charges sociales contre échéancier fiscal.</b> La CNSS,
/// l'IRPP et la CSS retenus sur salaires figurent déjà dans l'échéancier fiscal
/// (<c>CnssMonthlyRemittance</c>, <c>CnssDtsQuarterly</c>, <c>PayrollIrppWithholding</c>) dès que
/// l'exercice a été généré. Les charges ne sont donc émises ici que pour les mois où aucune de ces
/// obligations n'existe — sans quoi le même décaissement compterait deux fois.
/// </para>
/// </remarks>
public sealed class PayrollCollector : ICashFlowSourceCollector
{
    private readonly ITenantDbContextFactory _contextFactory;

    public PayrollCollector(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public CashFlowSourceType SourceType => CashFlowSourceType.Payroll;

    public bool IsEnabled(TreasuryForecastOptions options) => true;

    /// <summary>Obligations de l'échéancier qui portent déjà les charges et retenues sur salaires.</summary>
    private static readonly FiscalObligationType[] PayrollBackedObligations =
    {
        FiscalObligationType.CnssMonthlyRemittance,
        FiscalObligationType.CnssDtsQuarterly,
        FiscalObligationType.PayrollIrppWithholding
    };

    public async Task<IReadOnlyList<CashFlowLineDraft>> CollectAsync(
        CashFlowCollectionContext context,
        CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();

        var runs = await ctx.PayrollRuns
            .AsNoTracking()
            .Select(r => new
            {
                r.Id,
                r.Year,
                r.Month,
                r.Status,
                r.Label,
                r.TotalNet,
                Contributions = r.TotalCnssEmployee + r.TotalCnssEmployer + r.TotalIrpp + r.TotalCss
                                + r.TotalTfp + r.TotalFoprolos + r.TotalCssEmployer + r.TotalWorkAccident
            })
            .ToListAsync(cancellationToken);

        var runIds = runs.Select(r => r.Id).ToList();
        var paidByRun = runIds.Count == 0
            ? new Dictionary<Guid, decimal>()
            : await ctx.Payslips
                .AsNoTracking()
                .Where(p => runIds.Contains(p.PayrollRunId))
                .GroupBy(p => p.PayrollRunId)
                .Select(g => new { RunId = g.Key, Paid = g.Sum(p => p.PaidAmount) })
                .ToDictionaryAsync(x => x.RunId, x => x.Paid, cancellationToken);

        var monthsWithFiscalCover = await LoadMonthsCoveredByFiscalScheduleAsync(ctx, context, cancellationToken);

        var drafts = new List<CashFlowLineDraft>();
        var monthsWithRun = new HashSet<(int Year, int Month)>();

        foreach (var run in runs)
        {
            monthsWithRun.Add((run.Year, run.Month));

            var paid = paidByRun.TryGetValue(run.Id, out var p) ? p : 0m;
            var remaining = MillimeRounding.Round(run.TotalNet - paid);
            if (remaining <= 0m) continue;

            var payDate = ResolvePayDate(run.Year, run.Month, context.PayrollPaymentDayOfMonth);
            var expectedDate = payDate < context.Today ? context.Today : payDate;
            if (expectedDate < context.From || expectedDate > context.To) continue;

            // Un cycle encore en brouillon peut changer de montant ; un cycle validé est une dette
            // constatée. La probabilité le reflète.
            var isCommitted = run.Status is PayrollRunStatus.Validated or PayrollRunStatus.Closed;

            drafts.Add(new CashFlowLineDraft
            {
                Direction = CashFlowDirection.Outflow,
                SourceType = CashFlowSourceType.Payroll,
                SourceId = run.Id,
                SourceReference = $"{run.Year:D4}-{run.Month:D2}",
                Label = $"Salaires — {run.Label}",
                ThirdPartyName = "Personnel",
                ContractualDate = payDate,
                ExpectedDate = expectedDate,
                Amount = remaining,
                ProbabilityPercent = isCommitted ? 100m : context.Options.ProjectedPayrollProbabilityPercent,
                IsConfirmed = isCommitted
            });

            if (run.Contributions > 0m && !monthsWithFiscalCover.Contains((run.Year, run.Month)))
            {
                drafts.Add(BuildContributionDraft(
                    run.Year,
                    run.Month,
                    MillimeRounding.Round(run.Contributions),
                    context,
                    isCommitted ? 100m : context.Options.ProjectedPayrollProbabilityPercent,
                    isCommitted));
            }
        }

        drafts.AddRange(BuildProjectedRuns(runs
                .Where(r => r.Status is PayrollRunStatus.Validated or PayrollRunStatus.Closed)
                .OrderByDescending(r => r.Year).ThenByDescending(r => r.Month)
                .Select(r => r.TotalNet)
                .Take(3)
                .ToList(),
            monthsWithRun,
            context));

        return drafts;
    }

    /// <summary>
    /// Cycles non encore créés sur la fenêtre, estimés par la moyenne des trois derniers cycles
    /// arrêtés. Sans historique, aucune projection n'est produite : inventer une masse salariale
    /// serait pire qu'un trou assumé dans la prévision.
    /// </summary>
    private static IEnumerable<CashFlowLineDraft> BuildProjectedRuns(
        IReadOnlyList<decimal> recentNets,
        HashSet<(int Year, int Month)> monthsWithRun,
        CashFlowCollectionContext context)
    {
        if (recentNets.Count == 0) yield break;

        var averageNet = MillimeRounding.Round(recentNets.Average());
        if (averageNet <= 0m) yield break;

        var cursor = new DateTime(context.From.Year, context.From.Month, 1);
        var last = new DateTime(context.To.Year, context.To.Month, 1);

        for (; cursor <= last; cursor = cursor.AddMonths(1))
        {
            if (monthsWithRun.Contains((cursor.Year, cursor.Month))) continue;

            var payDate = ResolvePayDate(cursor.Year, cursor.Month, context.PayrollPaymentDayOfMonth);
            if (payDate < context.Today || payDate < context.From || payDate > context.To) continue;

            yield return new CashFlowLineDraft
            {
                Direction = CashFlowDirection.Outflow,
                SourceType = CashFlowSourceType.Payroll,
                SourceReference = $"{cursor.Year:D4}-{cursor.Month:D2}",
                Label = $"Salaires estimés — {cursor:MM/yyyy}",
                ThirdPartyName = "Personnel",
                ContractualDate = payDate,
                ExpectedDate = payDate,
                Amount = averageNet,
                ProbabilityPercent = context.Options.ProjectedPayrollProbabilityPercent,
                IsConfirmed = false
            };
        }
    }

    private static CashFlowLineDraft BuildContributionDraft(
        int year,
        int month,
        decimal amount,
        CashFlowCollectionContext context,
        decimal probability,
        bool isConfirmed)
    {
        // Les charges du mois M se règlent le mois suivant : c'est le rythme des déclarations.
        var declarationMonth = new DateTime(year, month, 1).AddMonths(1);
        var dueDate = new DateTime(declarationMonth.Year, declarationMonth.Month, 28);
        var expectedDate = dueDate < context.Today ? context.Today : dueDate;

        return new CashFlowLineDraft
        {
            Direction = CashFlowDirection.Outflow,
            SourceType = CashFlowSourceType.PayrollContribution,
            SourceReference = $"{year:D4}-{month:D2}",
            Label = $"Charges sociales et retenues — {month:D2}/{year}",
            ThirdPartyName = "CNSS / Recette des finances",
            ContractualDate = dueDate,
            ExpectedDate = expectedDate,
            Amount = amount,
            ProbabilityPercent = probability,
            IsConfirmed = isConfirmed
        };
    }

    /// <summary>Date de paiement des salaires d'un mois, jour ramené à la longueur du mois.</summary>
    internal static DateTime ResolvePayDate(int year, int month, int dayOfMonth)
    {
        var day = Math.Clamp(dayOfMonth, 1, DateTime.DaysInMonth(year, month));
        return new DateTime(year, month, day);
    }

    /// <summary>
    /// Mois de paie déjà couverts par une obligation de l'échéancier fiscal. Une obligation
    /// trimestrielle couvre les trois mois de son trimestre.
    /// </summary>
    private static async Task<HashSet<(int Year, int Month)>> LoadMonthsCoveredByFiscalScheduleAsync(
        Persistence.TenantDbContext ctx,
        CashFlowCollectionContext context,
        CancellationToken cancellationToken)
    {
        var entries = await ctx.FiscalScheduleEntries
            .AsNoTracking()
            .Where(e => !e.IsCancelled && PayrollBackedObligations.Contains(e.ObligationType))
            .Select(e => new { e.FiscalYear, e.PeriodMonth, e.PeriodQuarter })
            .ToListAsync(cancellationToken);

        var covered = new HashSet<(int, int)>();

        foreach (var entry in entries)
        {
            if (entry.PeriodMonth is > 0)
            {
                covered.Add((entry.FiscalYear, entry.PeriodMonth.Value));
                continue;
            }

            if (entry.PeriodQuarter is > 0 and <= 4)
            {
                var firstMonth = ((entry.PeriodQuarter.Value - 1) * 3) + 1;
                for (var m = firstMonth; m < firstMonth + 3; m++)
                    covered.Add((entry.FiscalYear, m));
            }
        }

        return covered;
    }
}
