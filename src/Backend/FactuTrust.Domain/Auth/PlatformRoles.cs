namespace FactuTrust.Domain.Auth;

/// <summary>
/// ASP.NET Identity role names for platform operators (not tenant <see cref="Enums.UserRole"/>).
///
/// Lot B1 — Multi-admins plateforme. Cinq rôles métier hiérarchisés :
/// <list type="bullet">
///   <item><see cref="PlatformAdmin"/> — super-administrateur, toutes permissions (rôle historique).</item>
///   <item><see cref="BillingAdmin"/> — pilote facturation, plans, coupons, factures.</item>
///   <item><see cref="SupportAgent"/> — support client, lecture audit, modération vitrines.</item>
///   <item><see cref="MigrationOperator"/> — exécute migrations EF, lecture tenants.</item>
///   <item><see cref="ReadOnlyAuditor"/> — lecture seule de tout (compliance).</item>
/// </list>
///
/// Tout utilisateur plateforme (TenantId = Guid.Empty) doit avoir <b>au moins un</b> de ces rôles.
/// La policy <c>PlatformAdmin</c> dans <c>PlatformPolicies</c> est étendue Lot B1 pour accepter
/// n'importe lequel des 5 rôles (rétro-compat : un user "PlatformAdmin" continue à passer).
/// </summary>
public static class PlatformRoles
{
    public const string PlatformAdmin = "PlatformAdmin";
    public const string BillingAdmin = "BillingAdmin";
    public const string SupportAgent = "SupportAgent";
    public const string MigrationOperator = "MigrationOperator";
    public const string ReadOnlyAuditor = "ReadOnlyAuditor";

    /// <summary>Tous les rôles plateforme reconnus, dans l'ordre hiérarchique.</summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        PlatformAdmin,
        BillingAdmin,
        SupportAgent,
        MigrationOperator,
        ReadOnlyAuditor
    };

    /// <summary>Indique si le nom de rôle fourni correspond à un rôle plateforme connu.</summary>
    public static bool IsKnownRole(string? roleName)
        => roleName is not null && All.Contains(roleName);
}
