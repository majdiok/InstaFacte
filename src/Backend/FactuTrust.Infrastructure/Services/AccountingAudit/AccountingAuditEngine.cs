using System.Text.Json;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Audit;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.AccountingAudit;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services.AccountingAudit;

public sealed class AccountingAuditEngine : IAccountingAuditEngine
{
    private readonly ITenantDbContextFactory _contextFactory;
    private readonly AccountingAuditRuleRegistry _registry;
    private readonly AccountingSettings _settings;

    public AccountingAuditEngine(
        ITenantDbContextFactory contextFactory,
        AccountingAuditRuleRegistry registry,
        IOptions<AccountingSettings> settings)
    {
        _contextFactory = contextFactory;
        _registry = registry;
        _settings = settings.Value;
    }

    public async Task<Result<AccountingAuditRunResultDto>> RunAsync(
        AccountingAuditRunRequestDto request,
        Guid? userId,
        string? userName,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.AccountingAuditDashboardEnabled)
            return Result.Failure<AccountingAuditRunResultDto>(
                Error.Validation("Audit", "Le tableau de bord d'audit comptable n'est pas activé."));

        if (request.FiscalYear is < 2000 or > 2100)
            return Result.Failure<AccountingAuditRunResultDto>(Error.Validation("FiscalYear", "Exercice invalide."));

        var periodFrom = request.PeriodFrom ?? new DateOnly(request.FiscalYear, 1, 1);
        var periodTo = request.PeriodTo ?? new DateOnly(request.FiscalYear, 12, 31);
        var moduleFilter = request.ModuleCodes?.Count > 0
            ? new HashSet<string>(request.ModuleCodes, StringComparer.OrdinalIgnoreCase)
            : null;

        await using var ctx = _contextFactory.CreateContext();
        var ruleSettings = await ctx.Set<AccountingControlRuleSetting>().AsNoTracking().ToListAsync(cancellationToken);

        var run = new AccountingControlRun
        {
            FiscalYear = request.FiscalYear,
            PeriodFrom = periodFrom,
            PeriodTo = periodTo,
            StartedAt = DateTime.UtcNow,
            TriggeredByUserId = userId,
            TriggeredByUserName = userName,
            Status = ControlRunStatus.Running,
            ModuleCodesFilter = moduleFilter is null ? null : string.Join(',', moduleFilter)
        };
        ctx.Set<AccountingControlRun>().Add(run);
        if (_settings.AccountingAuditPersistenceEnabled)
            await ctx.SaveChangesAsync(cancellationToken);

