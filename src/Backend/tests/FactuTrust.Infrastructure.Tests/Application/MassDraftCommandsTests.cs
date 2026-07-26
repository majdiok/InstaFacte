using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.Accounting.Commands;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

/// <summary>
/// Édition de masse de brouillons. Exigences de non-régression : n'affecte QUE les brouillons
/// (le validé/clôturé est ignoré et laissé intact), idempotence des compteurs, refus de déplacer
/// vers une période clôturée. Repository réel sur base in-memory.
/// </summary>
public sealed class MassDraftCommandsTests
{
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

    private readonly TestTenantDbContextFactory _factory = new($"MassDraftDb_{Guid.NewGuid()}");
    private Guid _periodId;

    private static IReadOnlyList<JournalLineInput> Lines() => new[]
    {
        new JournalLineInput("4111", "Client", 100m, 0, null, ThirdPartyKind.None),
        new JournalLineInput("707", "Vente", 0, 100m, null, ThirdPartyKind.None)
    };

    private Guid Seed(int number, JournalEntryStatus status, string journal = "JV")
    {
        using var ctx = _factory.CreateContext();
        if (_periodId == Guid.Empty)
        {
            var period = AccountingPeriod.Create(2026, 5, new DateTime(2026, 5, 1), new DateTime(2026, 5, 31));
            period.SetAuditInfo("test", false);
            ctx.AccountingPeriods.Add(period);
            ctx.SaveChanges();
            _periodId = period.Id;
        }
        var entry = JournalEntry.Create(number, journal, new DateTime(2026, 5, 10), "Origine", _periodId,
            false, "Manual", null, Lines(), initialStatus: status).Value;
        entry.SetAuditInfo("test", false);
        ctx.JournalEntries.Add(entry);
        ctx.SaveChanges();
        return entry.Id;
    }

    private MassUpdateDraftEntriesCommandHandler BuildUpdateHandler(bool periodClosedForNewDate = false)
    {
        var journals = new Mock<IJournalRepository>();
        journals.Setup(x => x.ExistsActiveJournalCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var periods = new Mock<IAccountingPeriodService>();
        periods.Setup(x => x.EnsureOpenPeriodAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => periodClosedForNewDate
                ? Result.Failure<AccountingPeriod>(Error.Validation("Period", "Période clôturée."))
                : Result.Success(AccountingPeriod.Create(2026, 7, new DateTime(2026, 7, 1), new DateTime(2026, 7, 31))));

        return new MassUpdateDraftEntriesCommandHandler(
            new JournalEntryRepository(_factory), journals.Object, periods.Object, new Mock<IAuditService>().Object);
    }

    private MassDeleteDraftEntriesCommandHandler BuildDeleteHandler()
        => new(new JournalEntryRepository(_factory), new Mock<IAuditService>().Object);

    private JournalEntry Reload(Guid id)
    {
        using var ctx = _factory.CreateContext();
        return ctx.JournalEntries.AsNoTracking().Include(e => e.Lines).Single(e => e.Id == id);
    }

    // ── Mass update ────────────────────────────────────────────────────────────

    [Fact]
    public async Task MassUpdate_ChangesJournalOnDrafts_SkipsValidated()
    {
        var draft = Seed(1, JournalEntryStatus.Brouillon);
        var validated = Seed(2, JournalEntryStatus.Validee);

        var result = await BuildUpdateHandler().Handle(
            new MassUpdateDraftEntriesCommand(new[] { draft, validated }, "JOD", null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Updated);
        Assert.Equal(1, result.Value.Skipped);
        Assert.Equal("JOD", Reload(draft).JournalCode);
        Assert.Equal("JV", Reload(validated).JournalCode);   // validé strictement inchangé
    }

    [Fact]
    public async Task MassUpdate_ChangesLabel_OnDraftOnly()
    {
        var draft = Seed(1, JournalEntryStatus.Brouillon);

        var result = await BuildUpdateHandler().Handle(
            new MassUpdateDraftEntriesCommand(new[] { draft }, null, null, "Corrigé en lot"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Updated);
        Assert.Equal("Corrigé en lot", Reload(draft).Label);
    }

    [Fact]
    public async Task MassUpdate_ChangesDate_ReassignsPeriod()
    {
        var draft = Seed(1, JournalEntryStatus.Brouillon);

        var result = await BuildUpdateHandler().Handle(
            new MassUpdateDraftEntriesCommand(new[] { draft }, null, new DateTime(2026, 7, 15), null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateTime(2026, 7, 15), Reload(draft).EntryDate);
    }

    [Fact]
    public async Task MassUpdate_NewDateInClosedPeriod_IsRefusedEntirely()
    {
        var draft = Seed(1, JournalEntryStatus.Brouillon);

        var result = await BuildUpdateHandler(periodClosedForNewDate: true).Handle(
            new MassUpdateDraftEntriesCommand(new[] { draft }, null, new DateTime(2026, 7, 15), null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(new DateTime(2026, 5, 10), Reload(draft).EntryDate);   // inchangé
    }

    [Fact]
    public async Task MassUpdate_UnknownJournal_IsRefused()
    {
        var draft = Seed(1, JournalEntryStatus.Brouillon);
        var journals = new Mock<IJournalRepository>();
        journals.Setup(x => x.ExistsActiveJournalCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var handler = new MassUpdateDraftEntriesCommandHandler(
            new JournalEntryRepository(_factory), journals.Object,
            new Mock<IAccountingPeriodService>().Object, new Mock<IAuditService>().Object);

        var result = await handler.Handle(
            new MassUpdateDraftEntriesCommand(new[] { draft }, "ZZZ", null, null), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task MassUpdate_NoChangesRequested_IsRefused()
    {
        var draft = Seed(1, JournalEntryStatus.Brouillon);
        var result = await BuildUpdateHandler().Handle(
            new MassUpdateDraftEntriesCommand(new[] { draft }, null, null, null), CancellationToken.None);
        Assert.True(result.IsFailure);
    }

    // ── Mass delete ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task MassDelete_RemovesDrafts_SkipsValidated()
    {
        var draft1 = Seed(1, JournalEntryStatus.Brouillon);
        var draft2 = Seed(2, JournalEntryStatus.Brouillon);
        var validated = Seed(3, JournalEntryStatus.Validee);

        var result = await BuildDeleteHandler().Handle(
            new MassDeleteDraftEntriesCommand(new[] { draft1, draft2, validated }), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Deleted);
        Assert.Equal(1, result.Value.Skipped);

        using var ctx = _factory.CreateContext();
        Assert.Equal(1, await ctx.JournalEntries.CountAsync());          // seul le validé subsiste
        Assert.True(await ctx.JournalEntries.AnyAsync(e => e.Id == validated));
    }

    [Fact]
    public async Task MassDelete_UnknownId_IsSkipped()
    {
        var result = await BuildDeleteHandler().Handle(
            new MassDeleteDraftEntriesCommand(new[] { Guid.NewGuid() }), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.Deleted);
        Assert.Equal(1, result.Value.Skipped);
    }

    [Fact]
    public async Task MassUpdate_EmptyIds_IsRefused()
    {
        var result = await BuildUpdateHandler().Handle(
            new MassUpdateDraftEntriesCommand(Array.Empty<Guid>(), "JOD", null, null), CancellationToken.None);
        Assert.True(result.IsFailure);
    }
}
