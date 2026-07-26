using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Centre de contrôle d'intégrité (lecture seule). Deux exigences structurantes : la PARITÉ avec le
/// pré-clôture pour les contrôles partagés (mêmes comptes), et la détection des anomalies
/// structurelles ajoutées. Le service ne mute jamais.
/// </summary>
public sealed class AccountingHealthServiceTests
{
    private readonly string _dbName = $"HealthDb_{Guid.NewGuid()}";
    private readonly TestTenantDbContextFactory _factory;

    public AccountingHealthServiceTests()
    {
        _factory = new TestTenantDbContextFactory(_dbName);
    }

    private sealed class TestTenantDbContextFactory : ITenantDbContextFactory
    {
        private readonly string _databaseName;
        public TestTenantDbContextFactory(string databaseName) => _databaseName = databaseName;
        public TenantDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseInMemoryDatabase(databaseName: _databaseName)
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

    private void SeedPeriod(int year, int month, bool closed)
    {
        var period = AccountingPeriod.Create(year, month, new DateTime(year, month, 1), new DateTime(year, month, 28));
        if (closed) period.Close("test");
        period.SetAuditInfo("test", false);
        using var ctx = _factory.CreateContext();
        ctx.AccountingPeriods.Add(period);
        ctx.SaveChanges();
    }

    private void Seed(params JournalEntry[] entries)
    {
        using var ctx = _factory.CreateContext();
        ctx.JournalEntries.AddRange(entries);
        ctx.SaveChanges();
    }

    private void SeedChart(params string[] accounts)
    {
        using var ctx = _factory.CreateContext();
        foreach (var a in accounts)
            ctx.ChartOfAccounts.Add(ChartOfAccount.Create(a, $"Compte {a}", int.Parse(a[..1]), null, AccountNatureType.Debit).Value);
        ctx.SaveChanges();
    }

    private PreClosingControlService BuildPreClosing()
    {
        var fixedAssets = new Mock<IFixedAssetRepository>();
        fixedAssets.Setup(r => r.GetUnpostedScheduleLinesForYearAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DepreciationScheduleLine>());
        return new PreClosingControlService(_factory, fixedAssets.Object,
            Options.Create(new AccountingSettings { UnletteredAgeThresholdDays = 90 }));
    }

    private AccountingHealthService BuildService(bool enabled = true)
        => new(_factory, BuildPreClosing(), Options.Create(new AccountingSettings { AccountingHealthEnabled = enabled }));

    private static PreClosingCheckDto Check(AccountingHealthReportDto dto, string code) =>
        dto.Checks.Single(c => c.Code == code);

    [Fact]
    public async Task Disabled_ReturnsFailure()
    {
        var result = await BuildService(enabled: false).RunAsync(2026);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Parity_SharedChecks_MatchPreClosingCounts()
    {
        // Un brouillon sur l'exercice : le contrôle « drafts » doit exister avec le même compte
        // que le pré-clôture appelé directement.
        Seed(MakeEntry(1, new DateTime(2026, 3, 10), JournalEntryStatus.Brouillon,
            new JournalLineInput("6132", "Loyer", 100m, 0m, null, ThirdPartyKind.None),
            new JournalLineInput("532", "Banque", 0m, 100m, null, ThirdPartyKind.None)));
        SeedPeriod(2026, 3, closed: false);
        SeedChart("6132", "532");

        var health = await BuildService().RunAsync(2026);
        var preClosing = await BuildPreClosing().RunAsync(2026);

        Assert.True(health.IsSuccess);
        Assert.True(preClosing.IsSuccess);
        Assert.Equal(preClosing.Value.Checks.Single(c => c.Code == "drafts").Count,
                     Check(health.Value, "drafts").Count);
        Assert.Equal(1, Check(health.Value, "drafts").Count);
        Assert.True(health.Value.HasAnomalies);
    }

    [Fact]
    public async Task OrphanAccount_IsDetected()
    {
        Seed(MakeEntry(1, new DateTime(2026, 3, 10), JournalEntryStatus.Validee,
            new JournalLineInput("6132", "Loyer", 100m, 0m, null, ThirdPartyKind.None),
            new JournalLineInput("99999", "Compte fantôme", 0m, 100m, null, ThirdPartyKind.None)));
        SeedPeriod(2026, 3, closed: false);
        SeedChart("6132");   // 99999 absent du plan

        var result = await BuildService().RunAsync(2026);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, Check(result.Value, "health-orphan-accounts").Count);
    }