        try
        {
            var evalCtx = new AuditEvaluationContextImpl(
                ctx, _settings, request.FiscalYear, periodFrom, periodTo, ruleSettings, moduleFilter);

            var allCandidates = new List<AnomalyCandidate>();
            foreach (var rule in _registry.GetAll())
            {
                if (!rule.IsEnabled(_settings, evalCtx.GetRuleSetting(rule.Code)))
                    continue;
                if (!evalCtx.IsModuleInScope(rule.ModuleCode))
                    continue;

                var found = await rule.EvaluateAsync(evalCtx, cancellationToken);
                allCandidates.AddRange(found);
            }

            var existingOpen = _settings.AccountingAuditPersistenceEnabled
                ? await ctx.Set<AccountingAnomaly>()
                    .Where(a => a.Status != AnomalyStatus.Corrected && a.Status != AnomalyStatus.Ignored)
                    .ToListAsync(cancellationToken)
                : [];

            var detectedFingerprints = new HashSet<string>(allCandidates.Select(c => c.Fingerprint));
            foreach (var candidate in allCandidates)
            {
                if (!_settings.AccountingAuditPersistenceEnabled)
                    continue;

                var existing = await ctx.Set<AccountingAnomaly>()
                    .FirstOrDefaultAsync(a => a.Fingerprint == candidate.Fingerprint, cancellationToken);

                if (existing is null)
                {
                    var anomaly = MapCandidate(run.Id, candidate);
                    ctx.Set<AccountingAnomaly>().Add(anomaly);
                    ctx.Set<AccountingAnomalyActivity>().Add(new AccountingAnomalyActivity
                    {
                        AnomalyId = anomaly.Id,
                        ActivityType = AuditActivityType.Detected,
                        UserId = userId,
                        UserName = userName,
                        Message = "Anomalie détectée"
                    });
                }
                else
                {
                    existing.RunId = run.Id;
                    existing.DetectedAt = DateTime.UtcNow;
                    existing.Amount = candidate.Amount;
                    existing.Description = candidate.Description;
                }
            }

            if (_settings.AccountingAuditPersistenceEnabled)
            {
                foreach (var open in existingOpen.Where(a => !detectedFingerprints.Contains(a.Fingerprint)))
                {
                    if (open.Status is AnomalyStatus.Ignored) continue;
                    open.Status = AnomalyStatus.Corrected;
                    open.ResolvedAt = DateTime.UtcNow;
                    ctx.Set<AccountingAnomalyActivity>().Add(new AccountingAnomalyActivity
                    {
                        AnomalyId = open.Id,
                        ActivityType = AuditActivityType.Corrected,
                        UserId = userId,
                        UserName = userName,
                        Message = "Anomalie auto-résolue (absente du dernier contrôle)"
                    });
                }
            }

            run.CompletedAt = DateTime.UtcNow;
            run.Status = ControlRunStatus.Completed;
            run.TotalAnomalies = allCandidates.Count;
            run.BlockingCount = allCandidates.Count(c => c.Severity == (int)PreClosingSeverity.Blocking);
            run.WarningCount = allCandidates.Count(c => c.Severity == (int)PreClosingSeverity.Warning);
            run.InfoCount = allCandidates.Count(c => c.Severity == (int)PreClosingSeverity.Info);
            run.ComplianceRate = ComputeComplianceRate(run.BlockingCount, run.WarningCount, run.InfoCount, _registry.GetAll().Count);

            if (_settings.AccountingAuditPersistenceEnabled)
                await ctx.SaveChangesAsync(cancellationToken);

            var dashboard = await BuildDashboardSnapshot(ctx, request.FiscalYear, run, cancellationToken);
            return Result.Success(new AccountingAuditRunResultDto
            {
                RunId = run.Id,
                FiscalYear = request.FiscalYear,
                StartedAt = run.StartedAt,
                CompletedAt = run.CompletedAt,
                Status = run.Status.ToString(),
                ComplianceRate = run.ComplianceRate,
                TotalAnomalies = run.TotalAnomalies,
                BlockingCount = run.BlockingCount,
                WarningCount = run.WarningCount,
                InfoCount = run.InfoCount,
                Dashboard = dashboard
            });
        }
        catch (Exception ex)
        {
            run.Status = ControlRunStatus.Failed;
            run.ErrorMessage = ex.Message;
            run.CompletedAt = DateTime.UtcNow;
            if (_settings.AccountingAuditPersistenceEnabled)
                await ctx.SaveChangesAsync(cancellationToken);
            return Result.Failure<AccountingAuditRunResultDto>(Error.Validation("Audit", ex.Message));
        }
    }

    public async Task<Result<AccountingAuditRunStatusDto>> GetRunStatusAsync(
        Guid runId, CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();
        var run = await ctx.Set<AccountingControlRun>().AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == runId, cancellationToken);
        if (run is null)
            return Result.Failure<AccountingAuditRunStatusDto>(Error.NotFound("AccountingControlRun", runId));

        return Result.Success(new AccountingAuditRunStatusDto
        {
            RunId = run.Id,
            Status = run.Status.ToString(),
            ErrorMessage = run.ErrorMessage,
            ComplianceRate = run.ComplianceRate,
            TotalAnomalies = run.TotalAnomalies
        });
    }

    private static decimal ComputeComplianceRate(int blocking, int warning, int info, int ruleCount)
    {
        if (ruleCount == 0) return 100m;
        var penalty = blocking * 1m + warning * 0.5m + info * 0.1m;
        var rate = Math.Max(0, 100m - (penalty / ruleCount * 100m));
        return Math.Round(rate, 1);
    }

    private static AccountingAnomaly MapCandidate(Guid runId, AnomalyCandidate c)
    {
        var anomaly = new AccountingAnomaly
        {
            RunId = runId,
            RuleCode = c.RuleCode,
            ModuleCode = c.ModuleCode,
            Fingerprint = c.Fingerprint,
            Severity = c.Severity,
            Category = (AnomalyCategory)c.Category,
            Title = c.Title,
            Description = c.Description,
            Impact = c.Impact,
            AccountRef = c.AccountRef,
            Amount = c.Amount,
            PeriodFrom = c.PeriodFrom,
            PeriodTo = c.PeriodTo,
            Status = AnomalyStatus.Open,
            DetectedAt = DateTime.UtcNow,
            DeepLinkRoute = c.DeepLinkRoute,
            RecommendationsJson = JsonSerializer.Serialize(c.Recommendations)
        };
        foreach (var line in c.Lines)
        {
            anomaly.Lines.Add(new AccountingAnomalyLine
            {
                AnomalyId = anomaly.Id,
                JournalEntryId = line.JournalEntryId,
                JournalEntryLineId = line.JournalEntryLineId,
                EntryDate = line.EntryDate,
                AccountNumber = line.AccountNumber,
                Label = line.Label,
                Debit = line.Debit,
                Credit = line.Credit,
                PieceRef = line.PieceRef,
                JustificationStatus = line.JustificationStatus
            });
        }
        return anomaly;
    }

    private static async Task<AccountingAuditDashboardDto> BuildDashboardSnapshot(
        TenantDbContext ctx, int fiscalYear, AccountingControlRun run, CancellationToken ct)
    {
        var priorYear = await ctx.Set<AccountingControlRun>().AsNoTracking()
            .Where(r => r.FiscalYear == fiscalYear - 1 && r.Status == ControlRunStatus.Completed)
            .OrderByDescending(r => r.CompletedAt)
            .FirstOrDefaultAsync(ct);

        decimal? delta = priorYear is null ? null : run.ComplianceRate - priorYear.ComplianceRate;

        return new AccountingAuditDashboardDto
        {
            FiscalYear = fiscalYear,
            BlockingCount = run.BlockingCount,
            WarningCount = run.WarningCount,
            AnomalyCount = run.WarningCount,
            InfoCount = run.InfoCount,
            ComplianceRate = run.ComplianceRate,
            ComplianceRateDeltaVsPriorYear = delta,
            LastRun = run.CompletedAt is null ? null : new AccountingAuditLastRunDto
            {
                RunId = run.Id,
                CompletedAt = run.CompletedAt.Value,
                Duration = run.CompletedAt.Value - run.StartedAt,
                TriggeredByUserName = run.TriggeredByUserName
            }
        };
    }
}
