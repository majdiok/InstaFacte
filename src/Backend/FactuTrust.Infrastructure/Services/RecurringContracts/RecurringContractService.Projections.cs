using System.Globalization;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.RecurringContracts;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services.RecurringContracts;

/// <summary>
/// Projections et agrégats de lecture (T6 échéancier, T7 résumé financier, T8 factures liées,
/// T9 évolution, T10 vue détail, T14 stats liste). Toutes reposent sur le projecteur partagé
/// <see cref="RecurringContractScheduleProjector"/> (D1) — zéro divergence avec le billing.
/// </summary>
public sealed partial class RecurringContractService
{
    /// <summary>
    /// Échéancier prévisionnel (T6) : fusionne les occurrences projetées (projecteur D1) et les
    /// runs existants par clé (PeriodFrom, PeriodTo) — jamais de doublon. Contrat clos
    /// (Cancelled/Expired) → seuls les runs existants sont retournés. Contrat Draft → projection
    /// depuis la date initiale calculée, toutes les occurrences « À venir ».
    /// </summary>
    public async Task<IReadOnlyList<RecurringContractScheduleItemDto>?> GetScheduleAsync(
        Guid id, int count, CancellationToken cancellationToken = default)
    {
        var contract = await _db.RecurringContracts
            .Include(c => c.Lines)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (contract is null) return null;

        var runs = await _db.RecurringContractBillingRuns.AsNoTracking()
            .Where(r => r.RecurringContractId == id)
            .ToListAsync(cancellationToken);
        var runsByPeriod = runs.ToDictionary(r => (r.PeriodFrom.Date, r.PeriodTo.Date));

        count = Math.Clamp(count, 1, 60);
        var today = DateTime.UtcNow.Date;
        var usageEstimate = ComputeUsageEstimate(runs);

        var projected = contract.Status is RecurringContractStatus.Cancelled or RecurringContractStatus.Expired
            ? Array.Empty<ProjectedBillingOccurrence>()
            : RecurringContractScheduleProjector.Project(
                contract.NextBillingDate, contract.StartDate, contract.EndDate, contract.AutoRenew,
                contract.BillingFrequency, contract.BillingDayOfMonth, count);

        var items = new List<RecurringContractScheduleItemDto>();
        var consumedKeys = new HashSet<(DateTime, DateTime)>();
        var isFirstProjected = true;

        foreach (var occurrence in projected)
        {
            var key = (occurrence.PeriodFrom.Date, occurrence.PeriodTo.Date);
            RecurringContractBillingRun? run = null;
            if (runsByPeriod.TryGetValue(key, out var matched))
            {
                run = matched;
                consumedKeys.Add(key);
            }

            var (ht, ttc) = EstimateOccurrenceAmounts(contract, occurrence, usageEstimate, isFirstProjected);
            isFirstProjected = false;

            var status = run is not null
                ? MapRunStatus(run.Status)
                : occurrence.Date < today && contract.Status == RecurringContractStatus.Active
                    ? RecurringContractScheduleOccurrenceStatus.Overdue
                    : RecurringContractScheduleOccurrenceStatus.Upcoming;

            items.Add(new RecurringContractScheduleItemDto
            {
                Date = occurrence.Date,
                PeriodFrom = occurrence.PeriodFrom,
                PeriodTo = occurrence.PeriodTo,
                Description = FormatPeriodDescription(occurrence.PeriodFrom, occurrence.PeriodTo),
                EstimatedAmountHT = ht,
                EstimatedAmountTTC = ttc,
                Status = status,
                StatusDisplay = status.ToDisplayString(),
                BillingRunId = run?.Id,
                InvoiceDraftId = run?.InvoiceDraftId,
                InvoiceId = run?.InvoiceId
            });
        }

        // Runs passés non consommés par une occurrence projetée (périodes antérieures) :
        // montants réels du run ; EstimatedAmountTTC = 0 (colonne « estimée » par nature —
        // les valeurs passées de référence sont exposées par linked-invoices).
        foreach (var run in runs.Where(r => !consumedKeys.Contains((r.PeriodFrom.Date, r.PeriodTo.Date))))
        {
            var status = MapRunStatus(run.Status);
            items.Add(new RecurringContractScheduleItemDto
            {
                Date = run.PeriodFrom,
                PeriodFrom = run.PeriodFrom,
                PeriodTo = run.PeriodTo,
                Description = FormatPeriodDescription(run.PeriodFrom, run.PeriodTo),
                EstimatedAmountHT = run.TotalAmount,
                EstimatedAmountTTC = 0m,
                Status = status,
                StatusDisplay = status.ToDisplayString(),
                BillingRunId = run.Id,
                InvoiceDraftId = run.InvoiceDraftId,
                InvoiceId = run.InvoiceId
            });
        }

        return items.OrderByDescending(i => i.Date).ToList();
    }

