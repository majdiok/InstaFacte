namespace FactuTrust.API.Middleware;

/// <summary>
/// API paths that accounting firms can access without a provisioned tenant database
/// (master DB only). Enables future async provisioning without blocking dashboard/login sync.
/// </summary>
public static class AccountingFirmMasterOnlyRoutes
{
    private static readonly string[] Prefixes =
    [
        "/api/auth/me",
        "/api/firm/dashboard",
        "/api/firm/context",
        "/api/firm/users",
        "/api/firm-assignments",
        "/api/firm/governance",
        "/api/firm/managed-clients",
        "/api/firm/fiscal-schedule",
        "/api/firm/fiscal-ops"
    ];

    public static bool IsMatch(string path, string? tenantKindClaim)
    {
        if (!string.Equals(tenantKindClaim, "accountingFirm", StringComparison.OrdinalIgnoreCase))
            return false;

        foreach (var prefix in Prefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
