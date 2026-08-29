using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Repositories;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// T8 : correction du double comptage des à-nouveaux (Bug D-bis) et diagnostic/réparation des
/// à-nouveaux contaminés. Utilise le VRAI <see cref="JournalEntryRepository"/> contre le fournisseur
/// InMemory (et non un mock) : preuve empirique que <c>ReserveNextEntryNumberAsync</c> (transaction
/// isolée + stratégie d'exécution) fonctionne dans ce parcours de régénération après extourne.
/// </summary>
public sealed class OpeningEntriesRepairTests
{
    private sealed class TestTenantDbContextFactory : ITenantDbContextFactory
    {
        private readonly string _databaseName;
        public TestTenantDbContextFactory(string databaseName) => _databaseName = databaseName;
        public TenantDbContext CreateContext()
        {
            // Ignore l'avertissement InMemory sur les transactions (ignorées mais pas rejetées) :
            // ReserveNextEntryNumberAsync ouvre une transaction même contre ce fournisseur de test.
            var options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseInMemoryDatabase(databaseName: _databaseName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new TenantDbContext(options);
        }
    }

    private static TestTenantDbContextFactory NewFactory() => new($"RepairOpeningDb_{Guid.NewGuid()}");

    /// <summary>Reproduction exacte de <c>AccountingService.GuidFromFiscalYear</c> (privée) pour semer
    /// une écriture contaminée avec le même identifiant de source déterministe que le générateur.</summary>
    private static Guid GuidFromFiscalYear(int fiscalYear)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(fiscalYear).CopyTo(bytes, 0);
        bytes[4] = 0xA0;
        bytes[5] = 0x0B;
        return new Guid(bytes);
    }

    /// <summary>Récupère (ou crée) la période comptable persistée pour la date donnée — indispensable
    /// car <c>JournalEntry.AccountingPeriodId</c> est une FK non nullable et
    /// <c>GetActiveBySourceAsync</c>/<c>GetBySourceAsync</c> incluent la navigation
    /// <c>AccountingPeriod</c> : le fournisseur InMemory omet silencieusement toute écriture dont la
    /// période référencée n'existe pas en base.</summary>
    private static Guid EnsurePeriodId(TestTenantDbContextFactory factory, DateTime date)
    {
        using var ctx = factory.CreateContext();
        var existing = ctx.AccountingPeriods.FirstOrDefault(p => p.FiscalYear == date.Year && p.Month == date.Month);
        if (existing is not null) return existing.Id;

        var period = AccountingPeriod.Create(date.Year, date.Month, new DateTime(date.Year, date.Month, 1), new DateTime(date.Year, date.Month, 1).AddMonths(1).AddDays(-1));
        ctx.AccountingPeriods.Add(period);
        ctx.SaveChanges();
        return period.Id;
    }

    private static JournalEntry Sale(TestTenantDbContextFactory factory, int number, DateTime date, decimal amount)
    {
        var lines = new[]
        {
            new JournalLineInput("4111", "Client", amount, 0m, null, ThirdPartyKind.None),
            new JournalLineInput("707", "Vente", 0m, amount, null, ThirdPartyKind.None)
        };
        var entry = JournalEntry.Create(
            number, "JV", date, $"Vente {number}", EnsurePeriodId(factory, date),
            false, "Manual", null, lines).Value;
        entry.SetAuditInfo("test", false);
        return entry;
    }

    private static void Seed(TestTenantDbContextFactory factory, params JournalEntry[] entries)
    {
        using var ctx = factory.CreateContext();
        ctx.JournalEntries.AddRange(entries);
        ctx.SaveChanges();
    }

