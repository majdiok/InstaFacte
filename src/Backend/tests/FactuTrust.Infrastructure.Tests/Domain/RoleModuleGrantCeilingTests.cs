using FactuTrust.Domain.Authorization;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

/// <summary>
/// Invariants of <see cref="RoleModuleGrantCeilingExtensions"/> — plan §7.1.A.
/// </summary>
public sealed class RoleModuleGrantCeilingTests
{
    private static readonly UserRole[] ExcludedRoles =
    {
        UserRole.Client, UserRole.FirmManager, UserRole.FirmAccountant
    };

    /// <summary>Roles for which the hybrid ceiling model applies (never empty, may extend the base set).</summary>
    private static readonly UserRole[] ApplicableRoles = Enum.GetValues<UserRole>()
        .Where(r => !ExcludedRoles.Contains(r))
        .ToArray();

    private static IReadOnlySet<string> BaseInModule(UserRole role, AppModule module)
    {
        var basePermissions = new HashSet<string>(role.GetPermissions());
        basePermissions.IntersectWith(module.GetModulePermissionUniverse());
        return basePermissions;
    }

    public static IEnumerable<object[]> ApplicableRoleModulePairs()
    {
        foreach (var role in ApplicableRoles)
        foreach (var module in AppModuleExtensions.AllValues)
            yield return new object[] { role, module };
    }

    [Theory]
    [MemberData(nameof(ApplicableRoleModulePairs))]
    public void GetGrantCeiling_is_always_a_superset_of_base_intersected_with_module_universe(UserRole role, AppModule module)
    {
        var ceiling = RoleModuleGrantCeilingExtensions.GetGrantCeiling(role, module);
        var baseInModule = BaseInModule(role, module);

        Assert.True(
            baseInModule.All(ceiling.Contains),
            $"{role}/{module}: ceiling {string.Join(',', ceiling)} does not contain base {string.Join(',', baseInModule)}");
    }

    /// <summary>
    /// Every applicable role must have at least one module where the ceiling is STRICTLY larger
    /// than base ∩ universe — i.e. the hybrid model must bring a real extension for each role
    /// (plan §3 decision A: "strictement plus large pour chaque rôle tenant applicable").
    /// Administrator is exempted (equality tested separately below — no extension by design).
    /// </summary>
    [Theory]
    [MemberData(nameof(NonAdministratorApplicableRoles))]
    public void Each_non_administrator_applicable_role_has_at_least_one_strict_extension(UserRole role)
    {
        var hasStrictExtension = AppModuleExtensions.AllValues.Any(module =>
        {
            var ceiling = RoleModuleGrantCeilingExtensions.GetGrantCeiling(role, module);
            var baseInModule = BaseInModule(role, module);
            return ceiling.Count > baseInModule.Count;
        });

        Assert.True(hasStrictExtension, $"{role}: no module extends beyond base ∩ universe.");
    }

    public static IEnumerable<object[]> NonAdministratorApplicableRoles() =>
        ApplicableRoles.Where(r => r != UserRole.Administrator).Select(r => new object[] { r });

    [Theory]
    [MemberData(nameof(AllModules))]
    public void Administrator_ceiling_equals_base_intersected_with_universe_no_extension(AppModule module)
    {
        var ceiling = RoleModuleGrantCeilingExtensions.GetGrantCeiling(UserRole.Administrator, module);
        var baseInModule = BaseInModule(UserRole.Administrator, module);

        Assert.Equal(baseInModule, ceiling);
    }

    public static IEnumerable<object[]> AllModules() =>
        AppModuleExtensions.AllValues.Select(m => new object[] { m });

    [Theory]
    [MemberData(nameof(ExcludedRoleModulePairs))]
    public void Excluded_roles_always_get_the_empty_ceiling_never_base_never_exception(UserRole role, AppModule module)
    {
        var ceiling = RoleModuleGrantCeilingExtensions.GetGrantCeiling(role, module);
        Assert.Empty(ceiling);
    }

    public static IEnumerable<object[]> ExcludedRoleModulePairs()
    {
        foreach (var role in ExcludedRoles)
        foreach (var module in AppModuleExtensions.AllValues)
            yield return new object[] { role, module };
    }

    [Fact]
    public void IsExcludedFromModuleGrants_matches_the_documented_excluded_role_set()
    {
        foreach (var role in Enum.GetValues<UserRole>())
        {
            var expected = ExcludedRoles.Contains(role);
            Assert.Equal(expected, RoleModuleGrantCeilingExtensions.IsExcludedFromModuleGrants(role));
        }
    }

