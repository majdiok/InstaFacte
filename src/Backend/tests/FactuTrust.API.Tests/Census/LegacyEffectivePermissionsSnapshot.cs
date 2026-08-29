using FactuTrust.Domain.Authorization;
using FactuTrust.Domain.Enums;

namespace FactuTrust.API.Tests;

/// <summary>
/// Frozen, byte-for-byte-equivalent COPY of the permission-computation logic as it existed at
/// base commit <c>5152d84</c> — i.e. immediately BEFORE this remediation plan's §6/§5.3 changes
/// to <see cref="EffectivePermissionsCalculator"/> and <see cref="RoleModuleGrantCeilingExtensions"/>.
///
/// Used exclusively by the §7.2.1 census utility (<see cref="CensusOfPermissionChangesTests"/>) to
/// compute the "before" side of the delta against the CURRENT (post-plan) calculator, so that
/// pre-deployment admins can be told exactly which users gain which permission keys. This class is
/// intentionally NOT shared with production code: it must stay pinned to the pre-plan snapshot even
/// if the current calculator changes again later, and it must never be reachable from any
/// non-test/non-tooling code path.
///
/// Provenance: <c>git show 5152d84:src/Backend/FactuTrust.Domain/Authorization/EffectivePermissionsCalculator.cs</c>
/// and the equivalent for <c>RoleModuleGrantCeilingExtensions.cs</c>. Deliberately reuses the CURRENT,
/// unchanged <see cref="UserRoleExtensions.GetPermissions"/>, <see cref="AppModuleExtensions.GetPermissionKeys"/>
/// and <see cref="ModuleFeatureCatalog"/> (their DATA did shift slightly as part of this same remediation —
/// e.g. three Payroll permission keys added to <c>GetPermissionKeys(Payroll)</c> as a bug fix — and that
/// shift is a genuine, intended behavior change the census should report, not something to mask).
/// </summary>
internal static class LegacyEffectivePermissionsCalculator
{
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
                // Pre-plan behavior: only the flat module key list, NOT the full
                // module-plus-features universe (GetModulePermissionUniverse did not exist yet).
                foreach (var p in module.GetPermissionKeys())
                    unionFromModules.Add(p);
            }
        }

        var permissionCeiling = new HashSet<string>(roleSet);
        LegacyRoleModuleGrantCeilingExtensions.AddExtensionsForEnabledModules(
            role,
            moduleGrants,
            defaultMissingModuleToEnabled,
            permissionCeiling);

        unionFromModules.IntersectWith(permissionCeiling);
        return unionFromModules;
    }
}

/// <summary>
/// Frozen pre-plan copy of <see cref="RoleModuleGrantCeilingExtensions"/> (see
/// <see cref="LegacyEffectivePermissionsCalculator"/> for provenance/rationale). Before this
/// remediation, the only grant-ceiling extension in the whole table was the historic
/// (Warehouse, Clients) exception; there was no excluded-role short-circuit and no forbidden-delta
/// concept.
/// </summary>
internal static class LegacyRoleModuleGrantCeilingExtensions
{
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

    private static IEnumerable<string> GetAdditionalKeysForGrant(UserRole role, AppModule module) =>
        (role, module) switch
        {
            (UserRole.Warehouse, AppModule.Clients) => module.GetPermissionKeys(),
            _ => Array.Empty<string>()
        };
}
