using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

public sealed class AuditServiceTests
{
    private sealed class TestTenantDbContextFactory : ITenantDbContextFactory
    {
        private readonly string _databaseName;

        public TestTenantDbContextFactory(string databaseName)
        {
            _databaseName = databaseName;
        }

        public TenantDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseInMemoryDatabase(_databaseName)
                .Options;
            return new TenantDbContext(options);
        }
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public string? Email { get; } = "audit-test@example.com";
        public Guid? TenantId { get; } = Guid.NewGuid();
        public UserRole? Role => UserRole.Administrator;
        public bool IsAuthenticated => true;
        public bool IsAccountingFirmDelegatedContext => false;
        public bool HasPermission(string permission) => true;
        public string? IpAddress { get; } = "127.0.0.1";
        public string? UserAgent { get; } = "FactuTrust.Tests";
    }

    [Fact]
    public async Task LogAsync_ConcurrentCalls_ProducesValidChain()
    {
        var db = $"AuditRace_{Guid.NewGuid()}";
        var factory = new TestTenantDbContextFactory(db);
        var sut = new AuditService(factory, new TestCurrentUser());

        const int n = 25;
        var tasks = Enumerable.Range(0, n).Select(_ =>
            sut.LogAsync("Test.Action", "TestEntity", cancellationToken: CancellationToken.None));
        await Task.WhenAll(tasks);

        var query = new AuditLogQueryService(factory);
        var result = await query.VerifyChainAsync(CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsValid);
        Assert.Equal(n, result.Value.EntryCount);
        Assert.Equal(0, result.Value.DuplicatePreviousHashGroupCount);
    }
}