    /// <summary>
    /// Résumé financier (T7, règle D4). Fenêtre = [StartDate, EndDate] si EndDate définie,
    /// sinon 12 mois glissants [max(StartDate, aujourd'hui), +12 mois − 1 jour].
    /// La projection part TOUJOURS de la date initiale (jamais de NextBillingDate) : la fenêtre
    /// inclut les périodes passées, sinon un contrat à mi-vie sous-estimerait le total.
    /// </summary>
    public async Task<RecurringContractFinancialSummaryDto?> GetFinancialSummaryAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var contract = await _db.RecurringContracts
            .Include(c => c.Lines)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (contract is null) return null;

        var today = DateTime.UtcNow.Date;
        var isOpenEnded = !contract.EndDate.HasValue;
        var (windowFrom, windowTo) = ComputeContractWindow(contract, today);

        var occurrences = RecurringContractScheduleProjector.Project(
                nextBillingDate: null,
                contract.StartDate, contract.EndDate,
                // Contrat clos : ne pas projeter au-delà d'EndDate.
                autoRenew: contract.AutoRenew && contract.Status is RecurringContractStatus.Active
                    or RecurringContractStatus.Suspended or RecurringContractStatus.Draft,
                contract.BillingFrequency, contract.BillingDayOfMonth, count: 500)
            .Where(o => o.PeriodFrom >= windowFrom && o.PeriodFrom <= windowTo)
            .ToList();

        var runs = await _db.RecurringContractBillingRuns.AsNoTracking()
            .Where(r => r.RecurringContractId == id)
            .ToListAsync(cancellationToken);
        var usageEstimate = ComputeUsageEstimate(runs);

        var total = 0m;
        var isFirst = true;
        foreach (var occurrence in occurrences)
        {
            total += EstimateOccurrenceAmounts(contract, occurrence, usageEstimate, isFirst).HT;
            isFirst = false;
        }
        total = decimal.Round(total, 3, MidpointRounding.AwayFromZero);

        var netRuns = await ComputeNetInvoicedAmountsByRunAsync(id, cancellationToken);
        var invoiced = decimal.Round(netRuns.Sum(r => r.NetAmount), 3, MidpointRounding.AwayFromZero);
        var remaining = decimal.Round(total - invoiced, 3, MidpointRounding.AwayFromZero);

