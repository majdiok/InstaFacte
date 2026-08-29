using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Repositories;

/// <summary>
/// Durcissement de la numérotation IMMO-{année}-{seq:D4} (T5/B2) : MAX+1 (pas COUNT+1) et retry
/// ciblé sur violation d'unicité.
/// Note : comme <c>RecurringContractNumberingTests</c> (même patron), le provider InMemory ne
/// valide pas l'index unique SQL — le chemin de retry sur <c>SqlException</c> 2601/2627
/// (nouveau contexte + séquence recalculée) est couvert par revue de code + test manuel sur SQL
/// Server (voir aussi <c>FixedAssetRepositorySqlRetryTests</c>, no-op si SQL indisponible), pas
/// par un test automatisé ici.
/// </summary>
public sealed class FixedAssetInventoryNumberingTests : IDisposable
{
    private readonly string _databaseName;
    private readonly TestTenantDbContextFactory _contextFactory;
    private readonly FixedAssetRepository _repository;

    public FixedAssetInventoryNumberingTests()
    {
        _databaseName = $"TestDb_FixedAssetInventoryNumbering_{Guid.NewGuid()}";
        _contextFactory = new TestTenantDbContextFactory(_databaseName);
        _repository = new FixedAssetRepository(_contextFactory);
    }

    [Fact]
    public async Task GetNextInventorySequenceAsync_WithGapInNumbering_ReturnsMaxPlusOne()
    {
        var year = 2026;
        await SeedFixedAssetAsync($"IMMO-{year}-0001");
        await SeedFixedAssetAsync($"IMMO-{year}-0005");

        var next = await _repository.GetNextInventorySequenceAsync(year, CancellationToken.None);

        // MAX+1 (0006), pas COUNT+1 (0003) : les trous de numérotation ne provoquent pas de
        // collision avec un numéro déjà attribué.
        Assert.Equal(6, next);
    }

    [Fact]
    public async Task GetNextInventorySequenceAsync_NoExistingAsset_ReturnsOne()
    {
        var next = await _repository.GetNextInventorySequenceAsync(2026, CancellationToken.None);

        Assert.Equal(1, next);
    }

    [Fact]
    public async Task GetNextInventorySequenceAsync_IgnoresOtherYears()
    {
        await SeedFixedAssetAsync("IMMO-2025-0009");

        var next = await _repository.GetNextInventorySequenceAsync(2026, CancellationToken.None);

        Assert.Equal(1, next);
    }

    [Fact]
    public async Task AddWithGeneratedInventoryNumberAsync_TwoSequentialCalls_ProduceDistinctSequentialNumbers()
    {
        var year = 2026;

        var first = await _repository.AddWithGeneratedInventoryNumberAsync(
            number => BuildDraftAsset(number),
            year,
            CancellationToken.None);
        var second = await _repository.AddWithGeneratedInventoryNumberAsync(
            number => BuildDraftAsset(number),
            year,
            CancellationToken.None);

        Assert.True(first.IsSuccess, first.Error?.Description);
        Assert.True(second.IsSuccess, second.Error?.Description);
        Assert.Equal($"IMMO-{year}-0001", first.Value.InventoryNumber);
        Assert.Equal($"IMMO-{year}-0002", second.Value.InventoryNumber);

        await using var verify = _contextFactory.CreateContext();
        var numbers = await verify.FixedAssets.AsNoTracking().Select(a => a.InventoryNumber).ToListAsync();
        Assert.Equal(2, numbers.Count);
        Assert.Equal(2, numbers.Distinct().Count());
    }

    [Fact]
    public async Task AddWithGeneratedInventoryNumberAsync_FactoryFailure_ReturnsFailureWithoutRetryOrPersistence()
    {
        var year = 2026;
        var factoryCalls = 0;

        var result = await _repository.AddWithGeneratedInventoryNumberAsync(
            _ =>
            {
                factoryCalls++;
                return Result.Failure<FixedAsset>(Error.Validation("Label", "Libellé requis."));
            },
            year,
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(1, factoryCalls); // pas de retry sur un échec de validation (factory), seulement sur SqlException ciblée.

        await using var verify = _contextFactory.CreateContext();
        Assert.Empty(await verify.FixedAssets.AsNoTracking().ToListAsync());

        // Aucun numéro n'a été « brûlé » : le prochain calcul repart toujours à 0001, puisque
        // rien n'a été persisté (pas de compteur séparé, MAX est recalculé sur les données réelles).
        var next = await _repository.GetNextInventorySequenceAsync(year, CancellationToken.None);
        Assert.Equal(1, next);
    }

    private async Task SeedFixedAssetAsync(string inventoryNumber)
    {
        await using var context = _contextFactory.CreateContext();
        context.FixedAssets.Add(BuildDraftAsset(inventoryNumber).Value);
        await context.SaveChangesAsync();
    }

    private static Result<FixedAsset> BuildDraftAsset(string inventoryNumber)
    {
        var created = FixedAsset.Create(
            inventoryNumber,
            "Camion",
            Guid.NewGuid(),
            20m,
            5m,
            "228",
            "2828",
            "68112",
            50_000m,
            0m,
            0m,
            new DateTime(2026, 1, 10));

        if (created.IsSuccess)
            created.Value.SetAuditInfo("test@factutrust.tn", isUpdate: false);

        return created;
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

    public void Dispose()
    {
        using var context = _contextFactory.CreateContext();
        context.Database.EnsureDeleted();
    }
}
