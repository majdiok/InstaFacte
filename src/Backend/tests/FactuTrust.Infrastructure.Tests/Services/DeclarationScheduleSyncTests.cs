using FactuTrust.Application.Common;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Accounting.FiscalSchedule;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Synchronisation déclaration mensuelle → échéancier fiscal : création auto si l'échéance est
/// absente, liaison SourceId, alignement du montant (total à payer), dépôt à la soumission,
/// rectificative conservant le dépôt, garde du feature flag.
/// </summary>
public sealed class DeclarationScheduleSyncTests
{
    private readonly TestTenantDbContextFactory _factory;
    private readonly FiscalScheduleRepository _repository;

    public DeclarationScheduleSyncTests()
    {
        _factory = new TestTenantDbContextFactory($"DeclScheduleSyncDb_{Guid.NewGuid()}");
        _repository = new FiscalScheduleRepository(_factory, TimeProvider.System);
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

    private DeclarationScheduleSynchronizer BuildSync(bool enabled = true) =>
        new(_repository,
            new DefaultVatDeadlineService(),
            Options.Create(new AccountingSettings { DeclarationScheduleSyncEnabled = enabled }),
            NullLogger<DeclarationScheduleSynchronizer>.Instance);

    private sealed class DefaultVatDeadlineService : ITunisianFiscalDeadlineService
    {
        public DateTime ComputeVatFilingDeadline(int periodYear, int periodMonth, TaxRegime? taxRegime = null) =>
            VatFilingDeadline.ForPeriod(periodYear, periodMonth);

        public DateTime AdjustForWeekendsAndHolidays(DateTime dueDate) => dueDate;
    }

    private static VatDeclaration Declaration(int year = 2026, int month = 7, bool submit = false)
    {
        var zero = Money.Create(0m, "TND");
        var declaration = VatDeclaration.CreateDraft(
            year, month,
            Money.Create(1900m, "TND"), zero, zero, // TVA collectée 19 %
            Money.Create(500m, "TND"), zero, zero,  // déductible biens
            "TND");
        if (submit)
            declaration.Submit();
        declaration.SetAuditInfo("test", false);
        return declaration;
    }

    private Guid SeedGeneratedEntry(int year = 2026, int month = 7, decimal amount = 500_000m,
        bool cancelled = false)
    {
        var entry = FiscalScheduleEntry.Create(
            FiscalObligationType.MonthlyDeclaration, "Declaration mensuelle", year,
            new DateTime(year, month + 1 > 12 ? 1 : month + 1, 22),
            amount,
            periodMonth: month,
            sourceType: FiscalScheduleSourceType.VatDeclaration).Value;
        if (cancelled)
            entry.Cancel();
        entry.SetAuditInfo("test", false);
        using var ctx = _factory.CreateContext();
        ctx.FiscalScheduleEntries.Add(entry);
        ctx.SaveChanges();
        return entry.Id;
    }

    private FiscalScheduleEntry SingleEntry()
    {
        using var ctx = _factory.CreateContext();
        return ctx.FiscalScheduleEntries.AsNoTracking().Single();
    }

    [Fact]
    public async Task MissingEntry_DraftSave_CreatesLinkedEntry_NotDeposited()
    {
        var declaration = Declaration();

        await BuildSync().SyncAsync(declaration, 30.692m);

        var entry = SingleEntry();
        Assert.Equal(FiscalObligationType.MonthlyDeclaration, entry.ObligationType);
        Assert.Equal(2026, entry.FiscalYear);
        Assert.Equal(7, entry.PeriodMonth);
        Assert.Equal(new DateTime(2026, 8, 22), entry.DueDate); // 22 du mois suivant
        Assert.Equal(30.692m, entry.EstimatedAmount);
        Assert.Equal(declaration.Id, entry.SourceId);
        Assert.Equal(FiscalScheduleSourceType.VatDeclaration, entry.SourceType);
        Assert.Null(entry.DepositDate);
        using var ctx = _factory.CreateContext();
        Assert.Single(ctx.FiscalScheduleHistoryEntries.Where(h => h.Action == "CreatedFromDeclaration"));
    }

    [Fact]
    public async Task MissingEntry_Submission_CreatesDepositedEntry()
    {
        var declaration = Declaration(submit: true);

        await BuildSync().SyncAsync(declaration, 100m);

        var entry = SingleEntry();
        Assert.Equal(declaration.SubmittedAt!.Value.Date, entry.DepositDate);
        Assert.Equal(FiscalScheduleStatus.Deposited, entry.ResolveStatus(DateTime.UtcNow.Date));
    }

    [Fact]
    public async Task ExistingEntry_Save_LinksSource_ReplacesAmount_WritesHistory()
    {
        var entryId = SeedGeneratedEntry(amount: 500_000m); // montant obsolète, SourceId null
        var declaration = Declaration();

        await BuildSync().SyncAsync(declaration, 30.692m);

        var entry = SingleEntry();
        Assert.Equal(entryId, entry.Id); // pas de doublon créé
        Assert.Equal(declaration.Id, entry.SourceId);
        Assert.Equal(30.692m, entry.EstimatedAmount);
        Assert.Null(entry.DepositDate);
        using var ctx = _factory.CreateContext();
        Assert.Single(ctx.FiscalScheduleHistoryEntries.Where(
            h => h.FiscalScheduleEntryId == entryId && h.Action == "DeclarationSynced"));
    }

    [Fact]
    public async Task ExistingEntry_Submission_MarksDeposited()
    {
        SeedGeneratedEntry();
        var declaration = Declaration(submit: true);

        await BuildSync().SyncAsync(declaration, 250m);

        var entry = SingleEntry();
        Assert.Equal(declaration.SubmittedAt!.Value.Date, entry.DepositDate);
        Assert.Equal(250m, entry.EstimatedAmount);
    }

    [Fact]
    public async Task Rectificative_KeepsDeposit_UpdatesAmount()
    {
        SeedGeneratedEntry();
        var sync = BuildSync();

        // V1 soumise → déposée.
        var declaration = Declaration(submit: true);
        await sync.SyncAsync(declaration, 250m);
        var depositAfterSubmit = SingleEntry().DepositDate;
        Assert.NotNull(depositAfterSubmit);

        // Rectificative : retour en brouillon (montant révisé), le dépôt doit être CONSERVÉ.
        var zero = Money.Create(0m, "TND");
        declaration.ApplyRevision(
            Money.Create(2500m, "TND"), zero, zero,
            Money.Create(500m, "TND"), zero, zero,
            0m, 0m, 0m, 0m, 0m, 0m, 0m);
        Assert.Equal(VatDeclarationStatus.Draft, declaration.Status);
        await sync.SyncAsync(declaration, 380m);

        var entry = SingleEntry();
        Assert.Equal(depositAfterSubmit, entry.DepositDate); // dépôt conservé
        Assert.Equal(380m, entry.EstimatedAmount);           // montant actualisé

        // Re-soumission → la date de dépôt est reposée (MarkDeposited accepte la re-pose).
        declaration.Submit();
        await sync.SyncAsync(declaration, 380m);
        Assert.Equal(declaration.SubmittedAt!.Value.Date, SingleEntry().DepositDate);
    }

    [Fact]
    public async Task CancelledEntry_IsSkipped_NoDuplicateCreated()
    {
        SeedGeneratedEntry(cancelled: true);
        var declaration = Declaration();

        // L'échéance annulée est ignorée par le lookup → une NOUVELLE échéance active est créée.
        await BuildSync().SyncAsync(declaration, 100m);

        using var ctx = _factory.CreateContext();
        var entries = ctx.FiscalScheduleEntries.AsNoTracking().ToList();
        Assert.Equal(2, entries.Count);
        var active = entries.Single(e => !e.IsCancelled);
        Assert.Equal(declaration.Id, active.SourceId);
        Assert.Equal(100m, active.EstimatedAmount);
    }

    [Fact]
    public async Task FlagOff_DoesNothing()
    {
        SeedGeneratedEntry(amount: 500_000m);
        var declaration = Declaration(submit: true);

        await BuildSync(enabled: false).SyncAsync(declaration, 30.692m);

        var entry = SingleEntry();
        Assert.Null(entry.SourceId);
        Assert.Equal(500_000m, entry.EstimatedAmount);
        Assert.Null(entry.DepositDate);
    }

    [Fact]
    public async Task NegativeTotal_IsFlooredToZero()
    {
        SeedGeneratedEntry();
        var declaration = Declaration();

        await BuildSync().SyncAsync(declaration, -42m);

        Assert.Equal(0m, SingleEntry().EstimatedAmount);
    }
}
