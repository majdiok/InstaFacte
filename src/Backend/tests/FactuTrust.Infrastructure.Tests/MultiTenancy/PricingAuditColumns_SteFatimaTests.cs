using FactuTrust.Infrastructure.Migrations.Tenant;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.MultiTenancy;

/// <summary>
/// Reproduces the POS submit failure path against an isolated provisioned tenant database:
/// PriceResolver reads pricing tables with EF-mapped audit columns.
/// </summary>
public sealed class PricingAuditColumnsTests : IDisposable
{
    private readonly SqlTestDatabase _sqlDb = new(nameof(PricingAuditColumnsTests));

    [Fact]
    public void AddPricingAuditColumns_Migration_IsRegistered()
    {
        var migrationAttribute = typeof(AddPricingAuditColumns_Tenant)
            .GetCustomAttributes(typeof(MigrationAttribute), inherit: false)
            .Cast<MigrationAttribute>()
            .Single();

        Assert.Equal("20260731180000_AddPricingAuditColumns_Tenant", migrationAttribute.Id);
    }

    [Fact]
    public async Task EnsureCoreDocumentAuditColumns_OnProvisionedTenant_ReturnsSuccess()
    {
        if (!_sqlDb.CanRun) return;

        var result = await TenantCoreSchemaValidator.EnsureCoreDocumentAuditColumnsAsync(_sqlDb.ConnectionString!);
        Assert.True(result.IsSuccess, result.Error?.Description);
    }

    [Fact]
    public async Task ClientProductPrices_EfQueryWithAuditColumns_DoesNotThrow()
    {
        if (!_sqlDb.CanRun) return;

        await using var context = CreateContext();

        var rows = await context.ClientProductPrices
            .AsNoTracking()
            .Select(p => new { p.Id, p.CreatedBy, p.UpdatedBy, p.Version, p.ClientId, p.ProductId })
            .Take(5)
            .ToListAsync();

        Assert.NotNull(rows);
    }

    [Fact]
    public async Task Promotions_And_PriceLists_EfQueryWithAuditColumns_DoesNotThrow()
    {
        if (!_sqlDb.CanRun) return;

        await using var context = CreateContext();

        var promotions = await context.Promotions
            .AsNoTracking()
            .Select(p => new { p.Id, p.CreatedBy, p.UpdatedBy, p.Version })
            .Take(5)
            .ToListAsync();

        var priceLists = await context.PriceLists
            .AsNoTracking()
            .Select(p => new { p.Id, p.CreatedBy, p.UpdatedBy, p.Version })
            .Take(5)
            .ToListAsync();

        Assert.NotNull(promotions);
        Assert.NotNull(priceLists);
    }

    private TenantDbContext CreateContext() => new(
        new DbContextOptionsBuilder<TenantDbContext>()
            .UseSqlServer(_sqlDb.ConnectionString!)
            .Options);

    public void Dispose() => _sqlDb.Dispose();
}
