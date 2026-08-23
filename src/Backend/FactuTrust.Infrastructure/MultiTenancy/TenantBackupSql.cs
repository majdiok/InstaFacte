namespace FactuTrust.Infrastructure.MultiTenancy;

/// <summary>
/// SQL used to backup the tenant template. LocalDB / Express reject COMPRESSION.
/// </summary>
public static class TenantBackupSql
{
    public static string Build(string escapedDatabaseName, bool compression)
    {
        var withClause = compression
            ? "WITH INIT, COPY_ONLY, COMPRESSION, STATS = 10"
            : "WITH INIT, COPY_ONLY, STATS = 10";

        return $@"
BACKUP DATABASE [{escapedDatabaseName}]
TO DISK = @path
{withClause}";
    }
}
