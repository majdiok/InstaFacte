using FactuTrust.Domain.Entities;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

public sealed class AuditLogQueryServiceTests
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

    [Fact]
    public async Task GetLogsAsync_Empty_ReturnsPagedResultWithZeroTotal()
    {
        var db = $"AuditTest_{Guid.NewGuid()}";
        var factory = new TestTenantDbContextFactory(db);
        var sut = new AuditLogQueryService(factory);
        var r = await sut.GetLogsAsync(null, null, null, null, null, 1, 25, CancellationToken.None);
        Assert.True(r.IsSuccess);
        Assert.Equal(0, r.Value.TotalCount);
        Assert.Empty(r.Value.Items);
        Assert.Equal(1, r.Value.Page);
        Assert.Equal(25, r.Value.PageSize);
    }

    [Fact]
    public async Task GetByIdAsync_Missing_ReturnsNotFound()
    {
        var db = $"AuditTest_{Guid.NewGuid()}";
        var factory = new TestTenantDbContextFactory(db);
        var sut = new AuditLogQueryService(factory);
        var r = await sut.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);
        Assert.True(r.IsFailure);
        Assert.Equal($"{nameof(AuditLog)}.NotFound", r.Error.Code);
    }

    [Fact]
    public async Task VerifyChainAsync_Empty_IsValid()
    {
        var db = $"AuditVerify_{Guid.NewGuid()}";
        var factory = new TestTenantDbContextFactory(db);
        var sut = new AuditLogQueryService(factory);
        var r = await sut.VerifyChainAsync(CancellationToken.None);
        Assert.True(r.IsSuccess);
        Assert.True(r.Value.IsValid);
        Assert.Equal(0, r.Value.EntryCount);
        Assert.Equal(0, r.Value.DuplicatePreviousHashGroupCount);
    }

    [Fact]
    public async Task VerifyChainAsync_SequentialLogs_AfterRepair_IsValid()
    {
        var db = $"AuditVerify_{Guid.NewGuid()}";
        var factory = new TestTenantDbContextFactory(db);
        await using (var ctx = factory.CreateContext())
        {
            var a = AuditLog.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "u@x.com",
                "A1",
                "E",
                null,
                null,
                null,
                "127.0.0.1",
                null,
                "GENESIS");
            var b = AuditLog.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "u@x.com",
                "A2",
                "E",
                null,
                null,
                null,
                "127.0.0.1",
                null,
                "WRONG");
            ctx.AuditLogs.AddRange(a, b);
            await ctx.SaveChangesAsync();
        }

        var r1 = await new AuditLogQueryService(factory).VerifyChainAsync(CancellationToken.None);
        Assert.True(r1.IsSuccess);
        Assert.False(r1.Value.IsValid);

        var repair = new AuditChainRepairService(factory);
        var repairResult = await repair.ResealChainAsync(CancellationToken.None);
        Assert.True(repairResult.IsSuccess);
        Assert.Equal(2, repairResult.Value);

        var r2 = await new AuditLogQueryService(factory).VerifyChainAsync(CancellationToken.None);
        Assert.True(r2.IsSuccess);
        Assert.True(r2.Value.IsValid);
        Assert.Equal(2, r2.Value.EntryCount);
    }
}
