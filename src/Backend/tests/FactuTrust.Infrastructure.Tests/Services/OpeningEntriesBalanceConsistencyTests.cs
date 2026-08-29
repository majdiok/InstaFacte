using FactuTrust.Application.Common.Interfaces.Repositories;
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
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Ancrage d'exercice : quand un exercice porte une écriture d'à-nouveau, celle-ci RÉSUME les
/// soldes antérieurs. L'ouverture doit partir d'elle et non de tout l'historique, sinon le report
/// est compté deux fois (une fois par l'historique conservé, une fois par l'à-nouveau).
/// <para>
/// Le pendant indispensable de ces tests : sans à-nouveau, aucun chiffre ne bouge — c'est le test
/// de gel <see cref="Balance_SansANouveau_ComportementHistoriqueInchange"/>.
/// </para>
/// </summary>
public sealed class OpeningEntriesBalanceConsistencyTests
{
    private static readonly Guid ClientId = Guid.Parse("11111111-2222-3333-4444-555555555555");

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

    private static TestTenantDbContextFactory NewFactory() =>
        new($"AnchorDb_{Guid.NewGuid()}");

    private static AccountingReportingService BuildReporting(ITenantDbContextFactory factory) =>
        new(factory, Options.Create(new AccountingSettings()));

    /// <summary>Vente : 4111 au débit (avec tiers) / 707 au crédit.</summary>
    private static JournalEntry Sale(int number, DateTime date, decimal amount, Guid? thirdPartyId = null)
    {
        var kind = thirdPartyId.HasValue ? ThirdPartyKind.Client : ThirdPartyKind.None;
        var lines = new[]
        {
            new JournalLineInput("4111", "Client", amount, 0m, thirdPartyId, kind),
            new JournalLineInput("707", "Vente", 0m, amount, null, ThirdPartyKind.None)
        };
        var entry = JournalEntry.Create(
            number, "JV", date, $"Vente {number}", Guid.NewGuid(),
            false, "Manual", null, lines).Value;
        entry.SetAuditInfo("test", false);
        return entry;
    }

    /// <summary>À-nouveau : 4111 au débit (avec tiers) / 131 au crédit, source « OpeningBalance ».</summary>
    private static JournalEntry OpeningEntry(DateTime date, decimal amount, Guid? thirdPartyId = null)
    {
        var kind = thirdPartyId.HasValue ? ThirdPartyKind.Client : ThirdPartyKind.None;
        var lines = new[]
        {
            new JournalLineInput("4111", "À-nouveau — 4111", amount, 0m, thirdPartyId, kind),
            new JournalLineInput("131", "Résultat reporté", 0m, amount, null, ThirdPartyKind.None)
        };
        var entry = JournalEntry.Create(
            1, "JAN", date, $"Écritures d'à-nouveau — Exercice {date.Year - 1}", Guid.NewGuid(),
            true, AccountingService.SourceOpeningBalance, Guid.NewGuid(), lines).Value;
        entry.SetAuditInfo("test", false);
        return entry;
    }

    private static void Seed(ITenantDbContextFactory factory, params JournalEntry[] entries)
    {
        using var ctx = factory.CreateContext();
        ctx.JournalEntries.AddRange(entries);
        ctx.SaveChanges();
    }

    private static decimal Closing(IReadOnlyList<BalanceRowDto> rows, string account)
    {
        var row = rows.Single(r => r.AccountNumber == account);
        return row.ClosingDebit - row.ClosingCredit;
    }

    private static decimal Opening(IReadOnlyList<BalanceRowDto> rows, string account)
    {
        var row = rows.Single(r => r.AccountNumber == account);
        return row.OpeningDebit - row.OpeningCredit;
    }

    // ── Gel du comportement historique ─────────────────────────────────────────────────────

    [Fact]
    public async Task Balance_SansANouveau_ComportementHistoriqueInchange()
    {
        // Aucun à-nouveau : l'ouverture reste la somme de tout l'historique antérieur.
        var factory = NewFactory();
        Seed(factory,
            Sale(1, new DateTime(2025, 6, 15), 1000m),
            Sale(2, new DateTime(2026, 4, 10), 300m));

        var result = await BuildReporting(factory)
            .GetBalanceAsync(new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));

