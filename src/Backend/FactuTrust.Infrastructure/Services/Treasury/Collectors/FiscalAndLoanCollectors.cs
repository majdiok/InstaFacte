using FactuTrust.Application.Common.Interfaces.Treasury;
using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services.Treasury.Collectors;

/// <summary>
/// Décaissements fiscaux issus de l'échéancier (TVA, retenues à la source, acomptes, CNSS…).
/// </summary>
/// <remarks>
/// La source la plus fiable du prévisionnel : chaque obligation y est déjà datée et chiffrée par
/// <c>IFiscalScheduleGenerator</c>. Le collecteur se contente de lire — il ne génère jamais
/// l'exercice, sous peine de créer des écritures d'échéancier à chaque recalcul de trésorerie.
/// Les obligations réglées, validées ou annulées sont exclues via <c>ResolveStatus</c>.
/// </remarks>
public sealed class FiscalObligationsCollector : ICashFlowSourceCollector
{
    private readonly ITenantDbContextFactory _contextFactory;

    public FiscalObligationsCollector(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public CashFlowSourceType SourceType => CashFlowSourceType.FiscalObligation;

    public bool IsEnabled(TreasuryForecastOptions options) => true;

    public async Task<IReadOnlyList<CashFlowLineDraft>> CollectAsync(
        CashFlowCollectionContext context,
        CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();

        var entries = await ctx.FiscalScheduleEntries
            .AsNoTracking()
            .Where(e => !e.IsCancelled
                        && e.PaymentDate == null
                        && e.EstimatedAmount > 0m
                        && e.DueDate >= context.From
                        && e.DueDate <= context.To)
            .Select(e => new
            {
                e.Id,
                e.DueDate,
                e.EstimatedAmount,
                e.ObligationLabel,
                e.ObligationType
            })
            .ToListAsync(cancellationToken);

        return entries
            .Select(e => new CashFlowLineDraft
            {
                Direction = CashFlowDirection.Outflow,
                SourceType = CashFlowSourceType.FiscalObligation,
                SourceId = e.Id,
                SourceReference = e.ObligationType.ToString(),
                Label = string.IsNullOrWhiteSpace(e.ObligationLabel)
                    ? "Échéance fiscale"
                    : e.ObligationLabel,
                ThirdPartyName = "Recette des finances",
                ContractualDate = e.DueDate.Date,
                ExpectedDate = e.DueDate.Date < context.Today ? context.Today : e.DueDate.Date,
                Amount = e.EstimatedAmount,
                ProbabilityPercent = 100m,
                IsConfirmed = true
            })
            .ToList();
    }
}

/// <summary>
/// Décaissements des échéances d'emprunts bancaires (capital + intérêts).
/// </summary>
/// <remarks>
/// L'échéancier d'emprunt est intégralement calculé à la souscription : chaque ligne est datée et
/// son montant connu. Seuls les emprunts actifs sont retenus — un emprunt remboursé ou annulé ne
/// produit plus de flux. Une ligne d'échéancier n'ayant pas d'indicateur de règlement, la borne
/// basse de la fenêtre suffit à écarter les échéances passées.
/// </remarks>
public sealed class LoanScheduleCollector : ICashFlowSourceCollector
{
    private readonly ITenantDbContextFactory _contextFactory;

