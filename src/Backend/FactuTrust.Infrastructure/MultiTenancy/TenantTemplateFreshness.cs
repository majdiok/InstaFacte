namespace FactuTrust.Infrastructure.MultiTenancy;

/// <summary>
/// Pure decision helper for the TemplateClone hot path: restore the existing .bak
/// without rebuilding/backing up the template database.
/// </summary>
public static class TenantTemplateFreshness
{
    public static bool CanRestoreWithoutRebuild(
        bool backupFileExists,
        bool templateDatabaseExists,
        int pendingMigrationCount,
        bool nctCatalogApplied) =>
        backupFileExists
        && templateDatabaseExists
        && pendingMigrationCount <= 0
        && nctCatalogApplied;
}
