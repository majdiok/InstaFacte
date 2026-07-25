using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Grand livre général (comptes en séquence) et récapitulatif par racine. Le contrôle structurant
/// est l'articulation avec la balance générale : mêmes mouvements, mêmes soldes de clôture.
/// </summary>
public sealed class GeneralLedgerReportingTests
{
    private readonly string _dbName = $"GeneralLedgerDb_{Guid.NewGuid()}";
    private readonly TestTenantDbContextFactory _factory;

    public GeneralLedgerReportingTests()
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

    private static JournalEntry Entry(int number, DateTime date, string debitAccount, string creditAccount, decimal amount)
    {
        var lines = new[]
        {
            new JournalLineInput(debitAccount, "Débit", amount, 0m, null, ThirdPartyKind.None),
            new JournalLineInput(creditAccount, "Crédit", 0m, amount, null, ThirdPartyKind.None)
        };
        var entry = JournalEntry.Create(
            number, "JV", date, $"Pièce {number}", Guid.NewGuid(),
            false, "Manual", null, lines).Value;
        entry.SetAuditInfo("test", false);
        return entry;
    }

    private void Seed()
    {
        using var ctx = _factory.CreateContext();
        ctx.JournalEntries.AddRange(
            Entry(1, new DateTime(2025, 11, 10), "4111", "707", 400m),   // exercice antérieur
            Entry(2, new DateTime(2026, 1, 15), "4111", "707", 1000m),
            Entry(3, new DateTime(2026, 2, 20), "532", "4111", 600m),
            Entry(4, new DateTime(2026, 3, 5), "607", "401", 250m));
        ctx.ChartOfAccounts.AddRange(
            ChartOfAccount.Create("4111", "Clients", 4, "411", AccountNatureType.Debit).Value,
            ChartOfAccount.Create("707", "Ventes de marchandises", 7, null, AccountNatureType.Credit).Value,
            ChartOfAccount.Create("532", "Banque", 5, null, AccountNatureType.Debit).Value);
        ctx.SaveChanges();
    }

    private AccountingReportingService BuildService()
        => new(_factory, Options.Create(new AccountingSettings()));

    private static readonly DateTime From = new(2026, 1, 1);
    private static readonly DateTime To = new(2026, 12, 31);

    [Fact]
    public async Task GrandLivreGeneral_RestitueLesComptesEnSequenceAvecReportEtSousTotal()
    {
        var result = await BuildService().GetLedgerRangeAsync(null, null, From, To);

        Assert.True(result.IsSuccess);
        var clients = result.Value.Accounts.Single(a => a.AccountNumber == "4111");

        Assert.Equal("Clients", clients.Label);
        Assert.Equal(400m, clients.OpeningBalance);          // report de l'exercice antérieur
        Assert.Equal(2, clients.Rows.Count);
        Assert.Equal(1000m, clients.TotalDebit);
        Assert.Equal(600m, clients.TotalCredit);
        Assert.Equal(800m, clients.ClosingBalance);          // 400 + 1000 − 600

        // Le solde progressif démarre au report, contrairement au grand livre mono-compte.
        Assert.Equal(1400m, clients.Rows[0].RunningBalance);
        Assert.Equal(800m, clients.Rows[1].RunningBalance);
    }

    [Fact]
    public async Task Articulation_GrandLivreGeneral_EgaleLaBalanceGenerale()
    {
        var service = BuildService();
        var ledger = await service.GetLedgerRangeAsync(null, null, From, To);
        var balance = await service.GetBalanceAsync(From, To);

        Assert.True(ledger.IsSuccess);
        Assert.True(balance.IsSuccess);

        Assert.Equal(balance.Value.Sum(r => r.MovementDebit), ledger.Value.TotalDebit);
        Assert.Equal(balance.Value.Sum(r => r.MovementCredit), ledger.Value.TotalCredit);
        Assert.True(ledger.Value.IsBalanced);

        foreach (var account in ledger.Value.Accounts)
        {
            var row = balance.Value.Single(r => r.AccountNumber == account.AccountNumber);
            Assert.Equal(row.ClosingDebit - row.ClosingCredit, account.ClosingBalance);
        }
    }

    [Fact]
    public async Task ModePlage_EtModeMonoCompte_DonnentLesMemesMouvements()
    {
        var service = BuildService();
        var range = await service.GetLedgerRangeAsync("4111", "4111", From, To);
        var single = await service.GetLedgerAsync("4111", From, To);

        Assert.True(range.IsSuccess);
        Assert.True(single.IsSuccess);

        var account = Assert.Single(range.Value.Accounts);
        Assert.Equal(single.Value.Count, account.Rows.Count);
        Assert.Equal(
            single.Value.Select(r => (r.EntryDate, r.Debit, r.Credit)),
            account.Rows.Select(r => (r.EntryDate, r.Debit, r.Credit)));
    }

    [Fact]
    public async Task PlageDeComptes_RestreintLaSelection()
    {
        var result = await BuildService().GetLedgerRangeAsync("5", "6", From, To);

        Assert.True(result.IsSuccess);
        Assert.All(result.Value.Accounts, a => Assert.StartsWith("5", a.AccountNumber));
        // Une plage partielle n'a pas vocation à être équilibrée.
        Assert.False(result.Value.IsBalanced);
    }

    [Fact]
    public async Task PlageInversee_EstRefusee()
    {
        var result = await BuildService().GetLedgerRangeAsync("7", "4", From, To);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Recapitulatif_AgregeParRacineEtRetrouveLesTotauxDeLaBalance()
    {
        var service = BuildService();
        var recap = await service.GetLedgerRecapAsync(2, From, To);
        var balance = await service.GetBalanceAsync(From, To);

        Assert.True(recap.IsSuccess);
        Assert.All(recap.Value, r => Assert.Equal(2, r.AccountNumber.Length));

        // 4111 et 401 se regroupent sous « 41 » et « 40 » : la racine 41 porte les mouvements clients.
        var racine41 = recap.Value.Single(r => r.AccountNumber == "41");
        Assert.Equal(1000m, racine41.MovementDebit);
        Assert.Equal(600m, racine41.MovementCredit);

        Assert.Equal(balance.Value.Sum(r => r.MovementDebit), recap.Value.Sum(r => r.MovementDebit));
        Assert.Equal(balance.Value.Sum(r => r.MovementCredit), recap.Value.Sum(r => r.MovementCredit));
    }

    [Fact]
    public async Task Recapitulatif_NiveauHorsBornes_EstRefuse()
    {
        var result = await BuildService().GetLedgerRecapAsync(0, From, To);
        Assert.True(result.IsFailure);
    }
}
