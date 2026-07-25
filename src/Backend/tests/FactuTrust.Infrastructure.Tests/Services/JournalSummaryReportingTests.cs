using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
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
/// Récapitulatifs de journaux : centralisateur (journaux × mois), récapitulation
/// (journaux × comptes) et totaux journaux. Le contrôle structurant est l'articulation :
/// la somme des totaux par journal doit égaler le total général du journal sur la même période.
/// </summary>
public sealed class JournalSummaryReportingTests
{
    private readonly string _dbName = $"JournalSummaryDb_{Guid.NewGuid()}";
    private readonly TestTenantDbContextFactory _factory;

    public JournalSummaryReportingTests()
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

    private static JournalEntry Entry(
        int number, string journalCode, DateTime date, string debitAccount, string creditAccount,
        decimal amount, JournalEntryStatus status = JournalEntryStatus.Validee)
    {
        var lines = new[]
        {
            new JournalLineInput(debitAccount, "Débit", amount, 0m, null, ThirdPartyKind.None),
            new JournalLineInput(creditAccount, "Crédit", 0m, amount, null, ThirdPartyKind.None)
        };
        var entry = JournalEntry.Create(
            number, journalCode, date, $"Pièce {number}", Guid.NewGuid(),
            false, "Manual", null, lines, initialStatus: status).Value;
        entry.SetAuditInfo("test", false);
        return entry;
    }

    private void Seed()
    {
        using var ctx = _factory.CreateContext();
        ctx.JournalEntries.AddRange(
            Entry(1, "JV", new DateTime(2026, 1, 15), "4111", "707", 1000m),
            Entry(2, "JV", new DateTime(2026, 2, 20), "4111", "707", 500m),
            Entry(3, "JA", new DateTime(2026, 2, 10), "607", "401", 300m),
            Entry(4, "JB", new DateTime(2026, 3, 5), "532", "4111", 800m));
        ctx.Journals.AddRange(
            Journal.Create("JV", "Journal des ventes", null, true).Value,
            Journal.Create("JA", "Journal des achats", null, true).Value,
            Journal.Create("JB", "Journal de banque", null, true).Value);
        ctx.ChartOfAccounts.AddRange(
            ChartOfAccount.Create("4111", "Clients", 4, "411", AccountNatureType.Debit).Value,
            ChartOfAccount.Create("707", "Ventes de marchandises", 7, null, AccountNatureType.Credit).Value);
        ctx.SaveChanges();
    }

    private AccountingReportingService BuildService(bool includeDrafts = false)
        => new(_factory, Options.Create(new AccountingSettings { IncludeBrouillardInReports = includeDrafts }));

    private static readonly DateTime From = new(2026, 1, 1);
    private static readonly DateTime To = new(2026, 12, 31);

    [Fact]
    public async Task Centralisateur_CroiseJournauxEtMois()
    {
        var result = await BuildService().GetJournalSummaryAsync(From, To, JournalSummaryGrouping.Month);

        Assert.True(result.IsSuccess);
        var summary = result.Value;

        // JV : janvier (1000) et février (500) — deux cases distinctes.
        var jvCells = summary.Cells.Where(c => c.JournalCode == "JV").OrderBy(c => c.Month).ToList();
        Assert.Equal(2, jvCells.Count);
        Assert.Equal(1, jvCells[0].Month);
        Assert.Equal(1000m, jvCells[0].Debit);
        Assert.Equal(2, jvCells[1].Month);
        Assert.Equal(500m, jvCells[1].Debit);

        // Les colonnes couvrent tous les mois de la période, y compris ceux sans mouvement.
        Assert.Equal(12, summary.Periods.Count);
        Assert.Equal("01/2026", summary.Periods[0].Label);
    }

