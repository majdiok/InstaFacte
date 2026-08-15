using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Services;
using FactuTrust.Domain.Entities.FirmGovernance;
using FactuTrust.Domain.Entities.Honoraires;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services;

public sealed class FirmDecisionTablesService : IFirmDecisionTablesService
{
    private const int RowLimit = 10;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);
    private const string CacheKeyPrefix = "firm-decision-tables:";

    private readonly MasterDbContext _master;
    private readonly ITenantService _tenantService;
    private readonly IFirmFiscalScheduleService _fiscalSchedule;
    private readonly IFirmTimeProfitabilityService _timeProfitability;
    private readonly IFirmGovernanceService _governance;
    private readonly IFirmCollaboratorRentabilityService _rentability;
    private readonly IMemoryCache _cache;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<FirmDecisionTablesService> _logger;

    public FirmDecisionTablesService(
        MasterDbContext master,
        ITenantService tenantService,
        IFirmFiscalScheduleService fiscalSchedule,
        IFirmTimeProfitabilityService timeProfitability,
        IFirmGovernanceService governance,
        IFirmCollaboratorRentabilityService rentability,
        IMemoryCache cache,
        TimeProvider timeProvider,
        ILogger<FirmDecisionTablesService> logger)
    {
        _master = master;
        _tenantService = tenantService;
        _fiscalSchedule = fiscalSchedule;
        _timeProfitability = timeProfitability;
        _governance = governance;
        _rentability = rentability;
        _cache = cache;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<FirmDecisionTablesDto> GetDecisionTablesAsync(
        Guid firmTenantId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeyPrefix + firmTenantId;
        if (_cache.TryGetValue(cacheKey, out FirmDecisionTablesDto? cached) && cached is not null)
            return cached;

        var sw = System.Diagnostics.Stopwatch.StartNew();

        var failures = new List<FirmDecisionTablesPartialFailureDto>();
        var today = _timeProvider.GetLocalNow().DateTime.Date;
        var currentYear = today.Year;

        var criticalFiscal = await SafeSectionAsync(
            "criticalFiscal",
            failures,
            () => BuildCriticalFiscalAsync(firmTenantId, today, cancellationToken));

        var atRisk = await SafeSectionAsync(
            "atRiskDossiers",
            failures,
            () => BuildAtRiskDossiersAsync(firmTenantId, today, cancellationToken));

        var negativeMargins = await SafeSectionAsync(
            "negativeMargins",
            failures,
            () => BuildNegativeMarginsAsync(firmTenantId, currentYear, cancellationToken));

        var pendingTimeSheets = await SafeSectionAsync(
            "pendingTimeSheets",
            failures,
            () => BuildPendingTimeSheetsAsync(firmTenantId, cancellationToken));

        var socialAlerts = await SafeSectionAsync(
            "socialAlerts",
            failures,
            () => BuildSocialAlertsAsync(firmTenantId, cancellationToken));

        var honorairesAlerts = await SafeSectionAsync(
            "honorairesAlerts",
            failures,
            () => BuildHonorairesAlertsAsync(firmTenantId, currentYear, cancellationToken));

        var dto = new FirmDecisionTablesDto
        {
            CriticalFiscalSchedules = criticalFiscal,
            AtRiskDossiers = atRisk,
            NegativeMargins = negativeMargins,
            PendingTimeSheets = pendingTimeSheets,
            SocialAlerts = socialAlerts,
            HonorairesAlerts = honorairesAlerts,
            Meta = new FirmDecisionTablesMetaDto
            {
                GeneratedAt = _timeProvider.GetUtcNow().UtcDateTime,
                PartialFailures = failures
            }
        };

        _cache.Set(cacheKey, dto, CacheDuration);

        sw.Stop();
        if (sw.ElapsedMilliseconds > 2000)
            _logger.LogWarning(
                "Decision tables for firm {FirmTenantId} took {ElapsedMs}ms",
                firmTenantId,
                sw.ElapsedMilliseconds);

        return dto;
    }

    private async Task<IReadOnlyList<T>> SafeSectionAsync<T>(
        string section,
        List<FirmDecisionTablesPartialFailureDto> failures,
        Func<Task<IReadOnlyList<T>>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Decision table section {Section} failed", section);
            failures.Add(new FirmDecisionTablesPartialFailureDto
            {
                Section = section,
                Message = ex.Message
            });
            return Array.Empty<T>();
        }
    }

    private async Task<IReadOnlyList<FirmCriticalFiscalRowDto>> BuildCriticalFiscalAsync(
        Guid firmTenantId,
        DateTime today,
        CancellationToken cancellationToken)
    {
        var schedule = await _fiscalSchedule.GetScheduleAsync(
            firmTenantId,
            new FiscalScheduleFiltersDto { Page = 1, PageSize = 500 },
            cancellationToken);

        var critical = schedule.Items
            .Where(i =>
                i.Status == (int)FiscalScheduleStatus.Overdue
                || i.Status == (int)FiscalScheduleStatus.UpcomingWithin7Days)
            .Select(i =>
            {
                var days = (i.DueDate.Date - today).Days;
                return new FirmCriticalFiscalRowDto
                {
                    EntryId = i.Id,
                    CompanyTenantId = i.CompanyTenantId,
                    CompanyName = i.CompanyName ?? "—",
                    ObligationType = i.ObligationType,
                    ObligationTypeDisplay = i.ObligationTypeDisplay,
                    ObligationLabel = i.ObligationLabel,
                    DueDate = i.DueDate,
                    DaysUntilDue = days,
                    IsOverdue = i.Status == (int)FiscalScheduleStatus.Overdue,
                    EstimatedAmount = i.EstimatedAmount,
                    Currency = i.Currency
                };
            })
            .OrderByDescending(r => r.IsOverdue)
            .ThenBy(r => r.DaysUntilDue)
            .ThenByDescending(r => r.EstimatedAmount)
            .Take(RowLimit)
            .ToList();

        return critical;
    }

    private async Task<IReadOnlyList<FirmAtRiskDossierRowDto>> BuildAtRiskDossiersAsync(
        Guid firmTenantId,
        DateTime today,
        CancellationToken cancellationToken)
    {
        var cutoff = today.AddDays(-30);

        var clients = await (
            from a in _master.FirmClientAssignments.AsNoTracking()
            join t in _master.Tenants.AsNoTracking() on a.CompanyTenantId equals t.Id
            where a.FirmTenantId == firmTenantId && a.Status == FirmAssignmentStatus.Active
            select new
            {
                a.Id,
                a.CompanyTenantId,
                t.CompanyName
            }).ToListAsync(cancellationToken);

        var permanentFiles = await _master.PermanentFiles.AsNoTracking()
            .Where(p => p.FirmTenantId == firmTenantId)
            .ToListAsync(cancellationToken);

        var pfByAssignment = permanentFiles.ToDictionary(p => p.FirmClientAssignmentId);
        var pfIds = permanentFiles.Select(p => p.Id).ToList();
        var repCounts = pfIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await _master.LegalRepresentatives.AsNoTracking()
                .Where(r => pfIds.Contains(r.PermanentFileId) && r.IsActive)
                .GroupBy(r => r.PermanentFileId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        var rows = new List<FirmAtRiskDossierRowDto>();

        foreach (var client in clients)
        {
            DateTime? lastEntry = null;
            try
            {
                var conn = await _tenantService.GetConnectionStringAsync(client.CompanyTenantId, cancellationToken);
                if (!string.IsNullOrEmpty(conn))
                {
                    await using var ctx = CreateTenantContext(conn);
                    lastEntry = await ctx.JournalEntries.AsNoTracking()
                        .MaxAsync(e => (DateTime?)e.EntryDate, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "At-risk dossier journal lookup failed for tenant {TenantId}", client.CompanyTenantId);
            }

            var isInactive = lastEntry is null || lastEntry < cutoff;
            var noJournal = lastEntry is null;

            var pf = pfByAssignment.GetValueOrDefault(client.Id);
            var hasPf = pf is not null;
            int? completionPercent = null;
            int missingCount = 0;
            string? accountantName = null;
            var dpIncomplete = false;

            if (pf is not null)
            {
                var repCount = repCounts.GetValueOrDefault(pf.Id);
                var quality = PermanentFileQualityCalculator.Compute(pf, repCount);
                completionPercent = quality.CompletionPercent;
                missingCount = quality.MissingItems.Count;
                accountantName = pf.AssignedAccountantName;
                dpIncomplete = pf.Status != PermanentFileStatus.Complete || !quality.IsBusinessComplete;
            }
            else
            {
                dpIncomplete = true;
            }

            var signals = new List<string>();
            if (isInactive) signals.Add("Inactif 30j");
            if (noJournal) signals.Add("Sans écriture");
            if (!hasPf) signals.Add("Sans DP");
            else if (dpIncomplete) signals.Add("DP incomplet");

            var score = 0;
            if (isInactive) score += 3;
            if (dpIncomplete) score += 2;
            if (noJournal) score += 1;

            if (score == 0)
                continue;

            rows.Add(new FirmAtRiskDossierRowDto
            {
                AssignmentId = client.Id,
                CompanyTenantId = client.CompanyTenantId,
                CompanyName = client.CompanyName,
                Signals = signals,
                LastJournalEntryDate = lastEntry,
                PermanentFileCompletionPercent = completionPercent,
                PermanentFileMissingItemsCount = missingCount,
                HasPermanentFile = hasPf,
                AssignedAccountantName = accountantName,
                PriorityScore = score
            });
        }

        return rows
            .OrderByDescending(r => r.PriorityScore)
            .ThenBy(r => r.LastJournalEntryDate ?? DateTime.MinValue)
            .Take(RowLimit)
            .ToList();
    }

    private async Task<IReadOnlyList<FirmNegativeMarginRowDto>> BuildNegativeMarginsAsync(
        Guid firmTenantId,
        int year,
        CancellationToken cancellationToken)
    {
        var report = await _timeProfitability.GetDossierTimeProfitabilityAsync(
            firmTenantId,
            companySearch: null,
            year: year,
            collaboratorUserId: null,
            marginFilter: FirmMarginSignFilter.Negative,
            cancellationToken);

        var assignmentToCompany = await _master.FirmClientAssignments.AsNoTracking()
            .Where(a => a.FirmTenantId == firmTenantId && a.Status == FirmAssignmentStatus.Active)
            .Select(a => new { a.Id, a.CompanyTenantId })
            .ToDictionaryAsync(a => a.Id, a => a.CompanyTenantId, cancellationToken);

        return report.Rows
            .OrderBy(r => r.Margin)
            .Take(RowLimit)
            .Select(r => new FirmNegativeMarginRowDto
            {
                FirmClientAssignmentId = r.FirmClientAssignmentId,
                CompanyTenantId = r.FirmClientAssignmentId.HasValue
                    ? assignmentToCompany.GetValueOrDefault(r.FirmClientAssignmentId.Value)
                    : null,
                CompanyName = r.CompanyName,
                Year = r.Year,
                CollaboratorUserId = r.CollaboratorUserId,
                CollaboratorName = r.CollaboratorName,
                BudgetAnnuel = r.BudgetAnnuel,
                TotalHours = r.TotalHours,
                Margin = r.Margin
            })
            .ToList();
    }

    private async Task<IReadOnlyList<FirmPendingTimeSheetRowDto>> BuildPendingTimeSheetsAsync(
        Guid firmTenantId,
        CancellationToken cancellationToken)
    {
        var entries = await _master.FirmTimeSheetEntries.AsNoTracking()
            .Where(e => e.FirmTenantId == firmTenantId && e.Status == FirmTimeSheetStatus.Submitted)
            .ToListAsync(cancellationToken);

        var grouped = entries
            .GroupBy(e => new
            {
                e.UserId,
                e.UserDisplayName,
                e.WorkDate.Year,
                e.WorkDate.Month
            })
            .Select(g => new FirmPendingTimeSheetRowDto
            {
                CollaboratorUserId = g.Key.UserId,
                CollaboratorName = g.Key.UserDisplayName,
                PeriodYear = g.Key.Year,
                PeriodMonth = g.Key.Month,
                SubmittedHours = g.Sum(x => x.Hours),
                EntryCount = g.Count(),
                CompanyNames = g
                    .Select(x => x.ClientCompanyName)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => n)
                    .ToList()!
            })
            .OrderByDescending(r => r.SubmittedHours)
            .ThenByDescending(r => r.PeriodYear)
            .ThenByDescending(r => r.PeriodMonth)
            .Take(RowLimit)
            .ToList();

        return grouped;
    }

    private async Task<IReadOnlyList<FirmSocialAlertRowDto>> BuildSocialAlertsAsync(
        Guid firmTenantId,
        CancellationToken cancellationToken)
    {
        var overview = await _governance.GetSocialOverviewAsync(firmTenantId, cancellationToken);

        return overview.Clients
            .Select(c =>
            {
                var score = c.PendingLeaveRequests + c.PayrollRunsDraftCount + c.DtsPendingCount;
                return new FirmSocialAlertRowDto
                {
                    CompanyTenantId = c.CompanyTenantId,
                    CompanyName = c.CompanyName,
                    EmployeeCount = c.EmployeeCount,
                    PendingLeaveRequests = c.PendingLeaveRequests,
                    PayrollRunsDraftCount = c.PayrollRunsDraftCount,
                    DtsPendingCount = c.DtsPendingCount,
                    AlertScore = score
                };
            })
            .Where(c => c.AlertScore > 0)
            .OrderByDescending(c => c.AlertScore)
            .ThenBy(c => c.CompanyName)
            .Take(RowLimit)
            .ToList();
    }

    private async Task<IReadOnlyList<FirmHonorairesAlertRowDto>> BuildHonorairesAlertsAsync(
        Guid firmTenantId,
        int year,
        CancellationToken cancellationToken)
    {
        var liveRows = await TryBuildLiveHonorairesAlertsAsync(firmTenantId, year, cancellationToken);
        if (liveRows.Count > 0)
            return liveRows;

        return await BuildSnapshotHonorairesAlertsAsync(firmTenantId, year, cancellationToken);
    }

    private async Task<List<FirmHonorairesAlertRowDto>> TryBuildLiveHonorairesAlertsAsync(
        Guid firmTenantId,
        int year,
        CancellationToken cancellationToken)
    {
        var conn = await _tenantService.GetConnectionStringAsync(firmTenantId, cancellationToken);
        if (string.IsNullOrEmpty(conn))
            return [];

        var assignmentMap = await (
            from a in _master.FirmClientAssignments.AsNoTracking()
            join t in _master.Tenants.AsNoTracking() on a.CompanyTenantId equals t.Id
            where a.FirmTenantId == firmTenantId && a.Status == FirmAssignmentStatus.Active
            select new { a.Id, a.CompanyTenantId, t.CompanyName }
        ).ToListAsync(cancellationToken);

        var companyByAssignment = assignmentMap.ToDictionary(
            a => a.Id,
            a => new { a.CompanyTenantId, Name = a.CompanyName });

        await using var ctx = CreateTenantContext(conn);
        var yearStart = new DateTime(year, 1, 1);
        var yearEnd = new DateTime(year, 12, 31);

        var invoices = await ctx.HonorairesInvoices.AsNoTracking()
            .Where(i =>
                i.IssueDate >= yearStart
                && i.IssueDate <= yearEnd
                && i.Status != HonorairesInvoiceStatus.Cancelled
                && i.Status != HonorairesInvoiceStatus.Draft)
            .ToListAsync(cancellationToken);

        if (invoices.Count == 0)
            return [];

        var grouped = invoices
            .GroupBy(i => i.FirmClientAssignmentId)
            .Select(g =>
            {
                var billed = g.Sum(i => i.TotalAmount.Amount);
                var collected = g.Sum(i => i.AmountPaid.Amount);
                var outstanding = g
                    .Where(i => i.Status is HonorairesInvoiceStatus.Validated or HonorairesInvoiceStatus.PartiallyPaid)
                    .Sum(i => Math.Max(0m, i.TotalAmount.Amount - i.AmountPaid.Amount));

                var meta = companyByAssignment.GetValueOrDefault(g.Key);
                var recovery = billed > 0 ? Math.Round(collected / billed * 100m, 2) : 0m;

                return new FirmHonorairesAlertRowDto
                {
                    FirmClientAssignmentId = g.Key,
                    CompanyTenantId = meta?.CompanyTenantId,
                    CompanyName = meta?.Name ?? g.First().ClientName,
                    Year = year,
                    BilledYtdAmount = billed,
                    CollectedAmount = collected,
                    DebitBalance = outstanding,
                    RecoveryRatePercent = recovery,
                    IsSnapshotData = false
                };
            })
            .Where(r => r.DebitBalance > 0 || r.RecoveryRatePercent < 100m)
            .OrderByDescending(r => r.DebitBalance)
            .ThenBy(r => r.RecoveryRatePercent)
            .Take(RowLimit)
            .ToList();

        return grouped;
    }

    private async Task<IReadOnlyList<FirmHonorairesAlertRowDto>> BuildSnapshotHonorairesAlertsAsync(
        Guid firmTenantId,
        int year,
        CancellationToken cancellationToken)
    {
        var list = await _rentability.ListAsync(firmTenantId, year, null, cancellationToken);
        var candidates = list.Items
            .Where(i => i.ClientDebitBalance > 0 || i.RecoveryRatePercent < 100m)
            .Take(20)
            .ToList();

        if (candidates.Count == 0)
            return [];

        var assignmentMap = await _master.FirmClientAssignments.AsNoTracking()
            .Where(a => a.FirmTenantId == firmTenantId)
            .Select(a => new { a.Id, a.CompanyTenantId })
            .ToDictionaryAsync(a => a.Id, a => a.CompanyTenantId, cancellationToken);

        var byAssignment = new Dictionary<Guid, (string Name, decimal Revenue, decimal Debit)>();

        foreach (var item in candidates)
        {
            var detail = await _rentability.GetByIdAsync(firmTenantId, item.Id, cancellationToken);
            if (detail?.Portfolio is null)
                continue;

            foreach (var row in detail.Portfolio)
            {
                if (!byAssignment.TryGetValue(row.FirmClientAssignmentId, out var acc))
                    acc = (row.CompanyName, 0m, 0m);

                acc.Revenue += row.RevenueShare;
                acc.Debit += row.ClientDebitBalance;
                byAssignment[row.FirmClientAssignmentId] = (row.CompanyName, acc.Revenue, acc.Debit);
            }
        }

        return byAssignment
            .Select(kv =>
            {
                var collected = kv.Value.Revenue - kv.Value.Debit;
                var recovery = kv.Value.Revenue > 0
                    ? Math.Round(collected / kv.Value.Revenue * 100m, 2)
                    : 0m;

                return new FirmHonorairesAlertRowDto
                {
                    FirmClientAssignmentId = kv.Key,
                    CompanyTenantId = assignmentMap.GetValueOrDefault(kv.Key),
                    CompanyName = kv.Value.Name,
                    Year = year,
                    BilledYtdAmount = kv.Value.Revenue,
                    CollectedAmount = Math.Max(0m, collected),
                    DebitBalance = kv.Value.Debit,
                    RecoveryRatePercent = recovery,
                    IsSnapshotData = true
                };
            })
            .Where(r => r.DebitBalance > 0 || r.RecoveryRatePercent < 100m)
            .OrderByDescending(r => r.DebitBalance)
            .ThenBy(r => r.RecoveryRatePercent)
            .Take(RowLimit)
            .ToList();
    }

    private static TenantDbContext CreateTenantContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new TenantDbContext(options);
    }
}
