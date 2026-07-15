using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Common.Interfaces.Services;
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
/// C2 : les à-nouveaux sont une sortie légale — refus si des brouillons subsistent sur
/// l'exercice clôturé, et l'agrégation des soldes exclut les brouillons (y compris ceux
/// d'exercices antérieurs qui échapperaient à la garde).
/// </summary>
public sealed class OpeningEntriesDraftExclusionTests
{
    private readonly string _dbName = $"OpeningTestDb_{Guid.NewGuid()}";
    private readonly TestTenantDbContextFactory _factory;

    public OpeningEntriesDraftExclusionTests()
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

    private static JournalEntry MakeEntry(int number, DateTime date, decimal amount, JournalEntryStatus status)
    {
        var lines = new[]
        {
            new JournalLineInput("4111", "Client", amount, 0m, null, ThirdPartyKind.None),
            new JournalLineInput("707", "Vente", 0m, amount, null, ThirdPartyKind.None)
        };
        var entry = JournalEntry.Create(
            number, "JV", date, $"Vente {number}", Guid.NewGuid(),
            false, "Manual", null, lines, initialStatus: status).Value;
        entry.SetAuditInfo("test", false);
        return entry;
    }

    private void Seed(params JournalEntry[] entries)
    {
        using var ctx = _factory.CreateContext();
        ctx.JournalEntries.AddRange(entries);
        ctx.SaveChanges();
    }

    private (AccountingService Service, Mock<IJournalEntryRepository> Journals, List<JournalEntry> Captured) BuildService(int draftsOnYear)
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
            .ReturnsAsync(draftsOnYear);
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

        var withholding = new Mock<IWithholdingTaxRepository>();
        var settings = Options.Create(new AccountingSettings());

        var service = new AccountingService(
            chart.Object, periodService.Object, journals.Object, withholding.Object,
            _factory, NullLogger<AccountingService>.Instance, settings);

        return (service, journals, captured);
    }

    [Fact]
    public async Task GenerateOpeningEntries_WithDraftsOnClosedYear_Fails()
    {
        Seed(MakeEntry(1, new DateTime(2026, 6, 15), 100m, JournalEntryStatus.Validee));
        var (service, journals, _) = BuildService(draftsOnYear: 2);

        var result = await service.GenerateOpeningEntriesAsync(2026, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("brouillard", result.Error.Description);
        journals.Verify(x => x.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerateOpeningEntries_ExcludesDraftBalances()
    {
        // Brouillon daté d'un exercice ANTÉRIEUR : il échappe à la garde (qui ne compte que
        // l'exercice clôturé) mais doit être exclu par le filtre de l'agrégation.
        Seed(
            MakeEntry(1, new DateTime(2026, 6, 15), 100m, JournalEntryStatus.Validee),
            MakeEntry(2, new DateTime(2025, 5, 10), 50m, JournalEntryStatus.Brouillon));
        var (service, _, captured) = BuildService(draftsOnYear: 0);

        var result = await service.GenerateOpeningEntriesAsync(2026, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(captured);
        var clientLine = entry.Lines.Single(l => l.AccountNumber == "4111");
        Assert.Equal(100m, clientLine.DebitAmount.Amount); // 100 validé, pas 150
        var resultLine = entry.Lines.Single(l => l.AccountNumber == AccountingService.OpeningBalanceProfitAccountNumber);
        Assert.Equal(100m, resultLine.CreditAmount.Amount);
    }

    [Fact]
    public async Task GenerateOpeningEntries_AllValidated_NonRegression()
    {
        Seed(
            MakeEntry(1, new DateTime(2026, 6, 15), 100m, JournalEntryStatus.Validee),
            MakeEntry(2, new DateTime(2026, 7, 20), 50m, JournalEntryStatus.Validee));
        var (service, _, captured) = BuildService(draftsOnYear: 0);

        var result = await service.GenerateOpeningEntriesAsync(2026, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(captured);
        var clientLine = entry.Lines.Single(l => l.AccountNumber == "4111");
        Assert.Equal(150m, clientLine.DebitAmount.Amount);
        var resultLine = entry.Lines.Single(l => l.AccountNumber == AccountingService.OpeningBalanceProfitAccountNumber);
        Assert.Equal(150m, resultLine.CreditAmount.Amount);
    }
}
