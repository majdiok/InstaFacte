using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Authorization;

/// <summary>
/// Extra permission keys allowed on top of <see cref="UserRoleExtensions.GetPermissions"/> when
/// per-user <c>UserModuleGrants</c> exist. Keeps legacy users (no grant rows) unchanged while letting
/// admins extend specific roles with selected modules (e.g. Magasinier + Clients).
/// </summary>
public static class RoleModuleGrantCeilingExtensions
{
    /// <summary>
    /// Adds grant ceiling keys for <paramref name="role"/> for each module marked enabled in <paramref name="moduleGrants"/>.
    /// </summary>
    public static void AddExtensionsForEnabledModules(
        UserRole role,
        IReadOnlyDictionary<AppModule, bool> moduleGrants,
        bool defaultMissingModuleToEnabled,
        HashSet<string> ceiling)
    {
        foreach (var module in Enum.GetValues<AppModule>())
        {
            var enabled = !moduleGrants.TryGetValue(module, out var flag) ? defaultMissingModuleToEnabled : flag;
            if (!enabled)
                continue;

            foreach (var key in GetAdditionalKeysForGrant(role, module))
                ceiling.Add(key);
        }
    }

    /// <summary>
    /// Permission keys that may apply when this role has the module enabled in grants, beyond the base role set.
    /// </summary>
    private static IEnumerable<string> GetAdditionalKeysForGrant(UserRole role, AppModule module) =>
        (role, module) switch
        {
            (UserRole.Warehouse, AppModule.Clients) => module.GetPermissionKeys(),
            _ => Array.Empty<string>()
        };
}