    /// <summary>
    /// No non-admin role's grant ceiling DELTA (ceiling \ base) may ever contain a users:*
    /// permission, settings:read/update, accounting:close, or any "*:delete" key — except the
    /// single allow-listed historic exception (Warehouse × Clients, which legitimately carries
    /// clients:delete for non-regression, see <see cref="RoleModuleGrantCeilingExtensions.IsHistoricWarehouseClientsException"/>).
    /// </summary>
    [Theory]
    [MemberData(nameof(NonAdministratorRoleModulePairs))]
    public void ForbiddenCeilingDelta_never_leaks_destructive_or_administrative_keys(UserRole role, AppModule module)
    {
        var ceiling = RoleModuleGrantCeilingExtensions.GetGrantCeiling(role, module);
        var basePermissions = new HashSet<string>(role.GetPermissions());
        var delta = ceiling.Where(k => !basePermissions.Contains(k)).ToList();

        if (RoleModuleGrantCeilingExtensions.IsHistoricWarehouseClientsException(role, module))
        {
            // Allow-listed: production behavior preserved on purpose (contains clients:delete).
            return;
        }

        foreach (var key in delta)
        {
            Assert.False(
                RoleModuleGrantCeilingExtensions.IsForbiddenCeilingDeltaKey(key),
                $"{role}/{module}: delta leaks forbidden key '{key}'.");
        }
    }

    public static IEnumerable<object[]> NonAdministratorRoleModulePairs()
    {
        foreach (var role in Enum.GetValues<UserRole>().Where(r => r != UserRole.Administrator))
        foreach (var module in AppModuleExtensions.AllValues)
            yield return new object[] { role, module };
    }

    [Fact]
    public void GetForbiddenCeilingDelta_contains_the_expected_explicit_keys()
    {
        var forbidden = RoleModuleGrantCeilingExtensions.GetForbiddenCeilingDelta();
        Assert.Contains(Permissions.Users.Create, forbidden);
        Assert.Contains(Permissions.Users.Read, forbidden);
        Assert.Contains(Permissions.Users.Update, forbidden);
        Assert.Contains(Permissions.Users.Delete, forbidden);
        Assert.Contains(Permissions.Settings.Read, forbidden);
        Assert.Contains(Permissions.Settings.Update, forbidden);
        Assert.Contains(Permissions.Accounting.Close, forbidden);
    }

    [Theory]
    [InlineData("clients:delete")]
    [InlineData("invoices:delete")]
    [InlineData("stock:delete")]
    public void IsForbiddenCeilingDeltaKey_flags_any_delete_suffixed_key(string key)
    {
        Assert.True(RoleModuleGrantCeilingExtensions.IsForbiddenCeilingDeltaKey(key));
    }

    [Fact]
    public void IsForbiddenCeilingDeltaKey_does_not_flag_ordinary_read_or_create_keys()
    {
        Assert.False(RoleModuleGrantCeilingExtensions.IsForbiddenCeilingDeltaKey(Permissions.Invoices.Read));
        Assert.False(RoleModuleGrantCeilingExtensions.IsForbiddenCeilingDeltaKey(Permissions.Clients.Create));
    }

    [Fact]
    public void Only_the_historic_Warehouse_Clients_pair_leaks_a_delete_key_in_any_non_admin_delta()
    {
        var offenders = new List<(UserRole Role, AppModule Module, string Key)>();

        foreach (var role in Enum.GetValues<UserRole>().Where(r => r != UserRole.Administrator))
        {
            var basePermissions = new HashSet<string>(role.GetPermissions());
            foreach (var module in AppModuleExtensions.AllValues)
            {
                if (RoleModuleGrantCeilingExtensions.IsHistoricWarehouseClientsException(role, module))
                    continue;

                var ceiling = RoleModuleGrantCeilingExtensions.GetGrantCeiling(role, module);
                foreach (var key in ceiling.Where(k => !basePermissions.Contains(k)))
                {
                    if (key.EndsWith(":delete", StringComparison.Ordinal))
                        offenders.Add((role, module, key));
                }
            }
        }

        Assert.Empty(offenders);
    }

    [Fact]
    public void Treasury_module_universe_includes_forecast_view_and_manage_keys()
    {
        var universe = AppModule.Treasury.GetModulePermissionUniverse();
        Assert.Contains(Permissions.TreasuryForecast.View, universe);
        Assert.Contains(Permissions.TreasuryForecast.Manage, universe);
        // These are NOT in the flat module key list (latent bug fixed by plan §5.2) —
        // only reachable via the forecast_read/forecast_manage features.
        Assert.DoesNotContain(Permissions.TreasuryForecast.View, AppModule.Treasury.GetPermissionKeys());
    }

    [Theory]
    [MemberData(nameof(AllModules))]
    public void GetPermissionKeys_is_always_a_subset_of_GetModulePermissionUniverse(AppModule module)
    {
        var keys = module.GetPermissionKeys();
        var universe = module.GetModulePermissionUniverse();
        Assert.True(keys.All(universe.Contains), $"{module}: GetPermissionKeys not fully covered by universe.");
    }
}