        return new RecurringContractFinancialSummaryDto
        {
            ContractId = id,
            WindowFrom = windowFrom,
            WindowTo = windowTo,
            IsOpenEnded = isOpenEnded,
            TotalContractAmount = total,
            TotalInvoicedAmount = invoiced,
            RemainingAmount = remaining,
            PercentInvoiced = total > 0m
                ? decimal.Round(invoiced / total * 100m, 2, MidpointRounding.AwayFromZero)
                : 0m,
            InvoicedRunsCount = netRuns.Count(r => r.IsCounted),
            TotalRunsCount = occurrences.Count,
            Currency = contract.Currency
        };
    }

    /// <summary>
    /// Factures liées (T8/D5) : factures portant SourceRecurringContractId ∪ avoirs rattachés
    /// via LinkedInvoiceId à ces factures. Les factures annulées figurent (statut affiché).
    /// </summary>
    public async Task<IReadOnlyList<RecurringContractLinkedInvoiceDto>?> GetLinkedInvoicesAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var exists = await _db.RecurringContracts.AsNoTracking()
            .AnyAsync(c => c.Id == id, cancellationToken);
        if (!exists) return null;

        var runs = await _db.RecurringContractBillingRuns.AsNoTracking()
            .Where(r => r.RecurringContractId == id)
            .ToListAsync(cancellationToken);
        var periodsByRunId = runs.ToDictionary(r => r.Id, r => (r.PeriodFrom, r.PeriodTo));

        var invoices = await _db.Invoices.AsNoTracking()
            .Where(i => i.SourceRecurringContractId == id)
            .ToListAsync(cancellationToken);
        var invoiceIds = invoices.Select(i => i.Id).ToList();

        // Avoirs rattachés à une facture du contrat sans porter eux-mêmes le lien contrat
        // (le filtre SourceRecurringContractId == null évite le doublon).
        var creditNotes = invoiceIds.Count == 0
            ? new List<Invoice>()
            : await _db.Invoices.AsNoTracking()
                .Where(i => i.Type == InvoiceType.CreditNote
                    && i.LinkedInvoiceId != null && invoiceIds.Contains(i.LinkedInvoiceId.Value)
                    && i.SourceRecurringContractId == null)
                .ToListAsync(cancellationToken);

        var runIdByInvoiceId = invoices
            .Where(i => i.SourceRecurringContractBillingRunId.HasValue)
            .ToDictionary(i => i.Id, i => i.SourceRecurringContractBillingRunId!.Value);

        return invoices.Concat(creditNotes).Select(i =>
        {
            var runId = i.SourceRecurringContractBillingRunId
                ?? (i.LinkedInvoiceId.HasValue
                    && runIdByInvoiceId.TryGetValue(i.LinkedInvoiceId.Value, out var originRunId)
                        ? originRunId
                        : (Guid?)null);
            (DateTime From, DateTime To)? period = runId.HasValue
                && periodsByRunId.TryGetValue(runId.Value, out var p) ? p : null;

            return new RecurringContractLinkedInvoiceDto
            {
                InvoiceId = i.Id,
                Number = i.Number.Value,
                Date = i.IssueDate,
                DueDate = i.DueDate,
                AmountHT = i.SubTotal.Amount,
                AmountTTC = i.TotalAmount.Amount,
                Status = i.Status,
                StatusDisplay = i.Status.ToDisplayString(),
                IsCreditNote = i.IsCreditNote,
                BillingRunId = runId,
                PeriodFrom = period?.From,
                PeriodTo = period?.To
            };
        }).OrderByDescending(d => d.Date).ToList();
    }

    /// <summary>
    /// Série mensuelle des montants facturés (T9/D9) : runs Invoiced groupés par mois de
    /// PeriodTo sur les <paramref name="months"/> derniers mois calendaires (mois courant inclus,
    /// clampé 1..36) ; montant HT net par le même helper que /financial-summary (D4) ;
    /// mois sans activité à 0 ; tri chronologique ascendant.
    /// </summary>
    public async Task<IReadOnlyList<RecurringContractEvolutionPointDto>?> GetEvolutionAsync(
        Guid id, int months, CancellationToken cancellationToken = default)
    {
        var exists = await _db.RecurringContracts.AsNoTracking()
            .AnyAsync(c => c.Id == id, cancellationToken);
        if (!exists) return null;

        months = Math.Clamp(months, 1, 36);
        var today = DateTime.UtcNow.Date;
        var firstMonth = new DateTime(today.Year, today.Month, 1).AddMonths(-(months - 1));

        var netRuns = await ComputeNetInvoicedAmountsByRunAsync(id, cancellationToken);
        var byMonth = netRuns
            .Where(r => r.PeriodTo >= firstMonth)
            .GroupBy(r => (r.PeriodTo.Year, r.PeriodTo.Month))
            .ToDictionary(
                g => g.Key,
                g => decimal.Round(g.Sum(x => x.NetAmount), 3, MidpointRounding.AwayFromZero));

        var points = new List<RecurringContractEvolutionPointDto>(months);
        for (var i = 0; i < months; i++)
        {
            var month = firstMonth.AddMonths(i);
            byMonth.TryGetValue((month.Year, month.Month), out var amount);
            points.Add(new RecurringContractEvolutionPointDto
            {
                Month = month.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                Amount = amount
            });
        }
        return points;
    }

    /// <summary>Vue détail enrichie (T10/D8) — le DTO et l'endpoint GET /{id} restent inchangés.</summary>
    public async Task<RecurringContractDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var contract = await _db.RecurringContracts
            .Include(c => c.Lines)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (contract is null) return null;

        var clientName = await _db.Clients.AsNoTracking()
            .Where(c => c.Id == contract.ClientId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "—";

        var metricIds = contract.Lines.Where(l => l.UsageMetricId.HasValue)
            .Select(l => l.UsageMetricId!.Value).Distinct().ToList();
        var metrics = metricIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.UsageMetrics.AsNoTracking()
                .Where(m => metricIds.Contains(m.Id))
                .ToDictionaryAsync(m => m.Id, m => m.Name, cancellationToken);

        var baseDto = MapContractCore(contract, clientName, metrics);

        var today = DateTime.UtcNow.Date;

        var activeFixedTotal = contract.Lines
            .Where(l => l.IsActive && l.LineType == RecurringContractLineType.FixedRecurring)
            .Sum(l => l.Quantity * l.UnitPriceHT);
        var estimatedMonthly = NormalizeMonthlyEstimate(activeFixedTotal, contract.BillingFrequency);

        // Occurrences à venir dans la fenêtre D4 (EndDate, sinon 12 mois glissants).
        var (windowFrom, windowTo) = ComputeContractWindow(contract, today);
        var upcomingCount = contract.Status is RecurringContractStatus.Cancelled or RecurringContractStatus.Expired
            ? 0
            : RecurringContractScheduleProjector.Project(
                    contract.NextBillingDate, contract.StartDate, contract.EndDate, contract.AutoRenew,
                    contract.BillingFrequency, contract.BillingDayOfMonth, count: 500)
                .Count(o => o.Date >= today && o.PeriodFrom >= windowFrom && o.PeriodFrom <= windowTo);

        // Totaux « plein tarif » de la période courante : lignes fixes effectives aujourd'hui
        // + frais d'installation non facturé ; lignes à consommation exclues (variables).
        decimal currentHt = 0m, currentTva = 0m;
        foreach (var line in contract.Lines.Where(l => l.IsEffectiveOn(today)))
        {
            decimal? lineHt = line.LineType switch
            {
                RecurringContractLineType.FixedRecurring => line.Quantity * line.UnitPriceHT,
                RecurringContractLineType.OneTimeSetup when !contract.SetupFeeBilled => line.Quantity * line.UnitPriceHT,
                _ => null
            };
            if (!lineHt.HasValue) continue;
            currentHt += lineHt.Value;
            currentTva += lineHt.Value * line.VatRate / 100m;
        }
        currentHt = decimal.Round(currentHt, 3, MidpointRounding.AwayFromZero);
        currentTva = decimal.Round(currentTva, 3, MidpointRounding.AwayFromZero);

        return new RecurringContractDetailDto
        {
            Id = baseDto.Id,
            Number = baseDto.Number,
            ClientId = baseDto.ClientId,
            ClientName = baseDto.ClientName,
            Status = baseDto.Status,
            StatusDisplay = baseDto.StatusDisplay,
            BillingFrequency = baseDto.BillingFrequency,
            BillingFrequencyDisplay = baseDto.BillingFrequencyDisplay,
            BillingDayOfMonth = baseDto.BillingDayOfMonth,
            StartDate = baseDto.StartDate,
            EndDate = baseDto.EndDate,
            NextBillingDate = baseDto.NextBillingDate,
            LastBilledPeriodEnd = baseDto.LastBilledPeriodEnd,
            PaymentTermTemplateId = baseDto.PaymentTermTemplateId,
            PriceListId = baseDto.PriceListId,
            AutoRenew = baseDto.AutoRenew,
            NoticePeriodDays = baseDto.NoticePeriodDays,
            Currency = baseDto.Currency,
            SourceQuoteId = baseDto.SourceQuoteId,
            Reference = baseDto.Reference,
            Notes = baseDto.Notes,
            SetupFeeBilled = baseDto.SetupFeeBilled,
            Lines = baseDto.Lines,
            EstimatedMonthlyAmount = estimatedMonthly,
            UpcomingOccurrencesCount = upcomingCount,
            CancellationDeadline = contract.EndDate?.AddDays(-contract.NoticePeriodDays),
            CurrentPeriodTotalHT = currentHt,
            CurrentPeriodTotalTVA = currentTva,
            CurrentPeriodTotalTTC = currentHt + currentTva
        };
    }

    /// <summary>KPI de la page liste (T14/D16) — réutilise la normalisation de ListAsync.</summary>
    public async Task<RecurringContractStatsDto> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var activeContracts = await _db.RecurringContracts.AsNoTracking()
            .Where(c => c.Status == RecurringContractStatus.Active)
            .Select(c => new { c.Id, c.BillingFrequency, c.NextBillingDate })
            .ToListAsync(cancellationToken);

        var activeIds = activeContracts.Select(c => c.Id).ToList();
        var lineTotals = activeIds.Count == 0
            ? new Dictionary<Guid, decimal>()
            : await _db.RecurringContractLines.AsNoTracking()
                .Where(l => activeIds.Contains(l.RecurringContractId) && l.IsActive)
                .GroupBy(l => l.RecurringContractId)
                .Select(g => new { ContractId = g.Key, Total = g.Sum(l => l.Quantity * l.UnitPriceHT) })
                .ToDictionaryAsync(x => x.ContractId, x => x.Total, cancellationToken);

        var mrr = activeContracts.Sum(c =>
            NormalizeMonthlyEstimate(lineTotals.GetValueOrDefault(c.Id), c.BillingFrequency));

        var dueSoonThreshold = DateTime.UtcNow.Date.AddDays(7);
        var dueSoon = activeContracts.Count(c =>
            c.NextBillingDate.HasValue && c.NextBillingDate.Value.Date <= dueSoonThreshold);

        var pendingDrafts = await _db.RecurringContractBillingRuns
            .CountAsync(r => r.Status == RecurringContractBillingRunStatus.DraftCreated
                && r.InvoiceDraftId != null, cancellationToken);

        return new RecurringContractStatsDto
        {
            ActiveCount = activeContracts.Count,
            EstimatedMonthlyRecurringTotal = decimal.Round(mrr, 3, MidpointRounding.AwayFromZero),
            DueSoonCount = dueSoon,
            PendingDraftsCount = pendingDrafts
        };
    }

    // ───────────────────────────── Helpers partagés ─────────────────────────────

    /// <summary>
    /// Montant estimé d'une occurrence (D2) : lignes fixes proratisées (même formule que le
    /// billing) + frais d'installation sur la première occurrence projetée si non facturé
    /// + estimation usage (moyenne des 3 derniers runs Invoiced, répartie une seule fois).
    /// TTC = Σ HT ligne × (1 + VatRate/100) ; taux usage = première ligne UsageMetered active,
    /// 19 % à défaut. Arrondis 3 décimales AwayFromZero.
    /// </summary>
    /// <summary>Fenêtre d'analyse D4 : StartDate→EndDate, sinon 12 mois glissants depuis max(StartDate, aujourd'hui).</summary>
    private static (DateTime From, DateTime To) ComputeContractWindow(RecurringContract contract, DateTime today)
    {
        var from = contract.EndDate.HasValue
            ? contract.StartDate
            : (contract.StartDate > today ? contract.StartDate : today);
        return (from, contract.EndDate ?? from.AddMonths(12).AddDays(-1));
    }

    private static (decimal HT, decimal TTC) EstimateOccurrenceAmounts(
        RecurringContract contract,
        ProjectedBillingOccurrence occurrence,
        decimal usageEstimate,
        bool isFirstProjected)
    {
        decimal ht = 0m, ttc = 0m;
        var usageApplied = false;
        decimal? usageVatRate = null;

        foreach (var line in contract.GetActiveLinesOn(occurrence.PeriodTo))
        {
            switch (line.LineType)
            {
                case RecurringContractLineType.FixedRecurring:
                {
                    var lineHt = RecurringContractLineProration.Prorate(
                        occurrence.PeriodFrom, occurrence.PeriodTo,
                        contract.StartDate, contract.EndDate,
                        line.EffectiveFrom, line.EffectiveTo,
                        line.Quantity * line.UnitPriceHT);
                    ht += lineHt;
                    ttc += lineHt * (1m + line.VatRate / 100m);
                    break;
                }
                case RecurringContractLineType.OneTimeSetup:
                {
                    if (!contract.SetupFeeBilled && isFirstProjected)
                    {
                        var lineHt = line.Quantity * line.UnitPriceHT;
                        ht += lineHt;
                        ttc += lineHt * (1m + line.VatRate / 100m);
                    }
                    break;
                }
                case RecurringContractLineType.UsageMetered:
                {
                    usageVatRate ??= line.VatRate;
                    if (!usageApplied)
                    {
                        usageApplied = true;
                        ht += usageEstimate;
                        ttc += usageEstimate * (1m + (usageVatRate ?? 19m) / 100m);
                    }
                    break;
                }
            }
        }

        return (decimal.Round(ht, 3, MidpointRounding.AwayFromZero),
                decimal.Round(ttc, 3, MidpointRounding.AwayFromZero));
    }

    /// <summary>Estimation usage : moyenne des UsageAmount des 3 derniers runs Invoiced (0 si aucun).</summary>
    private static decimal ComputeUsageEstimate(IReadOnlyCollection<RecurringContractBillingRun> runs)
    {
        var last = runs
            .Where(r => r.Status == RecurringContractBillingRunStatus.Invoiced)
            .OrderByDescending(r => r.PeriodTo)
            .Take(3)
            .Select(r => r.UsageAmount)
            .ToList();
        return last.Count == 0
            ? 0m
            : decimal.Round(last.Average(), 3, MidpointRounding.AwayFromZero);
    }

    private static RecurringContractScheduleOccurrenceStatus MapRunStatus(
        RecurringContractBillingRunStatus status) => status switch
    {
        RecurringContractBillingRunStatus.Invoiced => RecurringContractScheduleOccurrenceStatus.Invoiced,
        RecurringContractBillingRunStatus.DraftCreated or RecurringContractBillingRunStatus.Pending
            => RecurringContractScheduleOccurrenceStatus.DraftGenerated,
        RecurringContractBillingRunStatus.Failed => RecurringContractScheduleOccurrenceStatus.Failed,
        RecurringContractBillingRunStatus.Skipped => RecurringContractScheduleOccurrenceStatus.Skipped,
        _ => RecurringContractScheduleOccurrenceStatus.Upcoming
    };

    private static string FormatPeriodDescription(DateTime periodFrom, DateTime periodTo) =>
        string.Create(CultureInfo.InvariantCulture,
            $"Période du {periodFrom:dd/MM/yyyy} au {periodTo:dd/MM/yyyy}");

    /// <summary>Ligne d'un run Invoiced valorisée en HT net réellement facturé (helper D4/T7/T9).</summary>
    private sealed record NetInvoicedRun(Guid RunId, DateTime PeriodTo, decimal NetAmount, bool IsCounted);

    /// <summary>
    /// Pour chaque run Invoiced du contrat : montant HT net réellement facturé.
    /// Facture annulée → 0 (et run non compté) ; facture absente → repli sur run.TotalAmount ;
    /// avoirs non annulés rattachés (LinkedInvoiceId) déduits (SubTotal déjà signé négatif).
    /// Jointure sur les deux clés de liaison : run.InvoiceId (handler de validation) et
    /// invoice.SourceRecurringContractBillingRunId (linker).
    /// </summary>
    private async Task<IReadOnlyList<NetInvoicedRun>> ComputeNetInvoicedAmountsByRunAsync(
        Guid contractId, CancellationToken cancellationToken)
    {
        var runs = await _db.RecurringContractBillingRuns.AsNoTracking()
            .Where(r => r.RecurringContractId == contractId
                && r.Status == RecurringContractBillingRunStatus.Invoiced)
            .ToListAsync(cancellationToken);
        if (runs.Count == 0)
            return Array.Empty<NetInvoicedRun>();

        var runIds = runs.Select(r => r.Id).ToList();
        var runInvoiceIds = runs.Where(r => r.InvoiceId.HasValue)
            .Select(r => r.InvoiceId!.Value).ToList();

        var invoices = await _db.Invoices.AsNoTracking()
            .Where(i => (i.SourceRecurringContractBillingRunId != null
                    && runIds.Contains(i.SourceRecurringContractBillingRunId.Value))
                || runInvoiceIds.Contains(i.Id))
            .ToListAsync(cancellationToken);

        var invoiceIds = invoices.Select(i => i.Id).ToList();
        var creditNotes = invoiceIds.Count == 0
            ? new List<Invoice>()
            : await _db.Invoices.AsNoTracking()
                .Where(i => i.Type == InvoiceType.CreditNote
                    && i.LinkedInvoiceId != null && invoiceIds.Contains(i.LinkedInvoiceId.Value)
                    && i.Status != InvoiceStatus.Draft && i.Status != InvoiceStatus.Cancelled)
                .ToListAsync(cancellationToken);

        var result = new List<NetInvoicedRun>(runs.Count);
        foreach (var run in runs)
        {
            var invoice = invoices.FirstOrDefault(i => i.SourceRecurringContractBillingRunId == run.Id)
                ?? (run.InvoiceId.HasValue
                    ? invoices.FirstOrDefault(i => i.Id == run.InvoiceId.Value)
                    : null);

            if (invoice is null)
            {
                result.Add(new NetInvoicedRun(run.Id, run.PeriodTo, run.TotalAmount, IsCounted: true));
                continue;
            }
            if (invoice.Status == InvoiceStatus.Cancelled)
            {
                result.Add(new NetInvoicedRun(run.Id, run.PeriodTo, 0m, IsCounted: false));
                continue;
            }

            var credit = creditNotes
                .Where(cn => cn.LinkedInvoiceId == invoice.Id)
                .Sum(cn => cn.SubTotal.Amount);
            result.Add(new NetInvoicedRun(run.Id, run.PeriodTo,
                decimal.Round(invoice.SubTotal.Amount + credit, 3, MidpointRounding.AwayFromZero),
                IsCounted: true));
        }
        return result;
    }
}
