using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Audit;
using FactuTrust.Domain.Entities.AccountingAudit;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services.AccountingAudit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AccountingAudit;

/// <summary>
/// Périmètre et score du moteur d'audit.
///
/// <para>Ces tests verrouillent trois comportements dont dépend l'ajout de nouvelles familles de
/// contrôles : un run ne conclut jamais sur ce qu'il n'a pas balayé (module, exercice), et le taux
/// de conformité reste comparable d'un run à l'autre quel que soit le nombre de règles au
/// catalogue.</para>
/// </summary>
public sealed class AuditEngineScopingTests
{
    // ── Doublures ─────────────────────────────────────────────────────────────────────────

    private sealed class TestTenantDbContextFactory : ITenantDbContextFactory
    {
        private readonly string _databaseName;
        public TestTenantDbContextFactory(string databaseName) => _databaseName = databaseName;

        public TenantDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseInMemoryDatabase(_databaseName)
                .Options;
            return new TenantDbContext(options);
        }
    }

    /// <summary>Règle déterministe qui émet un nombre fixé d'anomalies sur un module donné.</summary>
    private sealed class StubRule : AccountingAuditRuleBase
    {
        private readonly int _anomalyCount;

        public StubRule(string code, string moduleCode, int severity, int anomalyCount)
        {
            Code = code;
            ModuleCode = moduleCode;
            DefaultSeverity = severity;
            _anomalyCount = anomalyCount;
        }

        public override string Code { get; }
        public override string ModuleCode { get; }
        public override int Category => (int)AnomalyCategory.Integrite;
        public override int DefaultSeverity { get; }

        public override Task<IReadOnlyList<AnomalyCandidate>> EvaluateAsync(
            IAuditEvaluationContext ctx, CancellationToken cancellationToken)
        {
            var results = new List<AnomalyCandidate>();
            for (var i = 0; i < _anomalyCount; i++)
            {
                results.Add(SingleGroup(
                    Code, ModuleCode, Category, DefaultSeverity,
                    $"Anomalie {Code} #{i}", "description", "impact",
                    accountRef: $"{Code}-{i}", amount: 100m,
                    periodFrom: null, periodTo: null,
                    lines: Array.Empty<AnomalyLineCandidate>(),
                    recommendations: Array.Empty<string>(),
                    deepLinkRoute: null));
            }
            return Task.FromResult<IReadOnlyList<AnomalyCandidate>>(results);
        }
    }

    private static AccountingSettings Settings() => new()
    {
        AccountingAuditDashboardEnabled = true,
        AccountingAuditPersistenceEnabled = true
    };

    private static AccountingAuditEngine BuildEngine(
        TestTenantDbContextFactory factory,
        params IAccountingAuditRule[] rules) =>
        new(factory, new AccountingAuditRuleRegistry(rules), Options.Create(Settings()));

    /// <summary>Anomalie ouverte préexistante, rattachée à un run déjà terminé.</summary>
    private static async Task SeedOpenAnomalyAsync(
        TestTenantDbContextFactory factory, int fiscalYear, string moduleCode, string ruleCode)
    {
        await using var ctx = factory.CreateContext();
        var run = new AccountingControlRun
        {
            FiscalYear = fiscalYear,
            PeriodFrom = new DateOnly(fiscalYear, 1, 1),
            PeriodTo = new DateOnly(fiscalYear, 12, 31),
            StartedAt = DateTime.UtcNow.AddDays(-1),
            CompletedAt = DateTime.UtcNow.AddDays(-1),
            Status = ControlRunStatus.Completed
        };
        ctx.Set<AccountingControlRun>().Add(run);
        ctx.Set<AccountingAnomaly>().Add(new AccountingAnomaly
        {
            RunId = run.Id,
            RuleCode = ruleCode,
            ModuleCode = moduleCode,
            Fingerprint = $"seed-{fiscalYear}-{moduleCode}-{ruleCode}",
            Severity = (int)PreClosingSeverity.Warning,
            Category = AnomalyCategory.Integrite,
            Title = "Anomalie préexistante",
            Description = "d",
            Impact = "i",
            Status = AnomalyStatus.Open,
            DetectedAt = DateTime.UtcNow.AddDays(-1)
        });
        await ctx.SaveChangesAsync();
    }

    private static async Task<AnomalyStatus> ReadStatusAsync(
        TestTenantDbContextFactory factory, string fingerprint)
    {
        await using var ctx = factory.CreateContext();
        var anomaly = await ctx.Set<AccountingAnomaly>().AsNoTracking()
            .FirstAsync(a => a.Fingerprint == fingerprint);
        return anomaly.Status;
    }

    // ── Périmètre de modules ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Module_filtered_run_does_not_close_anomalies_of_other_modules()
    {
        var factory = new TestTenantDbContextFactory($"Scoping_Module_{Guid.NewGuid()}");
        await SeedOpenAnomalyAsync(factory, 2026, moduleCode: "vat", ruleCode: "vat");

        // Run limité à « payroll » : rien n'a été évalué en TVA.
        var engine = BuildEngine(factory, new StubRule("payroll-stub", "payroll", (int)PreClosingSeverity.Warning, 1));
        var result = await engine.RunAsync(
            new AccountingAuditRunRequestDto { FiscalYear = 2026, ModuleCodes = ["payroll"] },
            userId: null, userName: "test");

        Assert.True(result.IsSuccess);
        Assert.Equal(AnomalyStatus.Open, await ReadStatusAsync(factory, "seed-2026-vat-vat"));
    }

    [Fact]
    public async Task Unfiltered_run_closes_anomalies_no_longer_detected()
    {
        var factory = new TestTenantDbContextFactory($"Scoping_Close_{Guid.NewGuid()}");
        await SeedOpenAnomalyAsync(factory, 2026, moduleCode: "vat", ruleCode: "vat");

        // Run complet qui ne redétecte pas l'anomalie TVA : elle est bien auto-résolue.
        var engine = BuildEngine(factory, new StubRule("integrity-stub", "integrity", (int)PreClosingSeverity.Info, 0));
        var result = await engine.RunAsync(
            new AccountingAuditRunRequestDto { FiscalYear = 2026 }, userId: null, userName: "test");

        Assert.True(result.IsSuccess);
        Assert.Equal(AnomalyStatus.Corrected, await ReadStatusAsync(factory, "seed-2026-vat-vat"));
    }

    // ── Périmètre d'exercice ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Run_on_one_fiscal_year_does_not_close_anomalies_of_another()
    {
        var factory = new TestTenantDbContextFactory($"Scoping_Year_{Guid.NewGuid()}");
        await SeedOpenAnomalyAsync(factory, 2025, moduleCode: "integrity", ruleCode: "drafts");

        var engine = BuildEngine(factory, new StubRule("integrity-stub", "integrity", (int)PreClosingSeverity.Info, 0));
        var result = await engine.RunAsync(
            new AccountingAuditRunRequestDto { FiscalYear = 2026 }, userId: null, userName: "test");

        Assert.True(result.IsSuccess);
        Assert.Equal(AnomalyStatus.Open, await ReadStatusAsync(factory, "seed-2025-integrity-drafts"));
    }

    [Fact]
    public async Task Ignored_anomalies_are_never_reopened_or_closed()
    {
        var factory = new TestTenantDbContextFactory($"Scoping_Ignored_{Guid.NewGuid()}");
        await using (var ctx = factory.CreateContext())
        {
            var run = new AccountingControlRun
            {
                FiscalYear = 2026,
                PeriodFrom = new DateOnly(2026, 1, 1),
                PeriodTo = new DateOnly(2026, 12, 31),
                StartedAt = DateTime.UtcNow.AddDays(-1),
                Status = ControlRunStatus.Completed
            };
            ctx.Set<AccountingControlRun>().Add(run);
            ctx.Set<AccountingAnomaly>().Add(new AccountingAnomaly
            {
                RunId = run.Id,
                RuleCode = "drafts",
                ModuleCode = "integrity",
                Fingerprint = "seed-ignored",
                Severity = (int)PreClosingSeverity.Warning,
                Category = AnomalyCategory.Integrite,
                Title = "Ignorée",
                Description = "d",
                Impact = "i",
                Status = AnomalyStatus.Ignored,
                IgnoreReason = "Cas connu du dossier",
                DetectedAt = DateTime.UtcNow.AddDays(-1)
            });
            await ctx.SaveChangesAsync();
        }

        var engine = BuildEngine(factory, new StubRule("integrity-stub", "integrity", (int)PreClosingSeverity.Info, 0));
        await engine.RunAsync(new AccountingAuditRunRequestDto { FiscalYear = 2026 }, null, "test");

        Assert.Equal(AnomalyStatus.Ignored, await ReadStatusAsync(factory, "seed-ignored"));
    }

    // ── Taux de conformité ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Compliance_rate_divides_by_evaluated_rules_not_by_catalogue_size()
    {
        // Deux catalogues de tailles différentes, MÊME périmètre évalué (une règle « integrity »
        // qui émet un bloquant) : le taux doit être identique. C'est la garantie qui permet
        // d'enrichir le catalogue sans fausser l'historique de tous les tenants.
        async Task<decimal> RateAsync(IAccountingAuditRule[] rules)
        {
            var factory = new TestTenantDbContextFactory($"Scoping_Rate_{Guid.NewGuid()}");
            var engine = BuildEngine(factory, rules);
            var result = await engine.RunAsync(
                new AccountingAuditRunRequestDto { FiscalYear = 2026, ModuleCodes = ["integrity"] },
                userId: null, userName: "test");
            Assert.True(result.IsSuccess);
            return result.Value.ComplianceRate;
        }

        var smallCatalogue = await RateAsync(
        [
            new StubRule("a", "integrity", (int)PreClosingSeverity.Blocking, 1)
        ]);

        var largeCatalogue = await RateAsync(
        [
            new StubRule("a", "integrity", (int)PreClosingSeverity.Blocking, 1),
            new StubRule("b", "payroll", (int)PreClosingSeverity.Warning, 5),
            new StubRule("c", "vat", (int)PreClosingSeverity.Warning, 5),
            new StubRule("d", "treasury", (int)PreClosingSeverity.Info, 5)
        ]);

        Assert.Equal(smallCatalogue, largeCatalogue);
    }

    [Fact]
    public async Task Evaluated_rule_count_is_persisted_on_the_run()
    {
        var factory = new TestTenantDbContextFactory($"Scoping_Count_{Guid.NewGuid()}");
        var engine = BuildEngine(factory,
            new StubRule("a", "integrity", (int)PreClosingSeverity.Info, 0),
            new StubRule("b", "integrity", (int)PreClosingSeverity.Info, 0),
            new StubRule("c", "payroll", (int)PreClosingSeverity.Info, 0));

        var result = await engine.RunAsync(
            new AccountingAuditRunRequestDto { FiscalYear = 2026, ModuleCodes = ["integrity"] },
            userId: null, userName: "test");

        Assert.True(result.IsSuccess);

        await using var ctx = factory.CreateContext();
        var run = await ctx.Set<AccountingControlRun>().AsNoTracking()
            .FirstAsync(r => r.Id == result.Value.RunId);

        // Seules les deux règles « integrity » entrent dans le périmètre.
        Assert.Equal(2, run.EvaluatedRuleCount);
    }

    [Fact]
    public async Task Disabled_rule_is_excluded_from_the_denominator()
    {
        var factory = new TestTenantDbContextFactory($"Scoping_Disabled_{Guid.NewGuid()}");
        await using (var seed = factory.CreateContext())
        {
            seed.Set<AccountingControlRuleSetting>().Add(new AccountingControlRuleSetting
            {
                RuleCode = "b",
                IsEnabled = false
            });
            await seed.SaveChangesAsync();
        }

        var engine = BuildEngine(factory,
            new StubRule("a", "integrity", (int)PreClosingSeverity.Info, 0),
            new StubRule("b", "integrity", (int)PreClosingSeverity.Info, 0));

        var result = await engine.RunAsync(
            new AccountingAuditRunRequestDto { FiscalYear = 2026 }, userId: null, userName: "test");

        await using var ctx = factory.CreateContext();
        var run = await ctx.Set<AccountingControlRun>().AsNoTracking()
            .FirstAsync(r => r.Id == result.Value.RunId);

        Assert.Equal(1, run.EvaluatedRuleCount);
    }

    // ── Tableau de bord ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Dashboard_anomaly_count_is_the_total_not_the_warning_count()
    {
        var factory = new TestTenantDbContextFactory($"Scoping_Dash_{Guid.NewGuid()}");
        var engine = BuildEngine(factory,
            new StubRule("blocking", "integrity", (int)PreClosingSeverity.Blocking, 2),
            new StubRule("warning", "integrity", (int)PreClosingSeverity.Warning, 3),
            new StubRule("info", "integrity", (int)PreClosingSeverity.Info, 4));

        var result = await engine.RunAsync(
            new AccountingAuditRunRequestDto { FiscalYear = 2026 }, userId: null, userName: "test");

        Assert.True(result.IsSuccess);
        var dashboard = result.Value.Dashboard;
        Assert.NotNull(dashboard);
        Assert.Equal(2, dashboard!.BlockingCount);
        Assert.Equal(3, dashboard.WarningCount);
        Assert.Equal(4, dashboard.InfoCount);
        Assert.Equal(9, dashboard.AnomalyCount);
    }
}
