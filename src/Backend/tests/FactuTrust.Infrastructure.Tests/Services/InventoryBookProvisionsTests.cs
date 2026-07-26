using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Fiscal;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Livre d'inventaire — provisions détaillées. Contrôle structurant : la ventilation par compte
/// (15/29/39/49/59) se recompose exactement en les totaux 5-groupes de la liasse consolidée
/// (même source : la balance de clôture). Le statut de verrouillage est reporté fidèlement.
/// </summary>
public sealed class InventoryBookProvisionsTests
{
    private static readonly string[] ProvisionPrefixes = { "15", "29", "39", "49", "59" };

    private readonly string _dbName = $"InventoryBookDb_{Guid.NewGuid()}";
    private readonly TestTenantDbContextFactory _factory;

    public InventoryBookProvisionsTests()
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

    private static JournalEntry Entry(int number, DateTime date, string debit, string credit, decimal amount)
    {
        var lines = new[]
        {
            new JournalLineInput(debit, "Débit", amount, 0m, null, ThirdPartyKind.None),
            new JournalLineInput(credit, "Crédit", 0m, amount, null, ThirdPartyKind.None)
        };
        var e = JournalEntry.Create(number, "JOD", date, $"Pièce {number}", Guid.NewGuid(),
            false, "Manual", null, lines).Value;
        e.SetAuditInfo("test", false);
        return e;
    }

    private void Seed()
    {
        using var ctx = _factory.CreateContext();
        // Dotations aux provisions/dépréciations → soldes créditeurs sur 1511, 2911, 4911.
        ctx.JournalEntries.AddRange(
            Entry(1, new DateTime(2026, 12, 31), "6815", "1511", 100m),   // provision risques
            Entry(2, new DateTime(2026, 12, 31), "6816", "2911", 60m),    // dépréciation immo
            Entry(3, new DateTime(2026, 12, 31), "6817", "4911", 40m),    // dépréciation clients
            Entry(4, new DateTime(2026, 6, 10), "6132", "532", 500m));    // écriture ordinaire
        ctx.ChartOfAccounts.AddRange(
            ChartOfAccount.Create("1511", "Provisions pour risques", 1, null, AccountNatureType.Credit).Value,
            ChartOfAccount.Create("2911", "Dépréciation des immobilisations", 2, null, AccountNatureType.Credit).Value,
            ChartOfAccount.Create("4911", "Dépréciation des comptes clients", 4, null, AccountNatureType.Credit).Value);
        ctx.SaveChanges();
    }

    private AccountingReportingService BuildReporting()
        => new(_factory, Options.Create(new AccountingSettings()));

    private GetInventoryBookQueryHandler BuildHandler(bool locked)
    {
        var reporting = BuildReporting();

        // IMediator : le handler ne consomme de la liasse que CompanyName + états ; un DTO minimal suffit
        // (l'articulation testée porte sur les provisions détaillées, calculées via la balance réelle).
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetConsolidatedLiasseQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new ConsolidatedLiasseDto { FiscalYear = 2026, CompanyName = "Test SA" }));

        var lockService = new Mock<IFiscalYearLockService>();
        var locks = locked
            ? new List<FiscalYearLockDto> { new() { FiscalYear = 2026, LockedAt = new DateTime(2027, 3, 1), LockedBy = "test" } }
            : new List<FiscalYearLockDto>();
        lockService.Setup(s => s.GetLocksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<FiscalYearLockDto>>(locks));

        return new GetInventoryBookQueryHandler(mediator.Object, reporting, lockService.Object);
    }

    [Fact]
    public async Task DetailedProvisions_ReconcileWithFiveGroupTotals()
    {
        var result = await BuildHandler(locked: false).Handle(new GetInventoryBookQuery(2026), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var detailed = result.Value.DetailedProvisions;

        // Une ligne par compte de provision, montant = solde créditeur net.
        Assert.Equal(100m, detailed.Single(r => r.Code == "1511").Amount);
        Assert.Equal(60m, detailed.Single(r => r.Code == "2911").Amount);
        Assert.Equal(40m, detailed.Single(r => r.Code == "4911").Amount);

        // Articulation : Σ du détail par racine == total 5-groupes calculé sur la même balance.
        var balance = (await BuildReporting().GetBalanceAsync(new DateTime(2026, 1, 1), new DateTime(2026, 12, 31))).Value;
        foreach (var prefix in ProvisionPrefixes)
        {
            var groupTotal = balance.Where(r => r.AccountNumber.StartsWith(prefix))
                .Sum(r => r.ClosingCredit - r.ClosingDebit);
            var detailTotal = detailed.Where(r => r.Code.StartsWith(prefix)).Sum(r => r.Amount);
            Assert.Equal(groupTotal, detailTotal);
        }
    }

    [Fact]
    public async Task ClosingBalance_MatchesGeneralBalance()
    {
        var result = await BuildHandler(locked: false).Handle(new GetInventoryBookQuery(2026), CancellationToken.None);
        var balance = (await BuildReporting().GetBalanceAsync(new DateTime(2026, 1, 1), new DateTime(2026, 12, 31))).Value;

        Assert.True(result.IsSuccess);
        Assert.Equal(balance.Count, result.Value.ClosingBalance.Count);
        Assert.Equal(
            balance.Sum(r => r.ClosingDebit),
            result.Value.ClosingBalance.Sum(r => r.ClosingDebit));
    }

    [Fact]
    public async Task LockStatus_IsReported()
    {
        var locked = await BuildHandler(locked: true).Handle(new GetInventoryBookQuery(2026), CancellationToken.None);
        var unlocked = await BuildHandler(locked: false).Handle(new GetInventoryBookQuery(2026), CancellationToken.None);

        Assert.True(locked.Value.IsYearLocked);
        Assert.NotNull(locked.Value.LockedAt);
        Assert.False(unlocked.Value.IsYearLocked);
        Assert.Null(unlocked.Value.LockedAt);
    }

    [Fact]
    public async Task InvalidYear_IsRejected()
    {
        var result = await BuildHandler(locked: false).Handle(new GetInventoryBookQuery(1500), CancellationToken.None);
        Assert.True(result.IsFailure);
    }
}
