using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Application.DTOs;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Repositories;

/// <summary>
/// Repository tests using EF InMemory (fast, no SqlServerRetryingExecutionStrategy).
/// For SQL retry + manual transaction coverage, see <see cref="FixedAssetRepositorySqlRetryTests"/>.
/// </summary>
public sealed class FixedAssetRepositoryTests : IDisposable
{
    private readonly string _databaseName;
    private readonly TestTenantDbContextFactory _contextFactory;
    private readonly FixedAssetRepository _repository;

    public FixedAssetRepositoryTests()
    {
        _databaseName = $"TestDb_FixedAssets_{Guid.NewGuid()}";
        _contextFactory = new TestTenantDbContextFactory(_databaseName);
        _repository = new FixedAssetRepository(_contextFactory);
    }

    private sealed class TestTenantDbContextFactory : ITenantDbContextFactory
    {
        private readonly string _databaseName;

        public TestTenantDbContextFactory(string databaseName) => _databaseName = databaseName;

        public TenantDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseInMemoryDatabase(databaseName: _databaseName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            return new TenantDbContext(options);
        }
    }

    [Fact]
    public async Task UpdateAsync_AfterPutInService_ShouldPersistInServiceEvent()
    {
        var (_, asset) = await CreatePersistedDraftAssetAsync();

        var reloaded = await _repository.GetByIdAsync(asset.Id, includeSchedule: false, includeEvents: false);
        Assert.NotNull(reloaded);

        var put = reloaded!.PutInService(new DateTime(2026, 3, 1), "404");
        Assert.True(put.IsSuccess);
        reloaded.SetAuditInfo("test@factutrust.tn", isUpdate: true);

        await _repository.UpdateAsync(reloaded);

        await using var verify = _contextFactory.CreateContext();
        var persisted = await verify.FixedAssets.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == asset.Id);
        Assert.NotNull(persisted);
        Assert.Equal(FixedAssetStatus.InService, persisted!.Status);
        Assert.Equal(new DateTime(2026, 3, 1), persisted.InServiceDate);