        Assert.True(result.IsSuccess);
        Assert.Equal(1000m, Opening(result.Value, "4111"));
        Assert.Equal(300m, result.Value.Single(r => r.AccountNumber == "4111").MovementDebit);
        Assert.Equal(1300m, Closing(result.Value, "4111"));
    }

    // ── Correction du double comptage ──────────────────────────────────────────────────────

    [Fact]
    public async Task Balance_AvecANouveau_NeCompteLeReportQuUneFois()
    {
        var factory = NewFactory();
        Seed(factory,
            Sale(1, new DateTime(2025, 6, 15), 1000m),           // exercice clôturé
            OpeningEntry(new DateTime(2026, 1, 1), 1000m),       // report du solde 2025
            Sale(2, new DateTime(2026, 4, 10), 300m));           // mouvement réel 2026

        var result = await BuildReporting(factory)
            .GetBalanceAsync(new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));

        Assert.True(result.IsSuccess);
        // Ouverture = à-nouveau seul (et non l'historique 2025 qui ferait double emploi).
        Assert.Equal(1000m, Opening(result.Value, "4111"));
        // Mouvements = la vente 2026 seule (l'à-nouveau est déjà dans l'ouverture).
        Assert.Equal(300m, result.Value.Single(r => r.AccountNumber == "4111").MovementDebit);
        // Clôture = 1300 et non 2300.
        Assert.Equal(1300m, Closing(result.Value, "4111"));
    }

    [Fact]
    public async Task Balance_SurUnMois_OuvertureIntegreANouveauEtMouvementsAnterieurs()
    {
        var factory = NewFactory();
        Seed(factory,
            Sale(1, new DateTime(2025, 6, 15), 1000m),
            OpeningEntry(new DateTime(2026, 1, 1), 1000m),
            Sale(2, new DateTime(2026, 2, 10), 200m),            // antérieur à la période
            Sale(3, new DateTime(2026, 3, 20), 300m));           // dans la période

        var result = await BuildReporting(factory)
            .GetBalanceAsync(new DateTime(2026, 3, 1), new DateTime(2026, 3, 31));

        Assert.True(result.IsSuccess);
        Assert.Equal(1200m, Opening(result.Value, "4111"));      // à-nouveau + février
        Assert.Equal(300m, result.Value.Single(r => r.AccountNumber == "4111").MovementDebit);
        Assert.Equal(1500m, Closing(result.Value, "4111"));
    }

    [Fact]
    public async Task BalanceAuxiliaire_AvecANouveauAuxiliarise_NeDoublePasLOuverture()
    {
        var factory = NewFactory();
        Seed(factory,
            Sale(1, new DateTime(2025, 6, 15), 1000m, ClientId),
            OpeningEntry(new DateTime(2026, 1, 1), 1000m, ClientId),
            Sale(2, new DateTime(2026, 4, 10), 300m, ClientId));

        var result = await BuildReporting(factory).GetAuxiliaryBalanceAsync(
            ThirdPartyKind.Client, new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));

        Assert.True(result.IsSuccess);
        var row = Assert.Single(result.Value);
        Assert.Equal(1000m, row.OpeningDebit);
        Assert.Equal(300m, row.MovementDebit);
        Assert.Equal(1300m, row.ClosingDebit - row.ClosingCredit);
    }

    [Fact]
    public async Task GrandLivreTiers_AvecANouveauAuxiliarise_SoldeOuvertureNonDouble()
    {
        var factory = NewFactory();
        Seed(factory,
            Sale(1, new DateTime(2025, 6, 15), 1000m, ClientId),
            OpeningEntry(new DateTime(2026, 1, 1), 1000m, ClientId),
            Sale(2, new DateTime(2026, 4, 10), 300m, ClientId));

        var result = await BuildReporting(factory).GetThirdPartyLedgerAsync(
            ClientId, ThirdPartyKind.Client, new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));

        Assert.True(result.IsSuccess);
        Assert.Equal(1000m, result.Value.OpeningBalance);
        var movement = Assert.Single(result.Value.Rows);   // l'à-nouveau n'est pas un mouvement
        Assert.Equal(300m, movement.Debit);
        Assert.Equal(1300m, movement.RunningBalance);
    }

    [Fact]
    public async Task GrandLivreTiers_SansANouveau_ComportementHistoriqueInchange()
    {
        var factory = NewFactory();
        Seed(factory,
            Sale(1, new DateTime(2025, 6, 15), 1000m, ClientId),
            Sale(2, new DateTime(2026, 4, 10), 300m, ClientId));

        var result = await BuildReporting(factory).GetThirdPartyLedgerAsync(
            ClientId, ThirdPartyKind.Client, new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));

        Assert.True(result.IsSuccess);
        Assert.Equal(1000m, result.Value.OpeningBalance);
        var movement = Assert.Single(result.Value.Rows);
        Assert.Equal(1300m, movement.RunningBalance);
    }

    // ── Génération : l'à-nouveau porte le tiers ────────────────────────────────────────────

    [Fact]
    public async Task GenerateOpeningEntries_ReporteLeTiersSurLesLignesDeCompteAuxiliarise()
    {
        var factory = NewFactory();
        Seed(factory, Sale(1, new DateTime(2026, 6, 15), 1000m, ClientId));

        var (service, captured) = BuildAccountingService(factory);
        var result = await service.GenerateOpeningEntriesAsync(2026, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(captured);
        var clientLine = entry.Lines.Single(l => l.AccountNumber == "4111");
        Assert.Equal(ClientId, clientLine.ThirdPartyId);
        Assert.Equal(ThirdPartyKind.Client, clientLine.ThirdPartyKind);
        Assert.Equal(1000m, clientLine.DebitAmount.Amount);

        // L'écriture reste équilibrée et les comptes non auxiliarisés restent sans tiers.
        Assert.Equal(
            entry.Lines.Sum(l => l.DebitAmount.Amount),
            entry.Lines.Sum(l => l.CreditAmount.Amount));
        var resultLine = entry.Lines.Single(l => l.AccountNumber == AccountingService.OpeningBalanceProfitAccountNumber);
        Assert.Null(resultLine.ThirdPartyId);
    }

    [Fact]
    public async Task GenerateOpeningEntries_VentileUneLigneParTiers()
    {
        var otherClient = Guid.Parse("99999999-8888-7777-6666-555555555555");
        var factory = NewFactory();
        Seed(factory,
            Sale(1, new DateTime(2026, 6, 15), 1000m, ClientId),
            Sale(2, new DateTime(2026, 7, 20), 400m, otherClient));

        var (service, captured) = BuildAccountingService(factory);
        var result = await service.GenerateOpeningEntriesAsync(2026, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(captured);
        var clientLines = entry.Lines.Where(l => l.AccountNumber == "4111").ToList();
        Assert.Equal(2, clientLines.Count);
        Assert.Equal(1000m, clientLines.Single(l => l.ThirdPartyId == ClientId).DebitAmount.Amount);
        Assert.Equal(400m, clientLines.Single(l => l.ThirdPartyId == otherClient).DebitAmount.Amount);
    }

    private static (AccountingService Service, List<JournalEntry> Captured) BuildAccountingService(
        ITenantDbContextFactory factory)
    {
        var chart = new Mock<IChartOfAccountRepository>();
        chart.Setup(x => x.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(180);
        chart.Setup(x => x.GetByAccountNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string acc, CancellationToken _) =>
                ChartOfAccount.Create(acc, $"Compte {acc}", int.Parse(acc[..1]), null, AccountNatureType.Debit).Value);

        var journals = new Mock<IJournalEntryRepository>();
        journals.Setup(x => x.GetBySourceAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JournalEntry?)null);
        journals.Setup(x => x.CountDraftsByFiscalYearAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        journals.Setup(x => x.ReserveNextEntryNumberAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        var captured = new List<JournalEntry>();
        journals.Setup(x => x.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()))
            .Callback((JournalEntry e, CancellationToken _) => captured.Add(e))
            .ReturnsAsync((JournalEntry e, CancellationToken _) => e);

        var periodService = new Mock<IAccountingPeriodService>();
        var period = AccountingPeriod.Create(2027, 1, new DateTime(2027, 1, 1), new DateTime(2027, 1, 31));
        periodService.Setup(x => x.EnsureOpenPeriodAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(period));

        var service = new AccountingService(
            chart.Object, periodService.Object, journals.Object, new Mock<IWithholdingTaxRepository>().Object,
            new Mock<IDepreciationRateCategoryRepository>().Object,
            factory, NullLogger<AccountingService>.Instance, Options.Create(new AccountingSettings()));

        return (service, captured);
    }
}
