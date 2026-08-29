using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Authorization;

/// <summary>
/// Validates that a <see cref="UserRole"/> is applicable to a given <see cref="TenantKind"/>
/// (plan §6 Phase 2.2). Complements — never replaces — <c>ClientPortalStaffRules</c>, which
/// already rejects <see cref="UserRole.Client"/> everywhere regardless of tenant kind.
/// </summary>
/// <remarks>
/// Verified against <c>FirmCollaboratorService.cs</c> and <c>AccountingFirmRegistrationService.cs</c>:
/// accounting-firm tenants only ever assign <see cref="UserRole.FirmManager"/> or
/// <see cref="UserRole.FirmAccountant"/> to staff; standard company roles (SalesRep, Warehouse,
/// Accountant, Administrator, ...) are never used there. Conversely, the two firm roles use a
/// delegated permission catalog (<see cref="DelegatedPermissionCatalog"/>) that only makes sense
/// inside an accounting-firm tenant.
/// </remarks>
public static class TenantRoleCompatibility
{
    private static readonly HashSet<UserRole> FirmOnlyRoles = new()
    {
        UserRole.FirmManager,
        UserRole.FirmAccountant
    };

    /// <summary>
    /// True when <paramref name="role"/> may be assigned to a user of a tenant of kind
    /// <paramref name="tenantKind"/>. Does not concern itself with <see cref="UserRole.Client"/>
    /// (handled separately by <c>ClientPortalStaffRules</c>).
    /// </summary>
    public static bool IsRoleAllowedForTenantKind(UserRole role, TenantKind tenantKind) =>
        tenantKind switch
        {
            TenantKind.AccountingFirm => FirmOnlyRoles.Contains(role),
            TenantKind.Company => !FirmOnlyRoles.Contains(role),
            _ => !FirmOnlyRoles.Contains(role)
        };

    /// <summary>
    /// French, user-facing rejection message for <paramref name="role"/> on a tenant of kind
    /// <paramref name="tenantKind"/>. Only meaningful when
    /// <see cref="IsRoleAllowedForTenantKind"/> returned <c>false</c>.
    /// </summary>
    public static string GetRejectionMessage(UserRole role, TenantKind tenantKind) =>
        tenantKind switch
        {
            TenantKind.AccountingFirm =>
                $"Le rôle {role.ToDisplayString()} n'est pas disponible pour un cabinet comptable. Seuls les rôles cabinet (Gérant de cabinet, Comptable de cabinet) sont autorisés.",
            _ => "Ce rôle est réservé aux cabinets comptables."
        };
}