        var events = await verify.FixedAssetEvents.AsNoTracking()
            .Where(e => e.FixedAssetId == asset.Id)
            .ToListAsync();
        Assert.Contains(events, e => e.EventType == FixedAssetEventType.Created);
        Assert.Contains(events, e => e.EventType == FixedAssetEventType.InService);
    }

    [Fact]
    public async Task UpdateAsync_DraftWithCategoryNavigation_ShouldNotThrow()
    {
        var (category, asset) = await CreatePersistedDraftAssetAsync();

        var reloaded = await _repository.GetByIdAsync(asset.Id);
        Assert.NotNull(reloaded);

        var update = reloaded!.UpdateDraft(
            "Camion modifie",
            null,
            55_000m,
            0m,
            0m,
            new DateTime(2026, 1, 15),
            category.LegalRatePercent,
            category.UsefulLifeYears,
            "218",
            "2818",
            "6818",
            null);
        Assert.True(update.IsSuccess);
        reloaded.SetAuditInfo("test@factutrust.tn", isUpdate: true);

        await _repository.UpdateAsync(reloaded);

        await using var verify = _contextFactory.CreateContext();
        var persisted = await verify.FixedAssets.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == asset.Id);
        Assert.NotNull(persisted);
        Assert.Equal("Camion modifie", persisted!.Label);
        Assert.Equal(55_000m, persisted.AcquisitionCost);
    }

    [Fact]
    public async Task PutInServiceInTransactionAsync_ShouldPersistStatusAndScheduleAtomically()
    {
        var (_, asset) = await CreatePersistedDraftAssetAsync();

        var reloaded = await _repository.GetByIdAsync(asset.Id, includeSchedule: true);
        Assert.NotNull(reloaded);

        var engine = new FactuTrust.Infrastructure.Services.DepreciationEngine();
        var simulated = reloaded!;
        simulated.PutInService(new DateTime(2026, 3, 1), "404");
        var lines = engine.GenerateSchedule(simulated);

        var result = await _repository.PutInServiceInTransactionAsync(
            asset.Id,
            new DateTime(2026, 3, 1),
            "404",
            lines,
            "test@factutrust.tn");

        Assert.True(result.IsSuccess);
        Assert.Equal(FixedAssetStatus.InService, result.Value.Status);
        Assert.NotEmpty(result.Value.ScheduleLines);

        await using var verify = _contextFactory.CreateContext();
        var persistedLines = await verify.DepreciationScheduleLines.AsNoTracking()
            .Where(l => l.FixedAssetId == asset.Id)
            .CountAsync();
        Assert.True(persistedLines > 0);
    }

    [Fact]
    public async Task SearchCurrentYearAmortizationTableAsync_ShouldReturnCalculatedAndPostedDotations()
    {
        var (_, asset) = await CreatePersistedDraftAssetAsync();
        var loaded = await _repository.GetByIdAsync(asset.Id, includeSchedule: true);
        Assert.NotNull(loaded);

        loaded!.PutInService(new DateTime(2026, 3, 1), "404");
        var linePosted = DepreciationScheduleLine.Create(
            loaded.Id,
            fiscalYear: 2026,
            periodMonth: 3,
            openingNbv: 50_000m,
            normalAnnualAmount: 10_000m,
            priorAccumulatedDepreciation: 0m,
            depreciationAmount: 5_000m,
            accumulatedDepreciation: 5_000m,
            closingNbv: 45_000m).Value;
        linePosted.MarkPosted(Guid.NewGuid(), Guid.NewGuid());

        var linePending = DepreciationScheduleLine.Create(
            loaded.Id,
            fiscalYear: 2026,
            periodMonth: 12,
            openingNbv: 45_000m,
            normalAnnualAmount: 10_000m,
            priorAccumulatedDepreciation: 5_000m,
            depreciationAmount: 2_000m,
            accumulatedDepreciation: 7_000m,
            closingNbv: 43_000m).Value;

        await _repository.ReplaceScheduleLinesAsync(loaded.Id, [linePosted, linePending]);

        var (rows, total) = await _repository.SearchCurrentYearAmortizationTableAsync(
            page: 1,
            pageSize: 25,
            fiscalYear: 2026,
            status: null,
            categoryId: null,
            search: null);

        Assert.Equal(1, total);
        var row = Assert.Single(rows);
        Assert.Equal(7_000m, row.DotationCalculeeExercice);
        Assert.Equal(5_000m, row.DotationComptabiliseeExercice);
        Assert.Equal(CurrentYearPostingStatus.Partial, row.PostingStatus);
    }

    [Fact]
    public async Task GetAmortizationReportAsync_ShouldBuildGroupedReportWithGrandTotal()
    {
        var (category, asset) = await CreatePersistedDraftAssetAsync();
        var loaded = await _repository.GetByIdAsync(asset.Id, includeSchedule: true);
        Assert.NotNull(loaded);

        loaded!.PutInService(new DateTime(2026, 3, 1), "404");
        var line = DepreciationScheduleLine.Create(
            loaded.Id,
            fiscalYear: 2026,
            periodMonth: 12,
            openingNbv: 50_000m,
            normalAnnualAmount: 10_000m,
            priorAccumulatedDepreciation: 1_000m,
            depreciationAmount: 5_000m,
            accumulatedDepreciation: 6_000m,
            closingNbv: 44_000m).Value;
        line.MarkPosted(Guid.NewGuid(), Guid.NewGuid());

        await _repository.ReplaceScheduleLinesAsync(loaded.Id, [line]);

        var report = await _repository.GetAmortizationReportAsync(
            fiscalYear: 2026,
            groupingMode: AmortizationReportGroupingMode.AssetAccount,
            status: null,
            categoryId: null,
            search: null,
            companyName: "Société Test");

        Assert.Equal("Société Test", report.Header.CompanyName);
        Assert.Single(report.Groups);
        Assert.Equal(50_000m, report.GrandTotal.OriginValue);
        Assert.Equal(5_000m, report.GrandTotal.DotationCalculeeExercice);
        Assert.Equal(5_000m, report.GrandTotal.DotationComptabiliseeExercice);
        Assert.NotEmpty(report.SummaryByNature);
    }

    [Fact]
    public async Task GetAmortizationReportAsync_FiscalCategoryGrouping_ShouldGroupByCategoryCode()
    {
        var (category, asset) = await CreatePersistedDraftAssetAsync();
        var loaded = await _repository.GetByIdAsync(asset.Id, includeSchedule: true);
        loaded!.PutInService(new DateTime(2026, 3, 1), "404");
        var line = DepreciationScheduleLine.Create(
            loaded.Id, 2026, 12, 50_000m, 10_000m, 0m, 2_000m, 2_000m, 48_000m).Value;
        await _repository.ReplaceScheduleLinesAsync(loaded.Id, [line]);

        var report = await _repository.GetAmortizationReportAsync(
            2026,
            AmortizationReportGroupingMode.FiscalCategory,
            null,
            null,
            null,
            string.Empty);

        Assert.Equal(AmortizationReportGroupingMode.FiscalCategory, report.GroupingMode);
        Assert.Equal(category.Code, report.Groups[0].GroupCode);
    }

    [Fact]
    public async Task GetAmortizationReportAsync_AssetWithoutFiscalYearLines_ShouldReturnZeroAmounts()
    {
        var (_, asset) = await CreatePersistedDraftAssetAsync();
        var loaded = await _repository.GetByIdAsync(asset.Id, includeSchedule: true);
        loaded!.PutInService(new DateTime(2026, 3, 1), "404");
        await _repository.ReplaceScheduleLinesAsync(loaded.Id, []);

        var report = await _repository.GetAmortizationReportAsync(
            2026,
            AmortizationReportGroupingMode.AssetAccount,
            null,
            null,
            null,
            string.Empty);

        var row = Assert.Single(report.Groups.SelectMany(g => g.Rows));
        Assert.Equal(0m, row.DotationCalculeeExercice);
        Assert.Equal(0m, row.DotationComptabiliseeExercice);
        Assert.Equal(CurrentYearPostingStatus.None, row.PostingStatus);
    }

    private async Task<(DepreciationRateCategory category, FixedAsset asset)> CreatePersistedDraftAssetAsync()
    {
        var category = DepreciationRateCategory.Create(
            "ROAD_TRANS_TEST",
            "Transport terrestre test",
            20m,
            "218",
            "2818",
            "6818",
            isNonDepreciable: false,
            sortOrder: 1);

        await using (var context = _contextFactory.CreateContext())
        {
            context.DepreciationRateCategories.Add(category);
            await context.SaveChangesAsync();
        }

        var asset = FixedAsset.Create(
            "IMMO-2026-0001",
            "Camion",
            category.Id,
            category.LegalRatePercent,
            category.UsefulLifeYears,
            "218",
            "2818",
            "6818",
            50_000m,
            0m,
            0m,
            new DateTime(2026, 1, 10)).Value;

        asset.SetAuditInfo("test@factutrust.tn", isUpdate: false);
        await _repository.AddAsync(asset);

        return (category, asset);
    }

    public void Dispose()
    {
        using var context = _contextFactory.CreateContext();
        context.Database.EnsureDeleted();
    }
}

