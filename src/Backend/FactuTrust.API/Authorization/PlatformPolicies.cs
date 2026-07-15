using FactuTrust.Domain.Auth;

namespace FactuTrust.API.Authorization;

/// <summary>
/// Constantes de policies d'autorisation plateforme.
///
/// Lot B1 — élargi de "PlatformAdmin only" à un système hiérarchique 5 rôles +
/// permissions fines.
///
/// <b>Compatibilité</b> : la policy <see cref="PlatformAdmin"/> historique reste
/// la "porte d'entrée" plateforme — Lot B1 elle accepte n'importe lequel des 5 rôles
/// (auparavant <c>PlatformAdmin</c> seul). Les permissions fines sont ensuite vérifiées
/// par <c>perm:&lt;clé&gt;</c> via <see cref="PolicyFor(string)"/>.
/// </summary>
public static class PlatformPolicies
{
    /// <summary>
    /// Porte d'entrée plateforme : exige n'importe lequel des rôles déclarés dans
    /// <see cref="PlatformRoles.All"/>. Conserve son nom historique pour rétro-compat
    /// avec tous les contrôleurs existants annotés <c>[Authorize(Policy = PlatformAdmin)]</c>.
    /// </summary>
    public const string PlatformAdmin = nameof(PlatformAdmin);

    /// <summary>Policy exigeant strictement le rôle <see cref="PlatformRoles.PlatformAdmin"/> (super-admin).</summary>
    public const string SuperAdminOnly = nameof(SuperAdminOnly);

    /// <summary>Policy exigeant le rôle <see cref="PlatformRoles.BillingAdmin"/>.</summary>
    public const string BillingAdminOnly = nameof(BillingAdminOnly);

    /// <summary>Policy exigeant le rôle <see cref="PlatformRoles.SupportAgent"/>.</summary>
    public const string SupportAgentOnly = nameof(SupportAgentOnly);

    /// <summary>Policy exigeant le rôle <see cref="PlatformRoles.MigrationOperator"/>.</summary>
    public const string MigrationOperatorOnly = nameof(MigrationOperatorOnly);

    /// <summary>Policy exigeant le rôle <see cref="PlatformRoles.ReadOnlyAuditor"/>.</summary>
    public const string ReadOnlyAuditorOnly = nameof(ReadOnlyAuditorOnly);

    /// <summary>
    /// Construit le nom de policy à passer à <c>[Authorize(Policy = ...)]</c> pour une
    /// permission fine. Réutilise la convention <c>perm:</c> du
    /// <see cref="PermissionPolicyProvider"/>.
    ///
    /// Exemple : <c>[Authorize(Policy = PlatformPolicies.PolicyFor(PlatformPermissions.TenantsSuspend))]</c>
    /// → policy <c>perm:platform.tenants:suspend</c>.
    /// </summary>
    public static string PolicyFor(string permission) => "perm:" + permission;
}
