using FactuTrust.Infrastructure.MultiTenancy;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.MultiTenancy;

public sealed class TenantCoreSchemaValidatorTests
{
    [Fact]
    public async Task EnsureInvoiceAuditColumnsAsync_OnDefaultTenant_ReturnsSuccess()
    {
        var connectionString =
            "Server=(localdb)\\MSSQLLocalDB;Database=FactuTrust_Tenant_Default;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

        var result = await TenantCoreSchemaValidator.EnsureInvoiceAuditColumnsAsync(connectionString);

        Assert.True(result.IsSuccess, result.Error?.Description);
    }

    [Fact]
    public async Task EnsureCoreDocumentAuditColumnsAsync_OnDefaultTenant_ReturnsSuccess()
    {
        var connectionString =
            "Server=(localdb)\\MSSQLLocalDB;Database=FactuTrust_Tenant_Default;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

        var result = await TenantCoreSchemaValidator.EnsureCoreDocumentAuditColumnsAsync(connectionString);

        Assert.True(result.IsSuccess, result.Error?.Description);
    }

    [Fact]
    public void BuildMissingColumnsQuery_IncludesCorePosTables()
    {
        var query = TenantCoreSchemaValidator.BuildMissingColumnsQuery();

        foreach (var table in new[]
                 {
                     "Invoices", "Clients", "InvoiceLines", "InvoiceDrafts",
                     "Quotes", "SalesOrders", "DeliveryNotes", "Products",
                     "ClientProductPrices", "PriceLists", "Promotions"
                 })
        {
            Assert.Contains($"N'{table}'", query, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task EnsureCoreDocumentAuditColumnsAsync_OnInstaIADevTenant_ReturnsSuccess()
    {
        var connectionString =
            "Server=(localdb)\\MSSQLLocalDB;Database=FactuTrust_Tenant_EDEA3855;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

        var result = await TenantCoreSchemaValidator.EnsureCoreDocumentAuditColumnsAsync(connectionString);

        Assert.True(result.IsSuccess, result.Error?.Description);
    }
}
