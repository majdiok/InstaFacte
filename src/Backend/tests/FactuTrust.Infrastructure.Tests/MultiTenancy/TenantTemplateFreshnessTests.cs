using FactuTrust.Infrastructure.MultiTenancy;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.MultiTenancy;

public sealed class TenantTemplateFreshnessTests
{
    [Fact]
    public void CanRestoreWithoutRebuild_WhenBackupTemplateAndCatalogAreCurrent()
    {
        Assert.True(TenantTemplateFreshness.CanRestoreWithoutRebuild(
            backupFileExists: true,
            templateDatabaseExists: true,
            pendingMigrationCount: 0,
            nctCatalogApplied: true));
    }

    [Theory]
    [InlineData(false, true, 0, true)]
    [InlineData(true, false, 0, true)]
    [InlineData(true, true, 1, true)]
    [InlineData(true, true, 0, false)]
    public void CanRestoreWithoutRebuild_IsFalse_WhenAnyReadinessSignalIsMissing(
        bool backupFileExists,
        bool templateDatabaseExists,
        int pendingMigrationCount,
        bool nctCatalogApplied)
    {
        Assert.False(TenantTemplateFreshness.CanRestoreWithoutRebuild(
            backupFileExists,
            templateDatabaseExists,
            pendingMigrationCount,
            nctCatalogApplied));
    }
}
