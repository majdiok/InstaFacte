using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Audit;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using FactuTrust.Infrastructure.Services.AccountingAudit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AccountingAudit;

/// <summary>
/// Invariant de non-régression le plus important de l'élargissement du catalogue de contrôles :
/// <b>ajouter une règle d'audit, même de sévérité bloquante, ne doit jamais empêcher une clôture
/// de période ni un verrouillage d'exercice.</b>
///
/// <para>La clôture consomme <see cref="IPreClosingControlService"/>, un service distinct du moteur
/// d'audit. Ces deux chemins doivent rester indépendants : sans ce test, un contrôle ajouté pour
/// aider le réviseur pourrait bloquer la clôture de tous les dossiers du parc.</para>
/// </summary>
public sealed class ClosingNotBlockedByAuditRulesTests
{
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

    /// <summary>Règle bloquante inconditionnelle : le pire cas pour la clôture.</summary>
    private sealed class AlwaysBlockingRule : AccountingAuditRuleBase
    {
        public override string Code => "test-always-blocking";
        public override string ModuleCode => "integrity";
        public override int Category => (int)AnomalyCategory.Integrite;
        public override int DefaultSeverity => (int)PreClosingSeverity.Blocking;

        public override Task<IReadOnlyList<AnomalyCandidate>> EvaluateAsync(
            IAuditEvaluationContext ctx, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AnomalyCandidate>>(
            [
                SingleGroup(Code, ModuleCode, Category, DefaultSeverity,
                    "Anomalie bloquante de test", "description", "impact",
                    accountRef: null, amount: 0m, periodFrom: null, periodTo: null,
                    lines: Array.Empty<AnomalyLineCandidate>(),
                    recommendations: Array.Empty<string>(),
                    deepLinkRoute: null)
            ]);
    }

    private static JournalEntry MakeEntry(int number, DateTime date, JournalEntryStatus status, params JournalLineInput[] lines)
    {
        var entry = JournalEntry.Create(number, "JOD", date, $"Écriture {number}", Guid.NewGuid(),
            false, "Manual", null, lines, initialStatus: status).Value;
        entry.SetAuditInfo("test", false);
        return entry;
    }

    [Fact]
    public async Task Blocking_audit_rule_does_not_make_pre_closing_blocking()
    {
        var factory = new TestTenantDbContextFactory($"ClosingGuard_{Guid.NewGuid()}");

        // Exercice propre : une écriture validée et équilibrée, période clôturée.
        await using (var ctx = factory.CreateContext())
        {
            ctx.JournalEntries.Add(MakeEntry(1, new DateTime(2026, 3, 10), JournalEntryStatus.Validee,
                new JournalLineInput("6132", "Loyer", 100m, 0m, null, ThirdPartyKind.None),
                new JournalLineInput("532", "Banque", 0m, 100m, null, ThirdPartyKind.None)));
            var period = AccountingPeriod.Create(2026, 3, new DateTime(2026, 3, 1), new DateTime(2026, 3, 31));
            period.Close("test");
            ctx.AccountingPeriods.Add(period);
            await ctx.SaveChangesAsync();
        }

        var fixedAssets = new Mock<IFixedAssetRepository>();
        fixedAssets.Setup(r => r.GetUnpostedScheduleLinesForYearAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DepreciationScheduleLine>());

        // Le moteur d'audit remonte bien un bloquant…
        var engine = new AccountingAuditEngine(
            factory,
            new AccountingAuditRuleRegistry([new AlwaysBlockingRule()]),
            Options.Create(new AccountingSettings
            {
                AccountingAuditDashboardEnabled = true,
                AccountingAuditPersistenceEnabled = true
            }));

        var auditResult = await engine.RunAsync(
            new AccountingAuditRunRequestDto { FiscalYear = 2026 }, userId: null, userName: "test");

        Assert.True(auditResult.IsSuccess);
        Assert.Equal(1, auditResult.Value.BlockingCount);

        // … et la checklist de pré-clôture reste, elle, non bloquante.
        var preClosing = new PreClosingControlService(
            factory, fixedAssets.Object,
            Options.Create(new AccountingSettings { UnletteredAgeThresholdDays = 90 }));

        var checklist = await preClosing.RunAsync(2026);

        Assert.True(checklist.IsSuccess);
        Assert.False(checklist.Value.HasBlocking);
        Assert.DoesNotContain(checklist.Value.Checks, c => c.Code == "test-always-blocking");
    }
}
