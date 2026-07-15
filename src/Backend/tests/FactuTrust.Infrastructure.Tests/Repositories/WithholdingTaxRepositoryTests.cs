using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Repositories;

public sealed class WithholdingTaxRepositoryTests : IDisposable
{
    private readonly string _databaseName;
    private readonly TestTenantDbContextFactory _contextFactory;
    private readonly WithholdingTaxRepository _repository;

    public WithholdingTaxRepositoryTests()
    {
        _databaseName = $"TestDb_{Guid.NewGuid()}";
        _contextFactory = new TestTenantDbContextFactory(_databaseName);
        _repository = new WithholdingTaxRepository(_contextFactory);
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

    [Fact]
    public async Task AddTypeAsync_ThenGetTypeByCodeAsync_ShouldReturnPersistedType()
    {
        var type = WithholdingTaxType.CreateSystem(
            "RS_TEST_001",
            WithholdingCategory.Honoraires,
            "Type test",
            5m,
            "Art. test");

        await _repository.AddTypeAsync(type);

        var loaded = await _repository.GetTypeByCodeAsync("RS_TEST_001");
        Assert.NotNull(loaded);
        Assert.Equal("Type test", loaded.Label);
        Assert.Equal(5m, loaded.DefaultRate);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
