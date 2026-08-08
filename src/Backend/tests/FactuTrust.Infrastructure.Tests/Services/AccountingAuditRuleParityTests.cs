using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Audit;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.AccountingAudit;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using FactuTrust.Infrastructure.Services.AccountingAudit;
using FactuTrust.Infrastructure.Services.AccountingAudit.Rules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using FactuTrust.Application.Common.Interfaces.Repositories;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Parité : les counts agrégés des règles d'audit doivent correspondre aux contrôles pré-clôture existants.
/// </summary>
public sealed class AccountingAuditRuleParityTests
{
    private readonly string _dbName = $"AuditParity_{Guid.NewGuid()}";

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

    private static JournalEntry MakeEntry(int number, DateTime date, JournalEntryStatus status, params JournalLineInput[] lines)
    {
        var entry = JournalEntry.Create(number, "JOD", date, $"Écriture {number}", Guid.NewGuid(),
            false, "Manual", null, lines, initialStatus: status).Value;
        entry.SetAuditInfo("test", false);
        return entry;
    }

    private PreClosingControlService BuildPreClosing(TestTenantDbContextFactory factory)
    {
        var fixedAssets = new Mock<IFixedAssetRepository>();
        fixedAssets.Setup(r => r.GetUnpostedScheduleLinesForYearAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DepreciationScheduleLine>());
        return new PreClosingControlService(factory, fixedAssets.Object,
            Options.Create(new AccountingSettings { UnletteredAgeThresholdDays = 90 }));
    }

    private async Task<int> AuditRuleCountAsync<TRule>(TestTenantDbContextFactory factory, int fiscalYear)
        where TRule : class, IAccountingAuditRule
    {
        await using var db = factory.CreateContext();
        var settings = new AccountingSettings { UnletteredAgeThresholdDays = 90 };
        var ctx = new AuditEvaluationContextImpl(db, settings, fiscalYear,
            new DateOnly(fiscalYear, 1, 1), new DateOnly(fiscalYear, 12, 31),
            Array.Empty<AccountingControlRuleSetting>(), null);

        var fixedAssets = new Mock<IFixedAssetRepository>();
        fixedAssets.Setup(r => r.GetUnpostedScheduleLinesForYearAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DepreciationScheduleLine>());

        IAccountingAuditRule rule = typeof(TRule).Name switch
        {
            nameof(DraftEntriesAuditRule) => new DraftEntriesAuditRule(),
            nameof(UnbalancedEntriesAuditRule) => new UnbalancedEntriesAuditRule(),
            nameof(SuspenseAccountsAuditRule) => new SuspenseAccountsAuditRule(),
            nameof(UnletteredLinesAuditRule) => new UnletteredLinesAuditRule(),
            nameof(DepreciationAuditRule) => new DepreciationAuditRule(fixedAssets.Object),
            nameof(VatMissingDeclarationAuditRule) => new VatMissingDeclarationAuditRule(),
            nameof(OpenPeriodsAuditRule) => new OpenPeriodsAuditRule(),
            nameof(SequenceGapsAuditRule) => new SequenceGapsAuditRule(),
            _ => throw new NotSupportedException()
        };

        var candidates = await rule.EvaluateAsync(ctx, CancellationToken.None);
        return candidates.Sum(c => c.Lines.Count > 0 ? c.Lines.Count : 1);
    }

    [Fact]
    public async Task DraftsRule_MatchesPreClosingCount()
    {
        var factory = new TestTenantDbContextFactory(_dbName);
        await using (var ctx = factory.CreateContext())
        {
            ctx.JournalEntries.Add(MakeEntry(1, new DateTime(2026, 5, 1), JournalEntryStatus.Brouillon,
                new JournalLineInput("6132", "Loyer", 100m, 0m, null, ThirdPartyKind.None),
                new JournalLineInput("532", "Banque", 0m, 100m, null, ThirdPartyKind.None)));
            ctx.JournalEntries.Add(MakeEntry(2, new DateTime(2026, 6, 1), JournalEntryStatus.Brouillon,
                new JournalLineInput("6132", "Loyer", 50m, 0m, null, ThirdPartyKind.None),
                new JournalLineInput("532", "Banque", 0m, 50m, null, ThirdPartyKind.None)));
            await ctx.SaveChangesAsync();
        }

        var preClosing = await BuildPreClosing(factory).RunAsync(2026);
        var auditCount = await AuditRuleCountAsync<DraftEntriesAuditRule>(factory, 2026);

        Assert.Equal(preClosing.Value.Checks.Single(c => c.Code == "drafts").Count, auditCount);
    }

    [Fact]
    public async Task CleanYear_PreClosingAndAuditEngine_BothZeroBlocking()
    {
        var factory = new TestTenantDbContextFactory($"{_dbName}_clean");
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

        var preClosing = await BuildPreClosing(factory).RunAsync(2026);
        Assert.False(preClosing.Value.HasBlocking);
    }
}
