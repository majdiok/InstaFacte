using FactuTrust.Infrastructure.Migrations.Tenant;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.MultiTenancy;

/// <summary>
/// Reproduces the POS submit failure path: PriceResolver reads ClientProductPrices
/// with EF-mapped audit columns (CreatedBy / UpdatedBy / Version).
/// </summary>
public sealed class PricingAuditColumns_SteFatimaTests
{
    private const string SteFatimaConnection =
        "Server=(localdb)\\MSSQLLocalDB;Database=FactuTrust_Tenant_45907A7B;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

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
    public async Task EnsureCoreDocumentAuditColumns_OnSteFatima_ReturnsSuccess()
    {
        var result = await TenantCoreSchemaValidator.EnsureCoreDocumentAuditColumnsAsync(SteFatimaConnection);
        Assert.True(result.IsSuccess, result.Error?.Description);
    }

    [Fact]
    public async Task ClientProductPrices_EfQueryWithAuditColumns_DoesNotThrow()
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseSqlServer(SteFatimaConnection)
            .Options;

        await using var context = new TenantDbContext(options);

        // Same shape as ClientProductPriceRepository.GetForClientProductAsync — must not
        // throw SqlException 207 Invalid column name CreatedBy/UpdatedBy/Version.
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
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseSqlServer(SteFatimaConnection)
            .Options;

        await using var context = new TenantDbContext(options);

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
}
