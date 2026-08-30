using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Tests.Fixtures;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.MultiTenancy;

public sealed class TenantCoreSchemaValidatorTests : IDisposable
{
    private readonly SqlTestDatabase _sqlDb = new(nameof(TenantCoreSchemaValidatorTests));

    [Fact]
    public async Task EnsureInvoiceAuditColumnsAsync_OnProvisionedTenant_ReturnsSuccess()
    {
        if (!_sqlDb.CanRun) return;

        var result = await TenantCoreSchemaValidator.EnsureInvoiceAuditColumnsAsync(_sqlDb.ConnectionString!);

        Assert.True(result.IsSuccess, result.Error?.Description);
    }

    [Fact]
    public async Task EnsureCoreDocumentAuditColumnsAsync_OnProvisionedTenant_ReturnsSuccess()
    {
        if (!_sqlDb.CanRun) return;

        var result = await TenantCoreSchemaValidator.EnsureCoreDocumentAuditColumnsAsync(_sqlDb.ConnectionString!);

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

    public void Dispose() => _sqlDb.Dispose();
}