/// <summary>
/// Integration tests for SqlServerRetryingExecutionStrategy + manual transactions.
/// Requires FACTUTRUST_TEST_SQL_CONNECTION or LocalDB; no-op when SQL unavailable.
/// </summary>
public sealed class FixedAssetRepositorySqlRetryTests : IDisposable
{
    private readonly string? _connectionString;
    private readonly bool _canRun;

    public FixedAssetRepositorySqlRetryTests()
    {
        _connectionString = Environment.GetEnvironmentVariable("FACTUTRUST_TEST_SQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            _connectionString = TryBuildLocalDbConnectionString();
        }

        _canRun = CanConnect(_connectionString);
    }

    [Fact]
    public async Task BeginTransaction_WithoutExecutionStrategy_ThrowsOnSqlServerRetry()
    {
        if (!_canRun)
            return;

        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.Database.BeginTransactionAsync(CancellationToken.None));
    }

    [Fact]
    public async Task PutInServiceInTransactionAsync_WithSqlServerRetry_ShouldSucceed()
    {
        if (!_canRun)
            return;

        var factory = new SqlRetryTenantDbContextFactory(_connectionString!);
        var repository = new FixedAssetRepository(factory);

        await using (var context = factory.CreateContext())
        {
            await context.Database.EnsureCreatedAsync();
        }

        var category = DepreciationRateCategory.Create(
            "ROAD_TRANS_SQL",
            "Transport SQL retry test",
            20m,
            "218",
            "2818",
            "6818",
            isNonDepreciable: false,
            sortOrder: 1);

        await using (var seedContext = factory.CreateContext())
        {
            seedContext.DepreciationRateCategories.Add(category);
            await seedContext.SaveChangesAsync();
        }

        var asset = FixedAsset.Create(
            "IMMO-SQL-0001",
            "Camion SQL retry",
            category.Id,
            category.LegalRatePercent,
            category.UsefulLifeYears,
            "218",
            "2818",
            "6818",
            50_000m,
            0m,
            0m,
            new DateTime(2026, 1, 10)).Value;

        asset.SetAuditInfo("test@factutrust.tn", isUpdate: false);
        await repository.AddAsync(asset);

        var engine = new FactuTrust.Infrastructure.Services.DepreciationEngine();
        var simulated = (await repository.GetByIdAsync(asset.Id, includeSchedule: true))!;
        simulated.PutInService(new DateTime(2026, 3, 1), "404");
        var lines = engine.GenerateSchedule(simulated);

        var result = await repository.PutInServiceInTransactionAsync(
            asset.Id,
            new DateTime(2026, 3, 1),
            "404",
            lines,
            "test@factutrust.tn");

        Assert.True(result.IsSuccess);
        Assert.Equal(FixedAssetStatus.InService, result.Value.Status);

        var idempotent = await repository.PutInServiceInTransactionAsync(
            asset.Id,
            new DateTime(2026, 3, 1),
            "404",
            lines,
            "test@factutrust.tn");

        Assert.True(idempotent.IsSuccess);
        Assert.Equal(FixedAssetStatus.InService, idempotent.Value.Status);
    }

    private TenantDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseSqlServer(
                _connectionString!,
                sql => sql.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null))
            .Options;

        return new TenantDbContext(options);
    }

    private static bool CanConnect(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return false;

        try
        {
            var options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseSqlServer(connectionString)
                .Options;
            using var context = new TenantDbContext(options);
            return context.Database.CanConnect();
        }
        catch
        {
            return false;
        }
    }

    private static string? TryBuildLocalDbConnectionString()
    {
        var dbName = $"FactuTrust_FixedAssetRetry_{Guid.NewGuid():N}";
        return $"Server=(localdb)\\mssqllocaldb;Database={dbName};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";
    }

    public void Dispose()
    {
        if (!_canRun || string.IsNullOrWhiteSpace(_connectionString))
            return;

        try
        {
            using var context = CreateContext();
            context.Database.EnsureDeleted();
        }
        catch
        {
            // Best-effort cleanup for optional integration tests.
        }
    }

    private sealed class SqlRetryTenantDbContextFactory : ITenantDbContextFactory
    {
        private readonly string _connectionString;

        public SqlRetryTenantDbContextFactory(string connectionString) => _connectionString = connectionString;

        public TenantDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseSqlServer(
                    _connectionString,
                    sql => sql.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorNumbersToAdd: null))
                .Options;

            return new TenantDbContext(options);
        }
    }
}