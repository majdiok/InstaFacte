using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.MultiTenancy;

public sealed class TenantMigrationGuardTests
{
    [Fact]
    public void Invalidate_RemovesCachedMigrationResult()
    {
        var tenantId = Guid.NewGuid();
        var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set($"{TenantMigrationGuard.CacheKeyPrefix}{tenantId}", true, TimeSpan.FromHours(1));

        var tenantService = new Mock<ITenantService>();
        var environment = new Mock<IHostEnvironment>();
        environment.Setup(e => e.EnvironmentName).Returns(Environments.Development);

        ITenantMigrationGuard guard = new TenantMigrationGuard(
            cache,
            tenantService.Object,
            NullLogger<TenantMigrationGuard>.Instance,
            environment.Object);

        guard.Invalidate(tenantId);

        Assert.False(cache.TryGetValue($"{TenantMigrationGuard.CacheKeyPrefix}{tenantId}", out _));
    }

    [Fact]
    public void Invalidate_WithEmptyTenantId_IsNoOp()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var tenantService = new Mock<ITenantService>();
        var environment = new Mock<IHostEnvironment>();

        ITenantMigrationGuard guard = new TenantMigrationGuard(
            cache,
            tenantService.Object,
            NullLogger<TenantMigrationGuard>.Instance,
            environment.Object);

        guard.Invalidate(Guid.Empty);

        tenantService.Verify(
            s => s.GetConnectionStringAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void MarkApplied_CachesSuccessfulMigrationResult()
    {
        var tenantId = Guid.NewGuid();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var tenantService = new Mock<ITenantService>();
        var environment = new Mock<IHostEnvironment>();
        environment.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        ITenantMigrationGuard guard = new TenantMigrationGuard(
            cache,
            tenantService.Object,
            NullLogger<TenantMigrationGuard>.Instance,
            environment.Object);

        guard.MarkApplied(tenantId);

        Assert.True(cache.TryGetValue($"{TenantMigrationGuard.CacheKeyPrefix}{tenantId}", out bool applied));
        Assert.True(applied);
        tenantService.Verify(
            s => s.GetConnectionStringAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void MarkApplied_WithEmptyTenantId_IsNoOp()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        TenantMigrationGuard.MarkApplied(cache, Guid.Empty);

        Assert.False(cache.TryGetValue($"{TenantMigrationGuard.CacheKeyPrefix}{Guid.Empty}", out _));
    }
}