    public LoanScheduleCollector(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public CashFlowSourceType SourceType => CashFlowSourceType.LoanInstallment;

    public bool IsEnabled(TreasuryForecastOptions options) => true;

    public async Task<IReadOnlyList<CashFlowLineDraft>> CollectAsync(
        CashFlowCollectionContext context,
        CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();

        var rows = await ctx.LoanScheduleLines
            .AsNoTracking()
            .Where(l => l.DueDate >= context.From && l.DueDate <= context.To && l.InstallmentAmount > 0m)
            .Join(
                ctx.Loans.AsNoTracking().Where(loan => loan.Status == LoanStatus.Active),
                line => line.LoanId,
                loan => loan.Id,
                (line, loan) => new
                {
                    line.Id,
                    line.DueDate,
                    line.InstallmentAmount,
                    line.InstallmentNumber,
                    loan.Label,
                    loan.LoanNumber,
                    loan.LenderName
                })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new CashFlowLineDraft
            {
                Direction = CashFlowDirection.Outflow,
                SourceType = CashFlowSourceType.LoanInstallment,
                SourceId = r.Id,
                SourceReference = r.LoanNumber,
                Label = $"Échéance {r.InstallmentNumber} — {r.Label}",
                ThirdPartyName = r.LenderName,
                ContractualDate = r.DueDate.Date,
                ExpectedDate = r.DueDate.Date,
                Amount = r.InstallmentAmount,
                ProbabilityPercent = 100m,
                IsConfirmed = true
            })
            .ToList();
    }
}

/// <summary>
/// Décaissements et encaissements des engagements récurrents saisis par l'utilisateur.
/// </summary>
/// <remarks>
/// Développe chaque engagement en occurrences sur la fenêtre projetée. Un jour du mois dépassant
/// la longueur du mois est ramené au dernier jour : le 31 devient le 28 ou 29 en février, sinon
/// l'occurrence disparaîtrait silencieusement quatre à cinq fois par an.
/// </remarks>
public sealed class RecurringCommitmentCollector : ICashFlowSourceCollector
{
    private readonly Application.Common.Interfaces.Repositories.IRecurringCashCommitmentRepository _repository;

    public RecurringCommitmentCollector(
        Application.Common.Interfaces.Repositories.IRecurringCashCommitmentRepository repository)
    {
        _repository = repository;
    }

    public CashFlowSourceType SourceType => CashFlowSourceType.RecurringCommitment;

    public bool IsEnabled(TreasuryForecastOptions options) => true;

    public async Task<IReadOnlyList<CashFlowLineDraft>> CollectAsync(
        CashFlowCollectionContext context,
        CancellationToken cancellationToken = default)
    {
        var commitments = await _repository.ListActiveForWindowAsync(
            context.From,
            context.To,
            cancellationToken);

        var drafts = new List<CashFlowLineDraft>();

        foreach (var commitment in commitments)
        {
            foreach (var occurrence in ExpandOccurrences(commitment, context.From, context.To))
            {
                drafts.Add(new CashFlowLineDraft
                {
                    Direction = commitment.Direction,
                    SourceType = CashFlowSourceType.RecurringCommitment,
                    SourceId = commitment.Id,
                    SourceReference = commitment.Category,
                    Label = commitment.Label,
                    ContractualDate = occurrence,
                    ExpectedDate = occurrence,
                    Amount = commitment.Amount,
                    ProbabilityPercent = 100m,
                    IsConfirmed = true
                });
            }
        }

        return drafts;
    }

    /// <summary>Occurrences d'un engagement tombant dans la fenêtre, bornes incluses.</summary>
    internal static IEnumerable<DateTime> ExpandOccurrences(
        Domain.Entities.Treasury.RecurringCashCommitment commitment,
        DateTime from,
        DateTime to)
    {
        var windowStart = from.Date;
        var windowEnd = to.Date;
        var effectiveEnd = commitment.EndDate?.Date is { } end && end < windowEnd ? end : windowEnd;

        if (commitment.StartDate.Date > effectiveEnd)
            yield break;

        if (commitment.Frequency == CashCommitmentFrequency.Weekly)
        {
            var cursor = commitment.StartDate.Date;
            while (cursor < windowStart) cursor = cursor.AddDays(7);
            for (; cursor <= effectiveEnd; cursor = cursor.AddDays(7))
                yield return cursor;
            yield break;
        }

        var step = commitment.MonthStep;
        if (step <= 0) yield break;

        // On part du mois de début pour que la cadence reste calée sur l'engagement : un
        // trimestriel démarré en février tombe en février, mai, août — pas en janvier.
        var month = new DateTime(commitment.StartDate.Year, commitment.StartDate.Month, 1);
        var windowFirstMonth = new DateTime(windowStart.Year, windowStart.Month, 1);

        while (month < windowFirstMonth)
            month = month.AddMonths(step);

        for (; month <= effectiveEnd; month = month.AddMonths(step))
        {
            var day = Math.Min(commitment.DayOfMonth, DateTime.DaysInMonth(month.Year, month.Month));
            var occurrence = new DateTime(month.Year, month.Month, day);

            if (occurrence < windowStart || occurrence < commitment.StartDate.Date) continue;
            if (occurrence > effectiveEnd) yield break;

            yield return occurrence;
        }
    }
}
