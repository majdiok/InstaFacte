using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Lot C : balance auxiliaire (une ligne par tiers) et grand livre d'un tiers.
/// Vérifie l'exactitude ouverture/mouvements/clôture, le solde progressif, le nommage
/// batché des tiers et la politique brouillard (consultation).
/// </summary>
public sealed class AuxiliaryReportingTests
{
    private readonly string _dbName = $"AuxDb_{Guid.NewGuid()}";
    private readonly TestTenantDbContextFactory _factory;
    private readonly Guid _clientA = Guid.NewGuid();
    private readonly Guid _clientB = Guid.NewGuid();

    public AuxiliaryReportingTests()
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

    private static JournalEntry MakeClientSale(int number, DateTime date, decimal amount, Guid clientId,
        JournalEntryStatus status = JournalEntryStatus.Validee)
    {
        var lines = new[]
        {
            new JournalLineInput("4111", "Client", amount, 0m, clientId, ThirdPartyKind.Client),
            new JournalLineInput("707", "Vente", 0m, amount, null, ThirdPartyKind.None)
        };
        var e = JournalEntry.Create(number, "JV", date, $"Vente {number}", Guid.NewGuid(),
            false, "Manual", null, lines, initialStatus: status).Value;
        e.SetAuditInfo("test", false);
        return e;
    }

    private static JournalEntry MakeClientPayment(int number, DateTime date, decimal amount, Guid clientId)
    {
        var lines = new[]
        {
            new JournalLineInput("532", "Banque", amount, 0m, null, ThirdPartyKind.None),
            new JournalLineInput("4111", "Règlement", 0m, amount, clientId, ThirdPartyKind.Client)
        };
        var e = JournalEntry.Create(number, "JB", date, $"Règlement {number}", Guid.NewGuid(),
            false, "Manual", null, lines).Value;
        e.SetAuditInfo("test", false);
        return e;
    }

    private void Seed()
    {
        using var ctx = _factory.CreateContext();
        // Client A : vente 100 avant période (ouverture), vente 50 + règlement 30 dans la période.
        ctx.JournalEntries.Add(MakeClientSale(1, new DateTime(2026, 1, 15), 100m, _clientA));
        ctx.JournalEntries.Add(MakeClientSale(2, new DateTime(2026, 5, 10), 50m, _clientA));
        ctx.JournalEntries.Add(MakeClientPayment(3, new DateTime(2026, 5, 20), 30m, _clientA));
        // Client B : brouillon de 40 dans la période (politique brouillard).
        ctx.JournalEntries.Add(MakeClientSale(4, new DateTime(2026, 5, 12), 40m, _clientB, JournalEntryStatus.Brouillon));
        ctx.Clients.Add(MakeClient(_clientA, "Client Alpha"));
        ctx.Clients.Add(MakeClient(_clientB, "Client Beta"));
        ctx.SaveChanges();
    }

    private static Client MakeClient(Guid id, string name)
    {
        var address = Address.Create("1 rue de la République", "Tunis", "Tunis").Value;
        var email = Email.Create($"{name.Replace(' ', '.').ToLowerInvariant()}@example.tn").Value;
        var client = Client.Create(name, ClientType.Individual, address, email).Value;
        // Les lignes d'écriture référencent un ThirdPartyId connu : on force l'Id (setter protégé).
        typeof(FactuTrust.Domain.Common.Entity).GetProperty("Id")!.SetValue(client, id);
        client.SetAuditInfo("test", false);
        return client;
    }

    private AccountingReportingService BuildService(bool includeDrafts = true)
        => new(_factory, Options.Create(new AccountingSettings { IncludeBrouillardInReports = includeDrafts }));

    private static readonly DateTime From = new(2026, 5, 1);
    private static readonly DateTime To = new(2026, 5, 31);

    [Fact]
    public async Task AuxiliaryBalance_ComputesOpeningMovementsClosing()
    {
        var result = await BuildService().GetAuxiliaryBalanceAsync(ThirdPartyKind.Client, From, To);

        Assert.True(result.IsSuccess);
        var alpha = result.Value.Single(r => r.ThirdPartyId == _clientA);
        Assert.Equal("Client Alpha", alpha.ThirdPartyName);
        Assert.Equal(100m, alpha.OpeningDebit);   // vente de janvier
        Assert.Equal(0m, alpha.OpeningCredit);
        Assert.Equal(50m, alpha.MovementDebit);   // vente de mai
        Assert.Equal(30m, alpha.MovementCredit);  // règlement de mai
        Assert.Equal(120m, alpha.ClosingDebit);   // 100 + 50 − 30
        Assert.Equal(0m, alpha.ClosingCredit);
    }

    [Fact]
    public async Task AuxiliaryBalance_DraftPolicy_FollowsFlag()
    {
        var withDrafts = await BuildService(includeDrafts: true).GetAuxiliaryBalanceAsync(ThirdPartyKind.Client, From, To);
        var withoutDrafts = await BuildService(includeDrafts: false).GetAuxiliaryBalanceAsync(ThirdPartyKind.Client, From, To);

        Assert.Contains(withDrafts.Value, r => r.ThirdPartyId == _clientB && r.MovementDebit == 40m);
        Assert.DoesNotContain(withoutDrafts.Value, r => r.ThirdPartyId == _clientB);
    }

    [Fact]
    public async Task AuxiliaryBalance_Suppliers_EmptyWhenNoSupplierLines()
    {
        var result = await BuildService().GetAuxiliaryBalanceAsync(ThirdPartyKind.Supplier, From, To);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task ThirdPartyLedger_RunningBalanceStartsAtOpening()
    {
        var result = await BuildService().GetThirdPartyLedgerAsync(_clientA, ThirdPartyKind.Client, From, To);

        Assert.True(result.IsSuccess);
        var ledger = result.Value;
        Assert.Equal("Client Alpha", ledger.ThirdPartyName);
        Assert.Equal(100m, ledger.OpeningBalance);
        Assert.Equal(2, ledger.Rows.Count);
        Assert.Equal(150m, ledger.Rows[0].RunningBalance); // 100 + 50
        Assert.Equal(120m, ledger.Rows[1].RunningBalance); // 150 − 30
        // Cohérence drill-down : dernier solde = clôture de la balance auxiliaire.
        var balance = await BuildService().GetAuxiliaryBalanceAsync(ThirdPartyKind.Client, From, To);
        Assert.Equal(balance.Value.Single(r => r.ThirdPartyId == _clientA).ClosingDebit,
            ledger.Rows[^1].RunningBalance);
    }
}
