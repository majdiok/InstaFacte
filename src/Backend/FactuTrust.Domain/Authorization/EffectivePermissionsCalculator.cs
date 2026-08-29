using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Authorization;

/// <summary>
/// Pure functions for effective permission sets: role ceiling ∩ union(enabled module permissions).
/// </summary>
public static class EffectivePermissionsCalculator
{
    /// <param name="moduleGrants">When null or empty, user has no stored grants → full role permissions (backward compatibility).</param>
    /// <param name="defaultMissingModuleToEnabled">When true, a module absent from <paramref name="moduleGrants"/> counts as enabled.</param>
    public static HashSet<string> Compute(
        UserRole role,
        IReadOnlyDictionary<AppModule, bool>? moduleGrants,
        bool defaultMissingModuleToEnabled = true) =>
        Compute(role, moduleGrants, null, defaultMissingModuleToEnabled);

    /// <param name="featureKeysByModule">
    /// When a module is enabled: if this dictionary contains an entry for that module, only permissions from those feature keys are included
    /// (empty list = none). If there is no entry, all permissions of the module apply. Null dictionary means no sub-selection anywhere.
    /// </param>
    public static HashSet<string> Compute(
        UserRole role,
        IReadOnlyDictionary<AppModule, bool>? moduleGrants,
        IReadOnlyDictionary<AppModule, IReadOnlyList<string>>? featureKeysByModule,
        bool defaultMissingModuleToEnabled = true)
    {
        var roleSet = role.GetPermissions().ToHashSet();

        if (moduleGrants is null || moduleGrants.Count == 0)
            return roleSet;

        var unionFromModules = new HashSet<string>();
        foreach (var module in Enum.GetValues<AppModule>())
        {
            var enabled = !moduleGrants.TryGetValue(module, out var flag) ? defaultMissingModuleToEnabled : flag;
            if (!enabled)
                continue;

            var useSubFeatures = false;
            IReadOnlyList<string>? featureKeys = null;
            if (featureKeysByModule is not null &&
                featureKeysByModule.TryGetValue(module, out var k))
            {
                useSubFeatures = true;
                featureKeys = k;
            }

            if (useSubFeatures)
            {
                ModuleFeatureCatalog.AddPermissionsForFeatures(module, featureKeys ?? Array.Empty<string>(), unionFromModules);
            }
            else
            {
                // No sub-selection: the full module universe applies (module keys + every
                // feature's permissions), not just GetPermissionKeys() — see AppModule.GetModulePermissionUniverse.
                foreach (var p in module.GetModulePermissionUniverse())
                    unionFromModules.Add(p);
            }
        }

        // Permissions genuinely outside the module-grant system altogether (no AppModule's universe
        // contains them, e.g. Permissions.Storefront.Manage) are never governed by any per-module
        // checkbox — module customization must never silently revoke them (plan §5.2/§7.2.1: a
        // customization delta of REMOVAL is a bug, only additions are the accepted consequence).
        var moduleMappedKeys = AppModuleExtensions.GetAllModulesPermissionUniverse();
        foreach (var permission in roleSet)
        {
            if (!moduleMappedKeys.Contains(permission))
                unionFromModules.Add(permission);
        }

        var permissionCeiling = new HashSet<string>(roleSet);
        RoleModuleGrantCeilingExtensions.AddExtensionsForEnabledModules(
            role,
            moduleGrants,
            defaultMissingModuleToEnabled,
            permissionCeiling);

        unionFromModules.IntersectWith(permissionCeiling);
        return unionFromModules;
    }
}
