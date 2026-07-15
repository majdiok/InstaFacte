using System.Text;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
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

public sealed class JournalImportServiceTests : IDisposable
{
    private readonly string _dbName = $"ImportTestDb_{Guid.NewGuid()}";
    private readonly TestTenantDbContextFactory _factory;

    public JournalImportServiceTests()
    {
        _factory = new TestTenantDbContextFactory(_dbName);
        SeedAccounts();
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

    private void SeedAccounts()
    {
        using var ctx = _factory.CreateContext();
        ctx.ChartOfAccounts.Add(ChartOfAccount.Create("4111", "Clients", 4, "411", AccountNatureType.Debit, isSystem: false).Value);
        ctx.ChartOfAccounts.Add(ChartOfAccount.Create("707", "Ventes", 7, null, AccountNatureType.Credit, isSystem: false).Value);
        ctx.SaveChanges();
    }

    private JournalImportService BuildService(bool enabled = true)
    {
        var periodService = new Mock<IAccountingPeriodService>();
        var period = AccountingPeriod.Create(2026, 1, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31));
        periodService
            .Setup(x => x.EnsureOpenPeriodAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(period));

        var settings = Options.Create(new AccountingSettings { DossierImportEnabled = enabled });
        return new JournalImportService(_factory, periodService.Object, settings);
    }

    private static byte[] BalancedCsv() => Encoding.UTF8.GetBytes(
        "journal,numero,date,compte,libelle,debit,credit\n" +
        "JV,1,2026-01-15,4111,Client X,100.000,0\n" +
        "JV,1,2026-01-15,707,Vente,0,100.000\n");

    [Fact]
    public async Task Preview_WhenDisabled_Fails()
    {
        var service = BuildService(enabled: false);

        var result = await service.PreviewAsync(BalancedCsv(), JournalImportFormat.Csv, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("activée", result.Error.Description);
    }

    [Fact]
    public async Task Preview_BalancedKnownAccounts_CanCommit()
    {
        var service = BuildService();

        var result = await service.PreviewAsync(BalancedCsv(), JournalImportFormat.Csv, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.CanCommit);
        Assert.Equal(1, result.Value.TotalEntries);
        Assert.Equal(2, result.Value.TotalLines);
        Assert.Empty(result.Value.Issues);
    }

    [Fact]
    public async Task Preview_UnknownAccount_CannotCommit()
    {
        var csv = Encoding.UTF8.GetBytes(
            "journal,numero,date,compte,libelle,debit,credit\n" +
            "JV,1,2026-01-15,9999,Inconnu,100,0\n" +
            "JV,1,2026-01-15,707,Vente,0,100\n");
        var service = BuildService();

        var result = await service.PreviewAsync(csv, JournalImportFormat.Csv, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.CanCommit);
        Assert.Contains(result.Value.Issues, i => i.Message.Contains("9999"));
    }

    [Fact]
    public async Task Commit_HappyPath_CreatesDraftEntry()
    {
        var service = BuildService();

        var result = await service.CommitAsync(BalancedCsv(), JournalImportFormat.Csv, CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Equal(1, result.Value.ImportedEntries);
        Assert.Equal(2, result.Value.ImportedLines);

        using var ctx = _factory.CreateContext();
        var entries = await ctx.JournalEntries.Include(j => j.Lines).ToListAsync();
        Assert.Single(entries);
        Assert.Equal(JournalEntryStatus.Brouillon, entries[0].Status);
        Assert.Equal(JournalImportService.SourceImport, entries[0].SourceEntityType);
        Assert.Equal(2, entries[0].Lines.Count);
        Assert.Equal("JV", entries[0].JournalCode);
    }

    [Fact]
    public async Task Commit_Unbalanced_IsRefusedAndWritesNothing()
    {
        var csv = Encoding.UTF8.GetBytes(
            "journal,numero,date,compte,libelle,debit,credit\n" +
            "JV,1,2026-01-15,4111,C,100,0\n" +
            "JV,1,2026-01-15,707,V,0,80\n");
        var service = BuildService();

        var result = await service.CommitAsync(csv, JournalImportFormat.Csv, CancellationToken.None);

        Assert.True(result.IsFailure);
        using var ctx = _factory.CreateContext();
        Assert.Empty(await ctx.JournalEntries.ToListAsync());
    }

    public void Dispose() => GC.SuppressFinalize(this);
}
