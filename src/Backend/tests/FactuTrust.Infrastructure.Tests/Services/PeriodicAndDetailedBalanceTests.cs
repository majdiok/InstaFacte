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
/// Balance par période (12 colonnes mensuelles) et balance détaillée (soldes + mouvements).
/// Les deux états sont des restitutions : ils doivent s'articuler exactement avec la balance
/// générale de la même période — c'est le contrôle qui les valide.
/// </summary>
public sealed class PeriodicAndDetailedBalanceTests
{
    private readonly string _dbName = $"PeriodicBalanceDb_{Guid.NewGuid()}";
    private readonly TestTenantDbContextFactory _factory;

    public PeriodicAndDetailedBalanceTests()
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
            Entry(1, new DateTime(2025, 12, 5), "4111", "707", 500m),    // exercice antérieur
            Entry(2, new DateTime(2026, 1, 15), "4111", "707", 1000m),
            Entry(3, new DateTime(2026, 3, 20), "4111", "707", 200m),
            Entry(4, new DateTime(2026, 3, 25), "532", "4111", 700m));
        ctx.ChartOfAccounts.AddRange(
            ChartOfAccount.Create("4111", "Clients", 4, "411", AccountNatureType.Debit).Value,
            ChartOfAccount.Create("707", "Ventes de marchandises", 7, null, AccountNatureType.Credit).Value);
        ctx.SaveChanges();
    }

    private AccountingReportingService BuildService()
        => new(_factory, Options.Create(new AccountingSettings()));

    // ── Balance par période ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task BalanceParPeriode_VentileLesMouvementsSurDouzeColonnes()
    {
        var result = await BuildService().GetBalanceByPeriodAsync(2026);

        Assert.True(result.IsSuccess);
        var clients = result.Value.Rows.Single(r => r.AccountNumber == "4111");

        Assert.Equal(12, clients.MonthlyDebit.Count);
        Assert.Equal(12, clients.MonthlyCredit.Count);
        Assert.Equal(500m, clients.Opening);                 // report de 2025
        Assert.Equal(1000m, clients.MonthlyDebit[0]);        // janvier
        Assert.Equal(0m, clients.MonthlyDebit[1]);           // février sans mouvement
        Assert.Equal(200m, clients.MonthlyDebit[2]);         // mars
        Assert.Equal(700m, clients.MonthlyCredit[2]);
        Assert.Equal(1000m, clients.Closing);                // 500 + 1200 − 700
    }

    [Fact]
    public async Task Articulation_SommeDesDouzeColonnes_EgaleLesMouvementsDeLaBalance()
    {
        var service = BuildService();
        var periodic = await service.GetBalanceByPeriodAsync(2026);
        var balance = await service.GetBalanceAsync(new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));

        Assert.True(periodic.IsSuccess);
        Assert.True(balance.IsSuccess);

        Assert.Equal(balance.Value.Sum(r => r.MovementDebit), periodic.Value.TotalDebit);
        Assert.Equal(balance.Value.Sum(r => r.MovementCredit), periodic.Value.TotalCredit);
        Assert.True(periodic.Value.IsBalanced);

        foreach (var row in periodic.Value.Rows)
        {
            var reference = balance.Value.Single(r => r.AccountNumber == row.AccountNumber);
            Assert.Equal(reference.MovementDebit, row.MonthlyDebit.Sum());
            Assert.Equal(reference.MovementCredit, row.MonthlyCredit.Sum());
            Assert.Equal(reference.ClosingDebit - reference.ClosingCredit, row.Closing);
        }
    }

    // ── Balance détaillée ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task BalanceDetaillee_JointChaqueSoldeAuDetailDeSesMouvements()
    {
        var result = await BuildService().GetDetailedBalanceAsync(
            null, null, new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));

        Assert.True(result.IsSuccess);
        var clients = result.Value.Accounts.Single(a => a.Balance.AccountNumber == "4111");

        Assert.Equal(3, clients.Rows.Count);
        Assert.Equal(1200m, clients.Balance.MovementDebit);
        Assert.Equal(700m, clients.Balance.MovementCredit);
    }

    [Fact]
    public async Task Articulation_SousTotalDuDetail_EgaleLaLigneDeBalance()
    {
        var result = await BuildService().GetDetailedBalanceAsync(
            null, null, new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));

        Assert.True(result.IsSuccess);
        foreach (var account in result.Value.Accounts)
        {
            Assert.Equal(account.Balance.MovementDebit, account.Rows.Sum(r => r.Debit));
            Assert.Equal(account.Balance.MovementCredit, account.Rows.Sum(r => r.Credit));
        }

        Assert.Equal(result.Value.TotalMovementDebit, result.Value.TotalMovementCredit);
    }

    [Fact]
    public async Task BalanceDetaillee_RespecteLaPlageDeComptes()
    {
        var result = await BuildService().GetDetailedBalanceAsync(
            "5", "6", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));

        Assert.True(result.IsSuccess);
        Assert.All(result.Value.Accounts, a => Assert.StartsWith("5", a.Balance.AccountNumber));
    }
}