    private static AccountingService BuildService(TestTenantDbContextFactory factory, bool manualReversalEnabled = false)
    {
        var chart = new Mock<IChartOfAccountRepository>();
        chart.Setup(x => x.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(180);
        chart.Setup(x => x.GetByAccountNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string acc, CancellationToken _) =>
                ChartOfAccount.Create(acc, $"Compte {acc}", int.Parse(acc[..1]), null, AccountNatureType.Debit).Value);

        var periodService = new Mock<IAccountingPeriodService>();
        periodService
            .Setup(x => x.EnsureOpenPeriodAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns((DateTime d, CancellationToken _) =>
            {
                // Persiste réellement la période (et non un objet volatile jamais ajouté au contexte) :
                // JournalEntry.AccountingPeriodId est une FK NON NULLABLE — l'Include(AccountingPeriod)
                // de GetActiveBySourceAsync/GetBySourceAsync filtre silencieusement (fournisseur InMemory)
                // toute écriture dont la période référencée n'existe pas en base.
                using var ctx = factory.CreateContext();
                var existing = ctx.AccountingPeriods
                    .FirstOrDefault(p => p.FiscalYear == d.Year && p.Month == d.Month);
                if (existing is not null)
                    return Task.FromResult(Result.Success(existing));

                var period = AccountingPeriod.Create(d.Year, d.Month, new DateTime(d.Year, d.Month, 1), new DateTime(d.Year, d.Month, 1).AddMonths(1).AddDays(-1));
                ctx.AccountingPeriods.Add(period);
                ctx.SaveChanges();
                return Task.FromResult(Result.Success(period));
            });

        var withholding = new Mock<IWithholdingTaxRepository>();
        var categories = new Mock<IDepreciationRateCategoryRepository>();
        var settings = Options.Create(new AccountingSettings { ManualReversalEnabled = manualReversalEnabled });

        // Vrai repository (pas un mock) : preuve que ReserveNextEntryNumberAsync fonctionne contre
        // l'InMemory provider dans le parcours réparation → régénération (T8).
        var journals = new JournalEntryRepository(factory);

        return new AccountingService(
            chart.Object, periodService.Object, journals, withholding.Object, categories.Object,
            factory, NullLogger<AccountingService>.Instance, settings);
    }

    private static decimal ClientDebit(JournalEntry entry) =>
        entry.Lines.Where(l => l.AccountNumber == "4111").Sum(l => l.DebitAmount.Amount);

    // ── 1. Clôtures séquentielles : pas de double comptage (Bug D-bis) ────────────────────

    [Fact]
    public async Task GenerateOpeningEntries_TwoSequentialClosings_NoDoubleCounting()
    {
        var factory = NewFactory();
        Seed(factory, Sale(factory, 1, new DateTime(2024, 6, 15), 100m));
        var service = BuildService(factory);

        // Clôture 2024 → AN 2025 (daté 01/01/2025).
        var first = await service.GenerateOpeningEntriesAsync(2024, CancellationToken.None);
        Assert.True(first.IsSuccess, first.IsFailure ? first.Error.Description : null);

        using (var ctx = factory.CreateContext())
        {
            var an2025 = await ctx.JournalEntries.Include(e => e.Lines)
                .SingleAsync(e => e.SourceEntityType == AccountingService.SourceOpeningBalance);
            Assert.Equal(100m, ClientDebit(an2025));
        }

        // Mouvement propre à 2025.
        Seed(factory, Sale(factory, 2, new DateTime(2025, 6, 1), 50m));

        // Clôture 2025 → AN 2026 (daté 01/01/2026), ancrée sur AN 2025 : 4111 doit valoir
        // exactement 150 (100 reporté + 50 de l'exercice), jamais 250 (double comptage historique
        // de l'à-nouveau lui-même) ni 300.
        var second = await service.GenerateOpeningEntriesAsync(2025, CancellationToken.None);
        Assert.True(second.IsSuccess, second.IsFailure ? second.Error.Description : null);

        using (var ctx = factory.CreateContext())
        {
            var an2026 = await ctx.JournalEntries.Include(e => e.Lines)
                .SingleAsync(e => e.Id == second.Value);
            Assert.Equal(150m, ClientDebit(an2026));
        }
    }

    // ── 2. Diagnostic d'un à-nouveau contaminé ─────────────────────────────────────────────

    [Fact]
    public async Task DiagnoseOpeningEntries_ContaminatedEntry_ListsDiscrepancy()
    {
        var factory = NewFactory();
        Seed(factory, Sale(factory, 1, new DateTime(2024, 6, 15), 100m));
        var service = BuildService(factory);

        var first = await service.GenerateOpeningEntriesAsync(2024, CancellationToken.None);
        Assert.True(first.IsSuccess);

        Seed(factory, Sale(factory, 2, new DateTime(2025, 6, 1), 50m));

        // AN 2026 CONTAMINÉ, semé directement (doublé : 250 au lieu de 150), avec le même
        // identifiant de source déterministe que le générateur (indispensable pour la réparation,
        // test 3) : GuidFromFiscalYear(2025).
        var contaminated = JournalEntry.Create(
            99, "JAN", new DateTime(2026, 1, 1), "Écritures d'à-nouveau — Exercice 2025 (contaminé)",
            EnsurePeriodId(factory, new DateTime(2026, 1, 1)), true, AccountingService.SourceOpeningBalance, GuidFromFiscalYear(2025),
            new[]
            {
                new JournalLineInput("4111", "À-nouveau — 4111", 250m, 0m, null, ThirdPartyKind.None),
                new JournalLineInput(AccountingService.OpeningBalanceProfitAccountNumber, "Résultat reporté", 0m, 250m, null, ThirdPartyKind.None)
            }).Value;
        contaminated.SetAuditInfo("test", false);
        Seed(factory, contaminated);

        var diagnostics = await service.DiagnoseOpeningEntriesAsync(CancellationToken.None);

        Assert.True(diagnostics.IsSuccess);
        var diag = Assert.Single(diagnostics.Value, d => d.OpeningEntryId == contaminated.Id);
        Assert.True(diag.HasDiscrepancy);
        var clientDiscrepancy = Assert.Single(diag.Discrepancies, d => d.AccountNumber == "4111");
        Assert.Equal(150m, clientDiscrepancy.ExpectedAmount);
        Assert.Equal(250m, clientDiscrepancy.ActualAmount);
        Assert.Equal(-100m, clientDiscrepancy.Difference);
    }

    // ── 3. Réparation : extourne interne + régénération, idempotente ──────────────────────

    [Fact]
    public async Task RepairOpeningEntries_ContaminatedEntry_FixesBalancesAndMarksOriginalReversed()
    {
        var factory = NewFactory();
        Seed(factory, Sale(factory, 1, new DateTime(2024, 6, 15), 100m));
        // ManualReversalEnabled=false : prouve que la réparation ne dépend pas du réglage
        // d'extourne manuelle (RepairOpeningEntriesAsync ne passe jamais par ReverseJournalEntryAsync).
        var service = BuildService(factory, manualReversalEnabled: false);

        var first = await service.GenerateOpeningEntriesAsync(2024, CancellationToken.None);
        Assert.True(first.IsSuccess);

        Seed(factory, Sale(factory, 2, new DateTime(2025, 6, 1), 50m));

        var contaminated = JournalEntry.Create(
            99, "JAN", new DateTime(2026, 1, 1), "Écritures d'à-nouveau — Exercice 2025 (contaminé)",
            EnsurePeriodId(factory, new DateTime(2026, 1, 1)), true, AccountingService.SourceOpeningBalance, GuidFromFiscalYear(2025),
            new[]
            {
                new JournalLineInput("4111", "À-nouveau — 4111", 250m, 0m, null, ThirdPartyKind.None),
                new JournalLineInput(AccountingService.OpeningBalanceProfitAccountNumber, "Résultat reporté", 0m, 250m, null, ThirdPartyKind.None)
            }, initialStatus: JournalEntryStatus.Validee).Value;
        contaminated.SetAuditInfo("test", false);
        Seed(factory, contaminated);

        var repair = await service.RepairOpeningEntriesAsync(2025, CancellationToken.None);
        Assert.True(repair.IsSuccess, repair.IsFailure ? repair.Error.Description : null);

        using (var ctx = factory.CreateContext())
        {
            var original = await ctx.JournalEntries.AsNoTracking().SingleAsync(e => e.Id == contaminated.Id);
            Assert.True(original.IsReversed);

            var reversal = await ctx.JournalEntries.AsNoTracking()
                .SingleAsync(e => e.SourceEntityType == AccountingService.SourceOpeningBalanceReversal);
            Assert.Equal(JournalEntryStatus.Validee, reversal.Status);
        }

        var journals = new JournalEntryRepository(factory);
        var active = await journals.GetActiveBySourceAsync(
            AccountingService.SourceOpeningBalance, GuidFromFiscalYear(2025), CancellationToken.None);
        Assert.NotNull(active);
        Assert.NotEqual(contaminated.Id, active!.Id);
        Assert.Equal(150m, ClientDebit(active));

        // Idempotence : une seconde réparation ne trouve plus d'écart → no-op, aucune nouvelle écriture.
        int countBefore;
        using (var ctx = factory.CreateContext())
            countBefore = await ctx.JournalEntries.CountAsync();

        var secondRepair = await service.RepairOpeningEntriesAsync(2025, CancellationToken.None);
        Assert.True(secondRepair.IsSuccess);

        using (var ctx = factory.CreateContext())
        {
            var countAfter = await ctx.JournalEntries.CountAsync();
            Assert.Equal(countBefore, countAfter);
        }
    }

    [Fact]
    public async Task RepairOpeningEntries_NoActiveEntry_IsNoOpSuccess()
    {
        var factory = NewFactory();
        var service = BuildService(factory);

        var result = await service.RepairOpeningEntriesAsync(2025, CancellationToken.None);

        Assert.True(result.IsSuccess);
        using var ctx = factory.CreateContext();
        Assert.Empty(ctx.JournalEntries);
    }
}
