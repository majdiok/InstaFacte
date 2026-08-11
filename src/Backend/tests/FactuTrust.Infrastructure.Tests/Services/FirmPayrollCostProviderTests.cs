using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

public sealed class FirmPayrollCostProviderTests
{
    private static readonly Guid FirmId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task GetAnnualEmployerCosts_returns_unavailable_when_no_tenant_database()
    {
        var tenantService = new Mock<ITenantService>();
        tenantService
            .Setup(t => t.GetConnectionStringAsync(FirmId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var factory = new CountingTenantDbContextFactory();
        var provider = new FirmPayrollCostProvider(
            new FirmTenantPayrollAccessor(tenantService.Object, factory),
            NullLogger<FirmPayrollCostProvider>.Instance);

        var snapshot = await provider.GetAnnualEmployerCostsAsync(FirmId, 2026);

        Assert.False(snapshot.IsAvailable);
        Assert.Contains("base de paie", snapshot.UnavailableReason!, StringComparison.OrdinalIgnoreCase);
        // Sans chaîne de connexion, aucun contexte ne doit être ouvert.
        Assert.Equal(0, factory.IsolatedContextsCreated);
    }

    [Fact]
    public async Task GetAnnualEmployerCosts_opens_the_firm_database_through_the_isolated_factory()
    {
        // La lecture de la paie du cabinet part d'une unité de travail Master : elle ne doit jamais
        // s'enrôler dans la transaction ambiante du tenant courant, ni construire son contexte à la main.
        var tenantService = new Mock<ITenantService>();
        tenantService
            .Setup(t => t.GetConnectionStringAsync(FirmId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Server=(local);Database=cabinet;Trusted_Connection=True");

        var factory = new CountingTenantDbContextFactory();
        var provider = new FirmPayrollCostProvider(
            new FirmTenantPayrollAccessor(tenantService.Object, factory),
            NullLogger<FirmPayrollCostProvider>.Instance);

        var snapshot = await provider.GetAnnualEmployerCostsAsync(FirmId, 2026);

        Assert.Equal(1, factory.IsolatedContextsCreated);
        Assert.Equal(0, factory.AmbientContextsCreated);
        // Base vide : aucun salarié actif, donc snapshot indisponible — mais la traversée a bien eu lieu.
        Assert.False(snapshot.IsAvailable);
    }

    private sealed class CountingTenantDbContextFactory : ITenantDbContextFactory
    {
        private readonly string _databaseName = Guid.NewGuid().ToString();

        public int IsolatedContextsCreated { get; private set; }
        public int AmbientContextsCreated { get; private set; }

        public TenantDbContext CreateContext()
        {
            AmbientContextsCreated++;
            return Build();
        }

        public TenantDbContext CreateIsolatedContext()
        {
            IsolatedContextsCreated++;
            return Build();
        }

        public TenantDbContext CreateIsolatedContext(string connectionString) => CreateIsolatedContext();

        private TenantDbContext Build() =>
            new(new DbContextOptionsBuilder<TenantDbContext>()
                .UseInMemoryDatabase(_databaseName)
                .Options);
    }
}
