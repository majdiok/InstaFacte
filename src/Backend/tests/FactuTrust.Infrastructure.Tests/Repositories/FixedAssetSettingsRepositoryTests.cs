using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Repositories;

/// <summary>
/// Tests du dépôt <see cref="FixedAssetSettingsRepository"/> (plan « Exercices décalés », P1) :
/// défaut usine (exercice civil) en lecture sans persistance, upsert create puis update.
/// EF InMemory — même patron que <see cref="FixedAssetRepositoryTests"/>.
/// </summary>
public sealed class FixedAssetSettingsRepositoryTests : IDisposable
{
    private readonly string _databaseName;
    private readonly TestTenantDbContextFactory _contextFactory;
    private readonly FixedAssetSettingsRepository _repository;

    public FixedAssetSettingsRepositoryTests()
    {
        _databaseName = $"TestDb_FixedAssetSettings_{Guid.NewGuid()}";
        _contextFactory = new TestTenantDbContextFactory(_databaseName);
        _repository = new FixedAssetSettingsRepository(_contextFactory);
    }

    public void Dispose() => _contextFactory.Dispose();

    private sealed class TestTenantDbContextFactory : ITenantDbContextFactory, IDisposable
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

        public TenantDbContext CreateIsolatedContext() => CreateContext();
        public TenantDbContext CreateIsolatedContext(string connectionString) => CreateContext();
        public void Dispose() { }
    }

    [Fact]
    public async Task GetForTenantAsync_EmptyDatabase_ReturnsCivilDefaultWithoutPersisting()
    {
        var settings = await _repository.GetForTenantAsync();

        Assert.Equal(1, settings.FiscalYearStartMonth);
        Assert.Equal("N/N+1", settings.FiscalYearLabelFormat);

        // Aucune ligne écrite en base par la lecture.
        await using var verify = _contextFactory.CreateContext();
        Assert.Equal(0, await verify.FixedAssetSettings.CountAsync());
    }

    [Fact]
    public async Task UpsertAsync_OnEmptyDatabase_CreatesSingletonRow()
    {
        var persisted = await _repository.UpsertAsync(7, "N/N+1", "test@factutrust.tn");

        Assert.Equal(7, persisted.FiscalYearStartMonth);
        Assert.Equal("N/N+1", persisted.FiscalYearLabelFormat);
        Assert.Equal("test@factutrust.tn", persisted.CreatedBy);

        await using var verify = _contextFactory.CreateContext();
        Assert.Equal(1, await verify.FixedAssetSettings.CountAsync());
    }

    [Fact]
    public async Task GetForTenantAsync_AfterUpsert_ReturnsPersistedValues()
    {
        await _repository.UpsertAsync(7, "N", "test@factutrust.tn");

        var settings = await _repository.GetForTenantAsync();

        Assert.Equal(7, settings.FiscalYearStartMonth);
        Assert.Equal("N", settings.FiscalYearLabelFormat);
    }

    [Fact]
    public async Task UpsertAsync_Twice_UpdatesInPlace_KeepsSingleRow()
    {
        await _repository.UpsertAsync(7, "N/N+1", "creator@factutrust.tn");
        var updated = await _repository.UpsertAsync(4, "N", "editor@factutrust.tn");

        Assert.Equal(4, updated.FiscalYearStartMonth);
        Assert.Equal("N", updated.FiscalYearLabelFormat);
        Assert.Equal("editor@factutrust.tn", updated.UpdatedBy);

        // Toujours une seule ligne (singleton mis à jour en place, pas de doublon).
        await using var verify = _contextFactory.CreateContext();
        Assert.Equal(1, await verify.FixedAssetSettings.CountAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public async Task UpsertAsync_InvalidStartMonth_ShouldThrow(int startMonth)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _repository.UpsertAsync(startMonth, "N/N+1", "test@factutrust.tn"));
    }

    [Fact]
    public async Task UpsertAsync_InvalidLabelFormat_ShouldThrow()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _repository.UpsertAsync(7, "X/Y", "test@factutrust.tn"));
    }
}
