using FactuTrust.Infrastructure.MultiTenancy;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.MultiTenancy;

public sealed class TenantBackupSqlTests
{
    [Fact]
    public void Build_WithCompression_IncludesCompressionClause()
    {
        var sql = TenantBackupSql.Build("FactuTrust_Tenant_Template", compression: true);

        Assert.Contains("BACKUP DATABASE [FactuTrust_Tenant_Template]", sql, StringComparison.Ordinal);
        Assert.Contains("COMPRESSION", sql, StringComparison.Ordinal);
        Assert.Contains("COPY_ONLY", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_WithoutCompression_OmitsCompressionClause()
    {
        var sql = TenantBackupSql.Build("FactuTrust_Tenant_Template", compression: false);

        Assert.Contains("BACKUP DATABASE [FactuTrust_Tenant_Template]", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("COMPRESSION", sql, StringComparison.Ordinal);
        Assert.Contains("COPY_ONLY", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_EscapesClosingBracketInDatabaseName()
    {
        var sql = TenantBackupSql.Build("Foo]]Bar", compression: false);

        Assert.Contains("BACKUP DATABASE [Foo]]Bar]", sql, StringComparison.Ordinal);
    }
}
