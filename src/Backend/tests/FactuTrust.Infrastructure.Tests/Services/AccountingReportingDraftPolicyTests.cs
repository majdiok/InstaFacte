using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Repositories;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// C3 : politique brouillard des états.
/// — Consultation (journal, grand livre, balance) : brouillons inclus selon
///   <see cref="AccountingSettings.IncludeBrouillardInReports"/>.
/// — Synthèse (bilan, compte de résultat) : brouillons exclus inconditionnellement.
/// — <c>SumDebitsByAccountAsync</c> (déclaration TVA) : brouillons exclus inconditionnellement.
/// </summary>
public sealed class AccountingReportingDraftPolicyTests
{
    private readonly string _dbName = $"DraftPolicyDb_{Guid.NewGuid()}";
    private readonly TestTenantDbContextFactory _factory;

    public AccountingReportingDraftPolicyTests()
    {
        _factory = new TestTenantDbContextFactory(_dbName);
        Seed();
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

    private static JournalEntry MakeEntry(int number, decimal amount, JournalEntryStatus status)
    {
        var lines = new[]
        {
            new JournalLineInput("4111", "Client", amount, 0m, null, ThirdPartyKind.None),
            new JournalLineInput("707", "Vente", 0m, amount, null, ThirdPartyKind.None)
        };
        var entry = JournalEntry.Create(
            number, "JV", new DateTime(2026, 5, 15), $"Vente {number}", Guid.NewGuid(),
            false, "Manual", null, lines, initialStatus: status).Value;
        entry.SetAuditInfo("test", false);
        return entry;
    }

    private void Seed()
    {
        using var ctx = _factory.CreateContext();
        // 1 écriture validée de 100 + 1 brouillon de 50 sur le même mois.
        ctx.JournalEntries.Add(MakeEntry(1, 100m, JournalEntryStatus.Validee));
        ctx.JournalEntries.Add(MakeEntry(2, 50m, JournalEntryStatus.Brouillon));
        ctx.ChartOfAccounts.Add(ChartOfAccount.Create("4111", "Clients", 4, "411", AccountNatureType.Debit).Value);
        ctx.ChartOfAccounts.Add(ChartOfAccount.Create("707", "Ventes", 7, null, AccountNatureType.Credit).Value);
        ctx.SaveChanges();
    }

    private AccountingReportingService BuildService(bool includeDrafts)
        => new(_factory, Options.Create(new AccountingSettings { IncludeBrouillardInReports = includeDrafts }));

    private static readonly DateTime From = new(2026, 1, 1);
    private static readonly DateTime To = new(2026, 12, 31);

    [Fact]
    public async Task Journal_FlagOn_IncludesMarkedDrafts()
    {
        var result = await BuildService(includeDrafts: true).GetJournalEntriesAsync(null, From, To);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.Contains(result.Value, e => e.IsDraft);
    }

    [Fact]
    public async Task Journal_FlagOff_ExcludesDrafts()
    {
        var result = await BuildService(includeDrafts: false).GetJournalEntriesAsync(null, From, To);

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(result.Value);
        Assert.False(entry.IsDraft);
    }

    [Fact]
    public async Task Ledger_FlagOff_ExcludesDraftLines()
    {
        var on = await BuildService(includeDrafts: true).GetLedgerAsync("4111", From, To);
        var off = await BuildService(includeDrafts: false).GetLedgerAsync("4111", From, To);

        Assert.Equal(2, on.Value.Count);
        var row = Assert.Single(off.Value);
        Assert.Equal(100m, row.Debit);
    }

    [Fact]
    public async Task Balance_FollowsFlag()
    {
        var on = await BuildService(includeDrafts: true).GetBalanceAsync(From, To);
        var off = await BuildService(includeDrafts: false).GetBalanceAsync(From, To);

        Assert.Equal(150m, on.Value.Single(r => r.AccountNumber == "4111").MovementDebit);
        Assert.Equal(100m, off.Value.Single(r => r.AccountNumber == "4111").MovementDebit);
    }

    [Fact]
    public async Task BalanceSheet_AlwaysExcludesDrafts_EvenWithFlagOn()
    {
        var result = await BuildService(includeDrafts: true).GetBalanceSheetAsync(2026);

        Assert.True(result.IsSuccess);
        var clients = result.Value.Assets.Single(a => a.AccountNumber == "4111");
        Assert.Equal(100m, clients.Amount);
    }

    [Fact]
    public async Task IncomeStatement_AlwaysExcludesDrafts_EvenWithFlagOn()
    {
        var result = await BuildService(includeDrafts: true).GetIncomeStatementAsync(2026);

        Assert.True(result.IsSuccess);
        Assert.Equal(100m, result.Value.TotalRevenue);
    }

    [Fact]
    public async Task SumDebitsByAccount_AlwaysExcludesDrafts()
    {
        var repo = new JournalEntryRepository(_factory);

        var sum = await repo.SumDebitsByAccountAsync("4111", From, To);

        Assert.Equal(100m, sum);
    }
}
