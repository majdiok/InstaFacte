using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Commands;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Repositories;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// C4 : la clôture de période est stricte — refus tant que des brouillons subsistent,
/// bascule Validee → Cloturee à la clôture, symétrique à la réouverture, et les brouillons
/// résiduels d'une période close sont figés (ni modifiables ni supprimables).
/// </summary>
public sealed class AccountingPeriodClosingTests
{
    private readonly string _dbName = $"PeriodCloseDb_{Guid.NewGuid()}";
    private readonly TestTenantDbContextFactory _factory;

    public AccountingPeriodClosingTests()
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

    private AccountingPeriodService BuildService()
        => new(new Mock<IAccountingPeriodRepository>().Object, _factory, Options.Create(new AccountingSettings()));

    private AccountingPeriod SeedPeriod(bool closed = false)
    {
        var period = AccountingPeriod.Create(2026, 5, new DateTime(2026, 5, 1), new DateTime(2026, 5, 31));
        if (closed) period.Close("test");
        period.SetAuditInfo("test", false);
        using var ctx = _factory.CreateContext();
        ctx.AccountingPeriods.Add(period);
        ctx.SaveChanges();
        return period;
    }

    private JournalEntry SeedEntry(Guid periodId, JournalEntryStatus status, int number = 1)
    {
        var lines = new[]
        {
            new JournalLineInput("4111", "Client", 100m, 0m, null, ThirdPartyKind.None),
            new JournalLineInput("707", "Vente", 0m, 100m, null, ThirdPartyKind.None)
        };
        var entry = JournalEntry.Create(
            number, "JV", new DateTime(2026, 5, 15), $"Vente {number}", periodId,
            false, "Manual", null, lines, initialStatus: status).Value;
        entry.SetAuditInfo("test", false);
        using var ctx = _factory.CreateContext();
        ctx.JournalEntries.Add(entry);
        ctx.SaveChanges();
        return entry;
    }

    private JournalEntryStatus EntryStatus(Guid entryId)
    {
        using var ctx = _factory.CreateContext();
        return ctx.JournalEntries.AsNoTracking().Single(e => e.Id == entryId).Status;
    }

    private bool PeriodIsClosed(Guid periodId)
    {
        using var ctx = _factory.CreateContext();
        return ctx.AccountingPeriods.AsNoTracking().Single(p => p.Id == periodId).IsClosed;
    }

    [Fact]
    public async Task Close_WithDraftsInPeriod_Fails()
    {
        var period = SeedPeriod();
        SeedEntry(period.Id, JournalEntryStatus.Brouillon);

        var result = await BuildService().ClosePeriodWithLockAsync(period.Id, "test", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("brouillard", result.Error.Description);
        Assert.False(PeriodIsClosed(period.Id));
    }

    [Fact]
    public async Task Close_FlipsValidatedEntriesToCloturee()
    {
        var period = SeedPeriod();
        var entry = SeedEntry(period.Id, JournalEntryStatus.Validee);

        var result = await BuildService().ClosePeriodWithLockAsync(period.Id, "test", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(PeriodIsClosed(period.Id));
        Assert.Equal(JournalEntryStatus.Cloturee, EntryStatus(entry.Id));
    }

    [Fact]
    public async Task Close_AlreadyClosed_Fails()
    {
        var period = SeedPeriod(closed: true);

        var result = await BuildService().ClosePeriodWithLockAsync(period.Id, "test", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("déjà clôturée", result.Error.Description);
    }

    [Fact]
    public async Task Reopen_FlipsClotureeEntriesBackToValidee()
    {
        var period = SeedPeriod();
        var entry = SeedEntry(period.Id, JournalEntryStatus.Validee);
        var service = BuildService();
        Assert.True((await service.ClosePeriodWithLockAsync(period.Id, "test", CancellationToken.None)).IsSuccess);

        var result = await service.ReopenPeriodAsync(period.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(PeriodIsClosed(period.Id));
        Assert.Equal(JournalEntryStatus.Validee, EntryStatus(entry.Id));
    }

    [Fact]
    public async Task Reopen_OpenPeriod_IsIdempotent()
    {
        var period = SeedPeriod();

        var result = await BuildService().ReopenPeriodAsync(period.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(PeriodIsClosed(period.Id));
    }

    [Fact]
    public async Task Reopen_UnknownPeriod_ReturnsNotFound()
    {
        var result = await BuildService().ReopenPeriodAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    // ── Brouillons figés en période close (défense en profondeur sur les handlers) ─────────

    [Fact]
    public async Task DeleteDraft_InClosedPeriod_Fails()
    {
        var period = SeedPeriod(closed: true);
        var entry = SeedEntry(period.Id, JournalEntryStatus.Brouillon);
        var repo = new JournalEntryRepository(_factory);
        var handler = new DeleteDraftJournalEntryCommandHandler(repo, new Mock<IAuditService>().Object);

        var result = await handler.Handle(new DeleteDraftJournalEntryCommand(entry.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("clôturée", result.Error.Description);
        Assert.Equal(JournalEntryStatus.Brouillon, EntryStatus(entry.Id)); // toujours présent
    }

    [Fact]
    public async Task UpdateDraft_InClosedPeriod_Fails()
    {
        var period = SeedPeriod(closed: true);
        var entry = SeedEntry(period.Id, JournalEntryStatus.Brouillon);
        var repo = new JournalEntryRepository(_factory);
        var chart = new Mock<IChartOfAccountRepository>();
        chart.Setup(x => x.GetByAccountNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string acc, CancellationToken _) =>
                ChartOfAccount.Create(acc, $"Compte {acc}", int.Parse(acc[..1]), null, AccountNatureType.Debit).Value);
        var handler = new UpdateDraftJournalEntryCommandHandler(repo, chart.Object, new Mock<IAuditService>().Object);

        var request = new UpdateDraftJournalEntryRequest
        {
            Label = "Modifié",
            Lines = new[]
            {
                new ManualJournalLineRequest { AccountNumber = "4111", LineLabel = "Client", Debit = 200m, Credit = 0m },
                new ManualJournalLineRequest { AccountNumber = "707", LineLabel = "Vente", Debit = 0m, Credit = 200m }
            }
        };

        var result = await handler.Handle(new UpdateDraftJournalEntryCommand(entry.Id, request), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("clôturée", result.Error.Description);
    }
}
