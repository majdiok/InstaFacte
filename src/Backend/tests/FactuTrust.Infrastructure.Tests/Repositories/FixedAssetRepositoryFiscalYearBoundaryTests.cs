using FactuTrust.Application.Common.Fiscal;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Repositories;

/// <summary>
/// P3 « Exercices décalés » — éligibilité par frontière d'exercice dans
/// <see cref="FixedAssetRepository.SearchAsync"/> et
/// <see cref="FixedAssetRepository.GetActiveForDepreciationRunAsync"/> : la clé d'exercice de la
/// mise en service (et non l'année civile) détermine l'éligibilité. Parité stricte exercice civil.
/// </summary>
public sealed class FixedAssetRepositoryFiscalYearBoundaryTests : IDisposable
{
    private readonly string _databaseName;
    private readonly TestTenantDbContextFactory _contextFactory;
    private readonly FixedAssetRepository _repository;

    public FixedAssetRepositoryFiscalYearBoundaryTests()
    {
        _databaseName = $"TestDb_FaFyBoundary_{Guid.NewGuid()}";
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

    private async Task<FixedAsset> CreatePersistedInServiceAssetAsync(string inventoryNumber, DateTime inServiceDate)
    {
        var category = DepreciationRateCategory.Create(
            "ROAD_TRANS_TEST", "Transport terrestre test", 20m, "228", "2828", "68112",
            isNonDepreciable: false, sortOrder: 1);

        await using (var context = _contextFactory.CreateContext())
        {
            context.DepreciationRateCategories.Add(category);
            await context.SaveChangesAsync();
        }

        var asset = FixedAsset.Create(
            inventoryNumber,
            "Machine",
            category.Id,
            category.LegalRatePercent,
            category.UsefulLifeYears,
            "228",
            "2828",
            "68112",
            50_000m,
            0m,
            0m,
            inServiceDate).Value;
        asset.PutInService(inServiceDate, "404");
        asset.SetAuditInfo("test@factutrust.tn", isUpdate: false);
        await _repository.AddAsync(asset);
        return asset;
    }

    [Fact]
    public async Task SearchAsync_OffsetFiscalYear_IncludesAssetInFilteredFiscalYear_EvenIfLaterCalendarYear()
    {
        // Mise en service 15/03/2027 : exercice décalé juillet→juin ⇒ clé 2026 (mars < juillet).
        var asset = await CreatePersistedInServiceAssetAsync("IMMO-2027-0001", new DateTime(2027, 3, 15));

        var (items, _) = await _repository.SearchAsync(1, 100, null, null, 2026, null, 7, CancellationToken.None);

        Assert.Contains(items, a => a.Id == asset.Id);
    }

    [Fact]
    public async Task SearchAsync_CivilFiscalYear_ExcludesAssetFromLaterCalendarYear_Parity()
    {
        // Même actif (15/03/2027) : exercice civil ⇒ clé 2027 > 2026 → exclu (comportement historique).
        var asset = await CreatePersistedInServiceAssetAsync("IMMO-2027-0001", new DateTime(2027, 3, 15));

        var (items, _) = await _repository.SearchAsync(1, 100, null, null, 2026, null, 1, CancellationToken.None);

        Assert.DoesNotContain(items, a => a.Id == asset.Id);
    }

    [Fact]
    public async Task SearchAsync_OffsetFiscalYear_ExcludesAssetFromLaterFiscalYear()
    {
        // Mise en service 15/09/2027 : exercice décalé juillet→juin ⇒ clé 2027 > 2026 → exclu.
        var asset = await CreatePersistedInServiceAssetAsync("IMMO-2027-0002", new DateTime(2027, 9, 15));

        var (items, _) = await _repository.SearchAsync(1, 100, null, null, 2026, null, 7, CancellationToken.None);

        Assert.DoesNotContain(items, a => a.Id == asset.Id);
    }

    [Fact]
    public async Task SearchAsync_OffsetFiscalYear_IncludesAssetExactlyAtFiscalYearStart()
    {
        // Mise en service 01/07/2026 : premier jour de l'exercice 2026 (juillet→juin) ⇒ clé 2026.
        var asset = await CreatePersistedInServiceAssetAsync("IMMO-2026-0007", new DateTime(2026, 7, 1));

        var (items, _) = await _repository.SearchAsync(1, 100, null, null, 2026, null, 7, CancellationToken.None);

        Assert.Contains(items, a => a.Id == asset.Id);
    }

    [Fact]
    public async Task GetActiveForDepreciationRunAsync_OffsetFiscalYear_BoundaryIncludesAndExcludes()
    {
        // Clé 2026 (mise en service 15/03/2027, mars < juillet) et clé 2027 (15/09/2027).
        var inFy = await CreatePersistedInServiceAssetAsync("IMMO-2027-0001", new DateTime(2027, 3, 15));
        var outFy = await CreatePersistedInServiceAssetAsync("IMMO-2027-0002", new DateTime(2027, 9, 15));

        var active = await _repository.GetActiveForDepreciationRunAsync(2026, 7, CancellationToken.None);

        Assert.Contains(active, a => a.Id == inFy.Id);
        Assert.DoesNotContain(active, a => a.Id == outFy.Id);
    }

    [Fact]
    public async Task GetActiveForDepreciationRunAsync_CivilFiscalYear_UsesCalendarYear_Parity()
    {
        // Exercice civil : les deux actifs (2027) sont exclus du filtre 2026 (comportement historique).
        var a1 = await CreatePersistedInServiceAssetAsync("IMMO-2027-0001", new DateTime(2027, 3, 15));
        var a2 = await CreatePersistedInServiceAssetAsync("IMMO-2027-0002", new DateTime(2027, 9, 15));

        var active = await _repository.GetActiveForDepreciationRunAsync(2026, 1, CancellationToken.None);

        Assert.DoesNotContain(active, a => a.Id == a1.Id);
        Assert.DoesNotContain(active, a => a.Id == a2.Id);
    }

    public void Dispose()
    {
        using var context = _contextFactory.CreateContext();
        context.Database.EnsureDeleted();
    }
}