    [Fact]
    public async Task Recapitulation_CroiseJournauxEtComptes()
    {
        var result = await BuildService().GetJournalSummaryAsync(From, To, JournalSummaryGrouping.Account);

        Assert.True(result.IsSuccess);
        var jvClients = result.Value.Cells.Single(c => c.JournalCode == "JV" && c.AccountNumber == "4111");
        Assert.Equal(1500m, jvClients.Debit);   // 1000 + 500
        Assert.Equal("Clients", jvClients.AccountLabel);

        var jvVentes = result.Value.Cells.Single(c => c.JournalCode == "JV" && c.AccountNumber == "707");
        Assert.Equal(1500m, jvVentes.Credit);
    }

    [Fact]
    public async Task Totaux_NeRenvoieQueLesSousTotauxParJournal()
    {
        var result = await BuildService().GetJournalSummaryAsync(From, To, JournalSummaryGrouping.Totals);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Cells);
        Assert.Equal(3, result.Value.JournalTotals.Count);

        var jv = result.Value.JournalTotals.Single(t => t.JournalCode == "JV");
        Assert.Equal("Journal des ventes", jv.JournalLabel);
        Assert.Equal(1500m, jv.Debit);
        Assert.Equal(2, jv.EntryCount);         // deux pièces, pas quatre lignes
    }

    [Fact]
    public async Task Articulation_SommeDesTotauxJournaux_EgaleLeTotalDuJournalGeneral()
    {
        var service = BuildService();

        var summary = await service.GetJournalSummaryAsync(From, To, JournalSummaryGrouping.Totals);
        var journal = await service.GetJournalEntriesAsync(null, From, To);

        Assert.True(summary.IsSuccess);
        Assert.True(journal.IsSuccess);

        var journalDebit = journal.Value.SelectMany(e => e.Lines).Sum(l => l.Debit);
        var journalCredit = journal.Value.SelectMany(e => e.Lines).Sum(l => l.Credit);

        Assert.Equal(journalDebit, summary.Value.TotalDebit);
        Assert.Equal(journalCredit, summary.Value.TotalCredit);
        Assert.True(summary.Value.IsBalanced);
    }

    [Fact]
    public async Task Articulation_SommeDesCellulesMensuelles_EgaleLeTotalGeneral()
    {
        var result = await BuildService().GetJournalSummaryAsync(From, To, JournalSummaryGrouping.Month);

        Assert.True(result.IsSuccess);
        Assert.Equal(result.Value.TotalDebit, result.Value.Cells.Sum(c => c.Debit));
        Assert.Equal(result.Value.TotalCredit, result.Value.Cells.Sum(c => c.Credit));
    }

    [Fact]
    public async Task FiltreParJournal_RestreintLeRecapitulatif()
    {
        var result = await BuildService().GetJournalSummaryAsync(
            From, To, JournalSummaryGrouping.Month, journalCode: "jv");

        Assert.True(result.IsSuccess);
        Assert.All(result.Value.Cells, c => Assert.Equal("JV", c.JournalCode));
        Assert.Equal(1500m, result.Value.TotalDebit);
    }

    [Fact]
    public async Task PolitiqueBrouillard_SuitLeDrapeauDeConsultation()
    {
        using (var ctx = _factory.CreateContext())
        {
            ctx.JournalEntries.Add(Entry(5, "JV", new DateTime(2026, 4, 1), "4111", "707", 250m, JournalEntryStatus.Brouillon));
            ctx.SaveChanges();
        }

        var withDrafts = await BuildService(includeDrafts: true)
            .GetJournalSummaryAsync(From, To, JournalSummaryGrouping.Totals);
        var withoutDrafts = await BuildService(includeDrafts: false)
            .GetJournalSummaryAsync(From, To, JournalSummaryGrouping.Totals);

        Assert.Equal(2850m, withDrafts.Value.TotalDebit);      // 2600 + 250
        Assert.Equal(2600m, withoutDrafts.Value.TotalDebit);
    }

    [Fact]
    public async Task PeriodeInversee_EstRefusee()
    {
        var result = await BuildService().GetJournalSummaryAsync(To, From, JournalSummaryGrouping.Month);

        Assert.True(result.IsFailure);
    }
}