    [Fact]
    public async Task OutOfPeriodEntry_IsDetected()
    {
        // Écriture en juin, mais seule la période de mars existe.
        Seed(MakeEntry(1, new DateTime(2026, 6, 10), JournalEntryStatus.Validee,
            new JournalLineInput("6132", "Loyer", 100m, 0m, null, ThirdPartyKind.None),
            new JournalLineInput("532", "Banque", 0m, 100m, null, ThirdPartyKind.None)));
        SeedPeriod(2026, 3, closed: false);
        SeedChart("6132", "532");

        var result = await BuildService().RunAsync(2026);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, Check(result.Value, "health-out-of-period").Count);
    }

    [Fact]
    public async Task DuplicatePieceNumber_IsDetected()
    {
        // Deux pièces JOD n°1 sur le même exercice.
        Seed(
            MakeEntry(1, new DateTime(2026, 3, 10), JournalEntryStatus.Validee,
                new JournalLineInput("6132", "Loyer", 100m, 0m, null, ThirdPartyKind.None),
                new JournalLineInput("532", "Banque", 0m, 100m, null, ThirdPartyKind.None)),
            MakeEntry(1, new DateTime(2026, 4, 15), JournalEntryStatus.Validee,
                new JournalLineInput("6132", "Loyer", 50m, 0m, null, ThirdPartyKind.None),
                new JournalLineInput("532", "Banque", 0m, 50m, null, ThirdPartyKind.None)));
        SeedPeriod(2026, 3, closed: false);
        SeedPeriod(2026, 4, closed: false);
        SeedChart("6132", "532");

        var result = await BuildService().RunAsync(2026);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, Check(result.Value, "health-piece-duplicates").Count);
    }

    [Fact]
    public async Task ThirdPartyOnNonAuxiliaryAccount_IsDetected()
    {
        var clientId = Guid.NewGuid();
        // Tiers rattaché à un compte de charges (613x) au lieu d'un compte 41x.
        Seed(MakeEntry(1, new DateTime(2026, 3, 10), JournalEntryStatus.Validee,
            new JournalLineInput("6132", "Loyer", 100m, 0m, clientId, ThirdPartyKind.Client),
            new JournalLineInput("532", "Banque", 0m, 100m, null, ThirdPartyKind.None)));
        SeedPeriod(2026, 3, closed: false);
        SeedChart("6132", "532");

        var result = await BuildService().RunAsync(2026);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, Check(result.Value, "health-thirdparty-mislink").Count);
    }

    [Fact]
    public async Task GlobalScope_OmitsPreClosingChecks_RunsStructuralOnly()
    {
        Seed(MakeEntry(1, new DateTime(2026, 3, 10), JournalEntryStatus.Validee,
            new JournalLineInput("6132", "Loyer", 100m, 0m, null, ThirdPartyKind.None),
            new JournalLineInput("532", "Banque", 0m, 100m, null, ThirdPartyKind.None)));
        SeedPeriod(2026, 3, closed: false);
        SeedChart("6132", "532");

        var result = await BuildService().RunAsync(null);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.FiscalYear);
        // Les contrôles par exercice (drafts, unbalanced…) ne sont pas présents en mode global.
        Assert.DoesNotContain(result.Value.Checks, c => c.Code == "drafts");
        // Mais les contrôles structurels le sont.
        Assert.Contains(result.Value.Checks, c => c.Code == "health-orphan-accounts");
        Assert.False(result.Value.HasAnomalies);
    }
}
