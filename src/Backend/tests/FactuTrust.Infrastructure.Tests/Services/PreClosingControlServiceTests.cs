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
/// Lot F : contrôles de pré-clôture — sévérités, HasBlocking et non-régression.
/// </summary>
public sealed class PreClosingControlServiceTests
{
    private readonly string _dbName = $"PreClosingDb_{Guid.NewGuid()}";
    private readonly TestTenantDbContextFactory _factory;

    public PreClosingControlServiceTests()
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

    private PreClosingControlService BuildService(IReadOnlyList<DepreciationScheduleLine>? unposted = null)
    {
        var fixedAssets = new Mock<IFixedAssetRepository>();
        fixedAssets.Setup(r => r.GetUnpostedScheduleLinesForYearAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(unposted ?? Array.Empty<DepreciationScheduleLine>());
        var settings = Options.Create(new AccountingSettings { UnletteredAgeThresholdDays = 90 });
        return new PreClosingControlService(_factory, fixedAssets.Object, settings);
    }

    private static PreClosingCheckDto Check(PreClosingChecklistDto dto, string code) =>
        dto.Checks.Single(c => c.Code == code);

    [Fact]
    public async Task Run_CleanYear_HasNoBlocking()
    {
        Seed(MakeEntry(1, new DateTime(2026, 3, 10), JournalEntryStatus.Validee,
            new JournalLineInput("6132", "Loyer", 100m, 0m, null, ThirdPartyKind.None),
            new JournalLineInput("532", "Banque", 0m, 100m, null, ThirdPartyKind.None)));
        SeedPeriod(2026, 3, closed: true);

        var result = await BuildService().RunAsync(2026);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.HasBlocking);
        Assert.Equal(0, Check(result.Value, "drafts").Count);
        Assert.Equal(0, Check(result.Value, "unbalanced").Count);
    }

    [Fact]
    public async Task Run_WithDraft_IsBlocking()
    {
        Seed(MakeEntry(1, new DateTime(2026, 4, 5), JournalEntryStatus.Brouillon,
            new JournalLineInput("6132", "Loyer", 100m, 0m, null, ThirdPartyKind.None),
            new JournalLineInput("532", "Banque", 0m, 100m, null, ThirdPartyKind.None)));

        var result = await BuildService().RunAsync(2026);

        Assert.True(result.Value.HasBlocking);
        var draftCheck = Check(result.Value, "drafts");
        Assert.Equal(1, draftCheck.Count);
        Assert.Equal((int)PreClosingSeverity.Blocking, draftCheck.Severity);
    }

    [Fact]
    public async Task Run_UnsettledSuspenseAccount_IsWarning()
    {
        // Compte 471 mouvementé mais non soldé (100 D sans contrepartie sur 471).
        Seed(MakeEntry(1, new DateTime(2026, 6, 15), JournalEntryStatus.Validee,
            new JournalLineInput("471", "Attente", 100m, 0m, null, ThirdPartyKind.None),
            new JournalLineInput("532", "Banque", 0m, 100m, null, ThirdPartyKind.None)));

        var result = await BuildService().RunAsync(2026);

        var suspense = Check(result.Value, "suspense");
        Assert.Equal(1, suspense.Count);
        Assert.Equal((int)PreClosingSeverity.Warning, suspense.Severity);
        Assert.False(result.Value.HasBlocking); // warning ne bloque pas
    }

    [Fact]
    public async Task Run_UnpostedDepreciation_IsWarning()
    {
        var line = DepreciationScheduleLine.Create(
            Guid.NewGuid(), 2026, null, 4000m, 1000m, 0m, 1000m, 1000m, 3000m).Value;

        var result = await BuildService(new[] { line }).RunAsync(2026);

        var dep = Check(result.Value, "depreciation");
        Assert.Equal(1, dep.Count);
        Assert.Equal((int)PreClosingSeverity.Warning, dep.Severity);
        Assert.Equal("/accounting/fixed-assets/depreciation-run", dep.DeepLinkRoute);
    }

    [Fact]
    public async Task Run_OpenPeriods_AreReported()
    {
        SeedPeriod(2026, 1, closed: false);
        SeedPeriod(2026, 2, closed: true);

        var result = await BuildService().RunAsync(2026);

        Assert.Equal(1, Check(result.Value, "open-periods").Count);
    }

    [Fact]
    public async Task Run_InvalidYear_Fails()
    {
        var result = await BuildService().RunAsync(1999);
        Assert.True(result.IsFailure);
    }
}
