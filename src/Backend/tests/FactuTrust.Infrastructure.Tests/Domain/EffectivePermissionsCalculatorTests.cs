using FactuTrust.Domain.Authorization;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

public sealed class EffectivePermissionsCalculatorTests
{
    [Fact]
    public void No_grants_returns_full_role_permissions()
    {
        var set = EffectivePermissionsCalculator.Compute(UserRole.Accountant, null);
        Assert.Contains(Permissions.Clients.Read, set);
        Assert.Contains(Permissions.Invoices.Create, set);
        Assert.DoesNotContain(Permissions.Users.Read, set);
    }

    [Fact]
    public void Empty_grant_dict_returns_full_role_permissions()
    {
        var set = EffectivePermissionsCalculator.Compute(UserRole.Accountant, new Dictionary<AppModule, bool>());
        Assert.Contains(Permissions.Clients.Read, set);
    }

    [Fact]
    public void Disabling_clients_module_removes_client_permissions_for_accountant()
    {
        var grants = new Dictionary<AppModule, bool>
        {
            [AppModule.Clients] = false,
            [AppModule.Products] = true,
            [AppModule.Sales] = true,
            [AppModule.Treasury] = true,
            [AppModule.Reports] = true,
            [AppModule.Administration] = true,
            [AppModule.Purchases] = true,
            [AppModule.Stock] = true
        };
        var set = EffectivePermissionsCalculator.Compute(UserRole.Accountant, grants);
        Assert.DoesNotContain(Permissions.Clients.Read, set);
        Assert.Contains(Permissions.Invoices.Read, set);
    }

    // Fixed as part of this remediation (was pre-existing broken/misnamed: the body called
    // UserRole.Client while the name said "Administrator", and asserted Permissions.Invoices.Read —
    // Client's base set has never contained that generic key, so the test was already red at base
    // commit 5152d84). Corrected to assert the actual invariant it described: a role can never
    // exceed its own ceiling even with every module toggled enabled. Client is an excluded role
    // (§5.3) whose effective set stays exactly its base Portal.* permissions.
    [Fact]
    public void Administrator_cannot_exceed_role_even_if_all_modules_enabled()
    {
        var grants = Enum.GetValues<AppModule>().ToDictionary(m => m, _ => true);
        var set = EffectivePermissionsCalculator.Compute(UserRole.Administrator, grants);
        var baseSet = UserRole.Administrator.GetPermissions().ToHashSet();
        Assert.True(set.SetEquals(baseSet));
        Assert.Contains(Permissions.Clients.Create, set);
    }

    [Fact]
    public void Client_is_excluded_role_and_keeps_only_portal_base_under_full_grants()
    {
        var grants = Enum.GetValues<AppModule>().ToDictionary(m => m, _ => true);
        var set = EffectivePermissionsCalculator.Compute(UserRole.Client, grants);
        var baseSet = UserRole.Client.GetPermissions().ToHashSet();
        Assert.True(set.SetEquals(baseSet));
        Assert.Contains(Permissions.Portal.InvoicesRead, set);
        Assert.DoesNotContain(Permissions.Invoices.Read, set);
        Assert.DoesNotContain(Permissions.Clients.Create, set);
    }

    [Fact]
    public void FilterToModulesWithEffectivePermissions_drops_clients_when_only_invoices_read()
    {
        var toggled = new List<AppModule> { AppModule.Clients, AppModule.Sales };
        var effective = new HashSet<string> { Permissions.Invoices.Read };
        var visible = AppModuleExtensions.FilterToModulesWithEffectivePermissions(toggled, effective);
        Assert.Contains(AppModule.Sales, visible);
        Assert.DoesNotContain(AppModule.Clients, visible);
    }

    // Fixed as part of this remediation (was pre-existing broken: asserted AppModule.Sales and
    // AppModule.Treasury ARE visible for UserRole.Client, but Client's Portal.* base set never maps
    // to any module's generic keys — Client is an excluded role whose effective set stays Portal.*
    // only, so no module can ever be visible). Corrected to the real invariant: the excluded Client
    // role shows no modules on the visible list, however many modules are toggle-enabled.
    [Fact]
    public void Client_all_modules_on_visible_list_excludes_impossible_modules()
    {
        var grants = Enum.GetValues<AppModule>().ToDictionary(m => m, _ => true);
        var effective = EffectivePermissionsCalculator.Compute(UserRole.Client, grants);
        var toggledOn = AppModuleExtensions.AllValues.ToList();
        var visible = AppModuleExtensions.FilterToModulesWithEffectivePermissions(toggledOn, effective);
        Assert.Empty(visible);
    }

    [Fact]
    public void Sales_sub_features_only_invoices_excludes_quotes_and_delivery_notes()
    {
        var grants = new Dictionary<AppModule, bool>
        {
            [AppModule.Sales] = true,
            [AppModule.Clients] = false,
            [AppModule.Products] = false,
            [AppModule.Treasury] = false,
            [AppModule.Reports] = false,
            [AppModule.Administration] = false,
            [AppModule.Purchases] = false,
            [AppModule.Stock] = false
        };
        var features = new Dictionary<AppModule, IReadOnlyList<string>>
        {
            [AppModule.Sales] = new[] { "invoices" }
        };
        var set = EffectivePermissionsCalculator.Compute(UserRole.Accountant, grants, features);
        Assert.Contains(Permissions.Invoices.Read, set);
        Assert.DoesNotContain(Permissions.DeliveryNotes.Read, set);
        Assert.DoesNotContain(Permissions.Quotes.Read, set);
    }

    [Fact]
    public void Empty_feature_list_for_module_adds_no_permissions_from_that_module()
    {
        var grants = Enum.GetValues<AppModule>().ToDictionary(m => m, _ => false);
        grants[AppModule.Sales] = true;
        var features = new Dictionary<AppModule, IReadOnlyList<string>> { [AppModule.Sales] = Array.Empty<string>() };
        var set = EffectivePermissionsCalculator.Compute(UserRole.Accountant, grants, features);
        Assert.DoesNotContain(Permissions.Quotes.Read, set);
        Assert.DoesNotContain(Permissions.Invoices.Read, set);
        Assert.DoesNotContain(Permissions.DeliveryNotes.Read, set);
    }

    [Fact]
    public void Clients_sub_features_read_only_excludes_create_update_delete()
    {
        var grants = Enum.GetValues<AppModule>().ToDictionary(m => m, _ => false);
        grants[AppModule.Clients] = true;
        var features = new Dictionary<AppModule, IReadOnlyList<string>>
        {
            [AppModule.Clients] = new[] { "read" }
        };
        var set = EffectivePermissionsCalculator.Compute(UserRole.Accountant, grants, features);
        Assert.Contains(Permissions.Clients.Read, set);
        Assert.DoesNotContain(Permissions.Clients.Create, set);
        Assert.DoesNotContain(Permissions.Clients.Update, set);
        Assert.DoesNotContain(Permissions.Clients.Delete, set);
    }

    [Fact]
    public void Purchases_sub_features_suppliers_only()
    {
        var grants = Enum.GetValues<AppModule>().ToDictionary(m => m, _ => false);
        grants[AppModule.Purchases] = true;
        var features = new Dictionary<AppModule, IReadOnlyList<string>>
        {
            [AppModule.Purchases] = new[] { "suppliers" }
        };
        var set = EffectivePermissionsCalculator.Compute(UserRole.Accountant, grants, features);
        Assert.Contains(Permissions.Suppliers.Read, set);
        Assert.DoesNotContain(Permissions.PurchaseOrders.Read, set);
    }

    [Fact]
    public void Purchases_module_without_sub_features_includes_purchase_receipts()
    {
        var grants = Enum.GetValues<AppModule>().ToDictionary(m => m, _ => false);
        grants[AppModule.Purchases] = true;
        var set = EffectivePermissionsCalculator.Compute(UserRole.Accountant, grants);
        // Accountant role ceiling: create/read/update (no delete)
        Assert.Contains(Permissions.PurchaseReceipts.Create, set);
        Assert.Contains(Permissions.PurchaseReceipts.Read, set);
        Assert.Contains(Permissions.PurchaseReceipts.Update, set);
        Assert.DoesNotContain(Permissions.PurchaseReceipts.Delete, set);
        Assert.Contains(Permissions.PurchaseOrders.Read, set);
        Assert.Contains(Permissions.Suppliers.Read, set);
        Assert.Contains(Permissions.SupplierInvoices.Read, set);
    }

    [Fact]
    public void Reports_sub_feature_sales_only_grants_view_and_export()
    {
        var grants = Enum.GetValues<AppModule>().ToDictionary(m => m, _ => false);
        grants[AppModule.Reports] = true;
        var features = new Dictionary<AppModule, IReadOnlyList<string>>
        {
            [AppModule.Reports] = new[] { "sales" }
        };
        var set = EffectivePermissionsCalculator.Compute(UserRole.Administrator, grants, features);
        Assert.Contains(Permissions.Reports.View, set);
        Assert.Contains(Permissions.Reports.Export, set);
        // reports:view, reports:export, PLUS storefront:manage — the latter has no owning
        // AppModule at all (AppModule.cs has no Storefront value), so per the module-mapped-key
        // fix it always survives regardless of which module/feature is toggled (never governed
        // by any checkbox in the first place; see AppModuleExtensions.GetAllModulesPermissionUniverse).
        Assert.Equal(3, set.Count);
    }

    [Fact]
    public void Reports_unknown_feature_key_ignored()
    {
        var grants = Enum.GetValues<AppModule>().ToDictionary(m => m, _ => false);
        grants[AppModule.Reports] = true;
        var features = new Dictionary<AppModule, IReadOnlyList<string>>
        {
            [AppModule.Reports] = new[] { "nonexistent_report_type" }
        };
        var set = EffectivePermissionsCalculator.Compute(UserRole.Administrator, grants, features);
        // Unknown feature key yields no Reports-specific permissions; storefront:manage still
        // rides along unconditionally (unmapped to any module — see comment above).
        Assert.Equal(new HashSet<string> { Permissions.Storefront.Manage }, set);
    }

    [Fact]
    public void SalesRep_has_sales_permissions_but_no_admin_or_purchases()
    {
        var grants = Enum.GetValues<AppModule>().ToDictionary(m => m, _ => true);
        var set = EffectivePermissionsCalculator.Compute(UserRole.SalesRep, grants);
        Assert.Contains(Permissions.Invoices.Create, set);
        Assert.Contains(Permissions.Invoices.Update, set);
        Assert.Contains(Permissions.Invoices.Send, set);
        Assert.Contains(Permissions.Payments.Read, set);
        Assert.Contains(Permissions.Clients.Create, set);
        Assert.Contains(Permissions.CRM.Read, set);
        Assert.Contains(Permissions.CRM.Create, set);
        Assert.Contains(Permissions.SalesTargets.Read, set);
        Assert.Contains(Permissions.Reports.SalesOwn, set);
        Assert.DoesNotContain(Permissions.Invoices.Sign, set);
        Assert.DoesNotContain(Permissions.Invoices.Delete, set);
        Assert.DoesNotContain(Permissions.CRM.Delete, set);
        Assert.DoesNotContain(Permissions.SalesTargets.Manage, set);
        Assert.DoesNotContain(Permissions.Suppliers.Read, set);
        Assert.DoesNotContain(Permissions.Reports.View, set);
    }

    [Fact]
    public void SalesManager_has_reports_and_delete_rights_unlike_SalesRep()
    {
        var grants = Enum.GetValues<AppModule>().ToDictionary(m => m, _ => true);
        var set = EffectivePermissionsCalculator.Compute(UserRole.SalesManager, grants);
        Assert.Contains(Permissions.Invoices.Delete, set);
        Assert.Contains(Permissions.Reports.View, set);
        Assert.Contains(Permissions.CRM.Read, set);
        Assert.Contains(Permissions.CRM.Delete, set);
        Assert.Contains(Permissions.SalesTargets.Manage, set);
        Assert.Contains(Permissions.Reports.SalesOwn, set);
        Assert.DoesNotContain(Permissions.Suppliers.Read, set);
    }

    [Fact]
    public void Warehouse_with_all_module_grants_has_stock_and_clients_but_no_sales_invoices()
    {
        var grants = Enum.GetValues<AppModule>().ToDictionary(m => m, _ => true);
        var set = EffectivePermissionsCalculator.Compute(UserRole.Warehouse, grants);
        Assert.Contains(Permissions.Stock.Update, set);
        Assert.Contains(Permissions.Inventory.Create, set);
        Assert.Contains(Permissions.Clients.Read, set);
        Assert.DoesNotContain(Permissions.Invoices.Read, set);
    }

    [Fact]
    public void Warehouse_grants_clients_read_only_gets_clients_read_not_manage()
    {
        var grants = Enum.GetValues<AppModule>().ToDictionary(m => m, _ => false);
        grants[AppModule.Clients] = true;
        grants[AppModule.Products] = true;
        grants[AppModule.Stock] = true;
        var features = new Dictionary<AppModule, IReadOnlyList<string>>
        {
            [AppModule.Clients] = new[] { "read" }
        };
        var set = EffectivePermissionsCalculator.Compute(UserRole.Warehouse, grants, features);
        Assert.Contains(Permissions.Clients.Read, set);
        Assert.DoesNotContain(Permissions.Clients.Create, set);
        Assert.Contains(Permissions.Stock.Read, set);
    }

    [Fact]
    public void Warehouse_no_grants_still_has_no_clients_permissions()
    {
        var set = EffectivePermissionsCalculator.Compute(UserRole.Warehouse, null);
        Assert.DoesNotContain(Permissions.Clients.Read, set);
        Assert.Contains(Permissions.Stock.Read, set);
    }

    [Fact]
    public void Purchaser_has_suppliers_and_orders_but_no_sales()
    {
        var grants = Enum.GetValues<AppModule>().ToDictionary(m => m, _ => true);
        var set = EffectivePermissionsCalculator.Compute(UserRole.Purchaser, grants);
        Assert.Contains(Permissions.PurchaseOrders.Create, set);
        Assert.Contains(Permissions.Suppliers.Update, set);
        Assert.DoesNotContain(Permissions.Invoices.Create, set);
    }

    [Fact]
    public void Cashier_has_pos_relevant_permissions_only()
    {
        var grants = Enum.GetValues<AppModule>().ToDictionary(m => m, _ => true);
        var set = EffectivePermissionsCalculator.Compute(UserRole.Cashier, grants);
        Assert.Contains(Permissions.Invoices.Create, set);
        Assert.Contains(Permissions.Payments.Create, set);
        Assert.DoesNotContain(Permissions.Invoices.Update, set);
        Assert.DoesNotContain(Permissions.Reports.View, set);
    }

    [Fact]
    public void Auditor_has_only_read_permissions_across_all()
    {
        var grants = Enum.GetValues<AppModule>().ToDictionary(m => m, _ => true);
        var set = EffectivePermissionsCalculator.Compute(UserRole.Auditor, grants);
        Assert.Contains(Permissions.Invoices.Read, set);
        Assert.Contains(Permissions.Reports.View, set);
        Assert.Contains(Permissions.Stock.Read, set);
        Assert.DoesNotContain(Permissions.Invoices.Create, set);
        Assert.DoesNotContain(Permissions.Stock.Update, set);
        // Ensure no write permissions exist
        Assert.DoesNotContain(set, p => p.EndsWith(":create") || p.EndsWith(":update") || p.EndsWith(":delete"));
    }

    [Fact]
    public void Supervisor_has_full_access_except_user_management()
    {
        var grants = Enum.GetValues<AppModule>().ToDictionary(m => m, _ => true);
        var set = EffectivePermissionsCalculator.Compute(UserRole.Supervisor, grants);
        Assert.Contains(Permissions.Invoices.Delete, set);
        Assert.Contains(Permissions.Stock.Delete, set);
        Assert.DoesNotContain(Permissions.Users.Create, set);
        Assert.Contains(Permissions.Settings.Read, set);
        Assert.Contains(Permissions.Settings.Update, set);
    }

    [Fact]
    public void Developer_no_grants_has_studio_design_and_custom_data_but_no_business_writes()
    {
        var set = EffectivePermissionsCalculator.Compute(UserRole.Developer, null);
        // Design-time + runtime custom-data permissions.
        Assert.Contains(Permissions.Studio.DesignEntities, set);
        Assert.Contains(Permissions.Studio.DesignForms, set);
        Assert.Contains(Permissions.Studio.DesignReports, set);
        Assert.Contains(Permissions.CustomData.RecordsRead, set);
        Assert.Contains(Permissions.CustomData.RecordsWrite, set);
        Assert.Contains(Permissions.CustomData.ReportsView, set);
        // Read-only access to whitelisted existing sources for building reports.
        Assert.Contains(Permissions.Reports.View, set);
        Assert.Contains(Permissions.Clients.Read, set);
        Assert.Contains(Permissions.Products.Read, set);
        Assert.Contains(Permissions.Invoices.Read, set);
        // No write access to existing business data, no user management.
        Assert.DoesNotContain(Permissions.Clients.Create, set);
        Assert.DoesNotContain(Permissions.Invoices.Create, set);
        Assert.DoesNotContain(Permissions.Users.Read, set);
    }

    [Fact]
    public void Disabling_studio_module_removes_studio_permissions_for_developer()
    {
        // §3.3 regression guard: studio/custom keys must be mapped to AppModule.Studio,
        // otherwise the role ceiling ∩ enabled-modules intersection would drop them for grant-scoped users.
        var grants = Enum.GetValues<AppModule>().ToDictionary(m => m, _ => true);
        grants[AppModule.Studio] = false;
        var set = EffectivePermissionsCalculator.Compute(UserRole.Developer, grants);
        Assert.DoesNotContain(Permissions.Studio.DesignEntities, set);
        Assert.DoesNotContain(Permissions.CustomData.RecordsRead, set);
        // Read-only whitelisted sources still resolve via their own modules.
        Assert.Contains(Permissions.Clients.Read, set);
        Assert.Contains(Permissions.Reports.View, set);
    }

    [Fact]
    public void Developer_with_only_studio_module_enabled_keeps_studio_permissions()
    {
        var grants = Enum.GetValues<AppModule>().ToDictionary(m => m, _ => false);
        grants[AppModule.Studio] = true;
        var set = EffectivePermissionsCalculator.Compute(UserRole.Developer, grants);
        Assert.Contains(Permissions.Studio.DesignEntities, set);
        Assert.Contains(Permissions.CustomData.RecordsRead, set);
        Assert.Contains(Permissions.CustomData.RecordsWrite, set);
        Assert.Contains(Permissions.CustomData.ReportsView, set);
        // Whitelisted read-only sources are gated by their own (disabled) modules here.
        Assert.DoesNotContain(Permissions.Clients.Read, set);
    }

    [Fact]
    public void Studio_module_maps_all_six_studio_and_custom_keys()
    {
        var grants = Enum.GetValues<AppModule>().ToDictionary(m => m, _ => false);
        grants[AppModule.Studio] = true;
        var set = EffectivePermissionsCalculator.Compute(UserRole.Administrator, grants);
        Assert.Contains(Permissions.Studio.DesignEntities, set);
        Assert.Contains(Permissions.Studio.DesignForms, set);
        Assert.Contains(Permissions.Studio.DesignReports, set);
        Assert.Contains(Permissions.CustomData.RecordsRead, set);
        Assert.Contains(Permissions.CustomData.RecordsWrite, set);
        Assert.Contains(Permissions.CustomData.ReportsView, set);
        // Plus storefront:manage, unconditionally (unmapped to any AppModule — see comment in
        // Reports_sub_feature_sales_only_grants_view_and_export above).
        Assert.Contains(Permissions.Storefront.Manage, set);
        Assert.Equal(7, set.Count);
    }

    [Fact]
    public void Projects_module_on_grants_administrator_project_keys()
    {
        var grants = Enum.GetValues<AppModule>().ToDictionary(m => m, _ => false);
        grants[AppModule.Projects] = true;
        var set = EffectivePermissionsCalculator.Compute(UserRole.Administrator, grants);
        Assert.Contains(Permissions.Projects.Read, set);
        Assert.Contains(Permissions.Projects.ManageTeam, set);
        Assert.Contains(Permissions.ProjectTasks.Read, set);
        Assert.Contains(Permissions.ProjectTime.Validate, set);
        Assert.Contains(Permissions.ProjectBilling.Create, set);
    }

    [Fact]
    public void Projects_module_off_strips_project_keys()
    {
        var grants = Enum.GetValues<AppModule>().ToDictionary(m => m, _ => true);
        grants[AppModule.Projects] = false;
        var set = EffectivePermissionsCalculator.Compute(UserRole.Administrator, grants);
        Assert.DoesNotContain(Permissions.Projects.Read, set);
        Assert.DoesNotContain(Permissions.ProjectTime.Validate, set);
        Assert.DoesNotContain(Permissions.ProjectBilling.Create, set);
    }

    [Fact]
    public void Projects_sub_features_time_only_excludes_billing()
    {
        var grants = Enum.GetValues<AppModule>().ToDictionary(m => m, _ => false);
        grants[AppModule.Projects] = true;
        var features = new Dictionary<AppModule, IReadOnlyList<string>>
        {
            [AppModule.Projects] = new[] { "time" }
        };
        var set = EffectivePermissionsCalculator.Compute(UserRole.Administrator, grants, features);
        Assert.Contains(Permissions.ProjectTime.Read, set);
        Assert.Contains(Permissions.ProjectTime.Validate, set);
        Assert.DoesNotContain(Permissions.ProjectBilling.Create, set);
        Assert.DoesNotContain(Permissions.Projects.ManageTeam, set);
    }

    // ---------------------------------------------------------------------------------------
    // §7.1.B — Round-trip no-op per role (plan v3, "zéro régression" — decision A / §1.4).
    // The ONLY guarantee this remediation makes for existing behavior is: a user with NO
    // persisted UserModuleGrants rows ("non personnalisé") keeps EXACTLY the same effective
    // permissions as before. Re-saving an ALREADY-customized user with explicit per-module
    // flags is explicitly NOT guaranteed to be a no-op (widened ceilings can add keys — the
    // accepted, census-tracked consequence documented in plan §5.4/§7.2).
    // ---------------------------------------------------------------------------------------

    public static IEnumerable<object[]> AllRoles() =>
        Enum.GetValues<UserRole>().Select(r => new object[] { r });

    [Theory]
    [MemberData(nameof(AllRoles))]
    public void No_grants_at_all_is_a_no_op_round_trip_for_every_role(UserRole role)
    {
        var expected = role.GetPermissions().ToHashSet();
        var actual = EffectivePermissionsCalculator.Compute(role, null);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(AllRoles))]
    public void Empty_grant_dictionary_is_a_no_op_round_trip_for_every_role(UserRole role)
    {
        var expected = role.GetPermissions().ToHashSet();
        var actual = EffectivePermissionsCalculator.Compute(role, new Dictionary<AppModule, bool>());
        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Accountant special case (plan §7.1.B): a single grant row for its own module
    /// (IsEnabled=true, EnabledFeatureKeys=null/absent — "no sub-selection", exactly how the
    /// modal would persist an untouched module) is still a no-op: Accountant's base permissions
    /// not mapped to any Accounting sub-feature (accounting:reverse/import/declare, audit:read)
    /// only survive because the full module universe applies when there is no explicit feature
    /// list. Accountant has no grant-ceiling delta for its own Accounting module, so no
    /// extension is added either.
    /// </summary>
    [Fact]
    public void Accountant_single_module_grant_with_null_feature_keys_is_still_a_no_op()
    {
        var grants = new Dictionary<AppModule, bool> { [AppModule.Accounting] = true };
        var set = EffectivePermissionsCalculator.Compute(UserRole.Accountant, grants);
        var expected = UserRole.Accountant.GetPermissions().ToHashSet();

        Assert.Equal(expected, set);
        // Explicitly confirm the keys that only ride along via the full-universe path.
        Assert.Contains(Permissions.Accounting.Reverse, set);
        Assert.Contains(Permissions.Accounting.Import, set);
        Assert.Contains(Permissions.Accounting.Declare, set);
        Assert.Contains(Permissions.Audit.Read, set);
    }

    /// <summary>
    /// Contrast case (plan §7.1.B, "documenter que la variante liste explicite de features n'est
    /// PAS un no-op"): the SAME module grant, but with an explicit narrow feature subset ("journal"
    /// only), is NOT a no-op for Accountant — accounting:reverse/import/declare and audit:read are
    /// not reachable through the "journal" feature and get dropped. This is expected/documented
    /// behavior, not a bug.
    /// </summary>
    [Fact]
    public void Accountant_single_module_grant_with_explicit_feature_subset_is_not_a_no_op()
    {
        var grants = new Dictionary<AppModule, bool> { [AppModule.Accounting] = true };
        var features = new Dictionary<AppModule, IReadOnlyList<string>>
        {
            [AppModule.Accounting] = new[] { "journal" }
        };
        var set = EffectivePermissionsCalculator.Compute(UserRole.Accountant, grants, features);
        var expected = UserRole.Accountant.GetPermissions().ToHashSet();

        Assert.NotEqual(expected, set);
        Assert.DoesNotContain(Permissions.Accounting.Reverse, set);
        Assert.DoesNotContain(Permissions.Accounting.Import, set);
        Assert.DoesNotContain(Permissions.Accounting.Declare, set);
        Assert.DoesNotContain(Permissions.Audit.Read, set);
        // The journal feature's own keys are still present.
        Assert.Contains(Permissions.Accounting.Read, set);
        Assert.Contains(Permissions.Accounting.Create, set);
    }

    // ---------------------------------------------------------------------------------------
    // §7.1.C — Zero-regression matrix, parameterized over every real (role, module) extension
    // pair (plan §5.3 table). "Extension pair" is derived directly from the production ceiling
    // API (RoleModuleGrantCeilingExtensions.GetGrantCeiling), not re-hardcoded here, so any new
    // pair added to the §5.3 table is automatically covered without touching this file.
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Every (role, module) pair whose grant ceiling is a STRICT superset of the role's base
    /// permissions intersected with the module universe — i.e. every pair where checking the
    /// module in the modal can genuinely widen what the user can do beyond their role's default.
    /// </summary>
    public static IEnumerable<object[]> ExtensionPairs()
    {
        foreach (var role in Enum.GetValues<UserRole>())
        {
            if (RoleModuleGrantCeilingExtensions.IsExcludedFromModuleGrants(role))
                continue;

            foreach (var module in Enum.GetValues<AppModule>())
            {
                var ceiling = RoleModuleGrantCeilingExtensions.GetGrantCeiling(role, module);
                var basePermissions = role.GetPermissions().ToHashSet();
                var delta = ceiling.Except(basePermissions).ToHashSet();
                if (delta.Count > 0)
                    yield return new object[] { role, module };
            }
        }
    }

    /// <summary>
    /// The historic Warehouse×Clients pair uses legacy "absent = enabled" semantics for its own
    /// extension (see RoleModuleGrantCeilingExtensions.AddExtensionsForEnabledModules): whenever
    /// <paramref name="role"/> is Warehouse and the Clients module key is absent from
    /// <paramref name="grants"/> (or explicitly true), its extension delta leaks into the ceiling
    /// regardless of which OTHER module is under test. Returns that delta, or empty otherwise.
    /// </summary>
    private static HashSet<string> AmbientWarehouseClientsExtension(UserRole role, IReadOnlyDictionary<AppModule, bool> grants)
    {
        if (role != UserRole.Warehouse)
            return new HashSet<string>();

        var clientsEnabledForExtension = !grants.TryGetValue(AppModule.Clients, out var flag) || flag;
        if (!clientsEnabledForExtension)
            return new HashSet<string>();

        var basePermissions = role.GetPermissions().ToHashSet();
        return RoleModuleGrantCeilingExtensions.GetGrantCeiling(role, AppModule.Clients).Except(basePermissions).ToHashSet();
    }

    [Theory]
    [MemberData(nameof(ExtensionPairs))]
    public void Extension_pair_absent_from_grants_grants_no_extension(UserRole role, AppModule module)
    {
        var basePermissions = role.GetPermissions().ToHashSet();
        var delta = RoleModuleGrantCeilingExtensions.GetGrantCeiling(role, module).Except(basePermissions).ToHashSet();

        // Non-empty dict (so Compute doesn't short-circuit on "no grants at all"), but the
        // target module key itself is absent — only some unrelated module is present.
        var otherModule = Enum.GetValues<AppModule>().First(m => m != module);
        var grants = new Dictionary<AppModule, bool> { [otherModule] = true };

        var actual = EffectivePermissionsCalculator.Compute(role, grants);
        var ambientHistoricExtension = AmbientWarehouseClientsExtension(role, grants);

        foreach (var extensionKey in delta)
        {
            if (ambientHistoricExtension.Contains(extensionKey))
            {
                // The single documented historic exception (Warehouse×Clients): an ABSENT
                // Clients grant still counts as enabled for its own extension (legacy behavior
                // preserved for non-regression) — so this delta DOES leak in here, unlike every
                // other (role, module) extension pair.
                Assert.Contains(extensionKey, actual);
            }
            else
            {
                Assert.DoesNotContain(extensionKey, actual);
            }
        }
    }

    [Theory]
    [MemberData(nameof(ExtensionPairs))]
    public void Extension_pair_explicitly_disabled_strips_module_and_grants_no_extension(UserRole role, AppModule module)
    {
        var basePermissions = role.GetPermissions().ToHashSet();
        var delta = RoleModuleGrantCeilingExtensions.GetGrantCeiling(role, module).Except(basePermissions).ToHashSet();
        var moduleBaseKeys = basePermissions.Intersect(module.GetPermissionKeys()).ToHashSet();

        var grants = new Dictionary<AppModule, bool> { [module] = false };
        var actual = EffectivePermissionsCalculator.Compute(role, grants);

        foreach (var extensionKey in delta)
            Assert.DoesNotContain(extensionKey, actual);

        // The module's own base permissions are removed too — unless another enabled module's
        // universe (all other modules default to enabled here) happens to also carry the same
        // key, which the historic Warehouse×Clients pair aside, does not occur for any current pair.
        if (!RoleModuleGrantCeilingExtensions.IsHistoricWarehouseClientsException(role, module))
        {
            foreach (var key in moduleBaseKeys)
            {
                var coveredByAnotherModule = Enum.GetValues<AppModule>()
                    .Where(m => m != module)
                    .Any(m => m.GetModulePermissionUniverse().Contains(key));
                if (!coveredByAnotherModule)
                    Assert.DoesNotContain(key, actual);
            }
        }
    }

    [Theory]
    [MemberData(nameof(ExtensionPairs))]
    public void Extension_pair_enabled_with_null_feature_keys_grants_the_full_ceiling_delta(UserRole role, AppModule module)
    {
        var basePermissions = role.GetPermissions().ToHashSet();
        var ceiling = RoleModuleGrantCeilingExtensions.GetGrantCeiling(role, module);
        var delta = ceiling.Except(basePermissions).ToHashSet();

        var grants = new Dictionary<AppModule, bool> { [module] = true };
        var actual = EffectivePermissionsCalculator.Compute(role, grants);
        var ambientHistoricExtension = AmbientWarehouseClientsExtension(role, grants);

        // The whole extension delta rides along with "no sub-selection" (module universe applies).
        foreach (var extensionKey in delta)
            Assert.Contains(extensionKey, actual);

        // Result never exceeds base(role) ∪ ceiling(role, module) ∪ the ambient historic
        // Warehouse×Clients leak (only ever non-empty when role==Warehouse and module!=Clients;
        // see AmbientWarehouseClientsExtension) for this single-module grant.
        var allowed = basePermissions.Union(ceiling).Union(ambientHistoricExtension).ToHashSet();
        Assert.True(actual.IsSubsetOf(allowed), $"{role}/{module}: actual has keys outside base ∪ ceiling: {string.Join(",", actual.Except(allowed))}");

        // Never any forbidden key, except the single documented historic exception (whether it's
        // the pair under test, or leaking ambiently from Clients while some other module is tested).
        foreach (var key in actual)
        {
            if (RoleModuleGrantCeilingExtensions.IsForbiddenCeilingDeltaKey(key) && !basePermissions.Contains(key))
            {
                Assert.True(
                    RoleModuleGrantCeilingExtensions.IsHistoricWarehouseClientsException(role, module) ||
                    ambientHistoricExtension.Contains(key),
                    $"{role}/{module}: forbidden delta key '{key}' leaked outside the documented historic exception.");
            }
        }
    }

    [Theory]
    [MemberData(nameof(ExtensionPairs))]
    public void Extension_pair_enabled_with_empty_explicit_feature_list_drops_all_of_that_module(UserRole role, AppModule module)
    {
        var basePermissions = role.GetPermissions().ToHashSet();
        var ceiling = RoleModuleGrantCeilingExtensions.GetGrantCeiling(role, module);
        var moduleUniverse = module.GetModulePermissionUniverse();

        var grants = new Dictionary<AppModule, bool> { [module] = true };
        var features = new Dictionary<AppModule, IReadOnlyList<string>> { [module] = Array.Empty<string>() };
        var actual = EffectivePermissionsCalculator.Compute(role, grants, features);

        // With an explicit-but-empty feature subset, this module contributes nothing to the
        // union: none of its universe keys (base or extension) survive in the result, unless
        // some OTHER default-enabled module's universe independently also carries the same key.
        foreach (var key in moduleUniverse)
        {
            var coveredByAnotherModule = Enum.GetValues<AppModule>()
                .Where(m => m != module)
                .Any(m => m.GetModulePermissionUniverse().Contains(key));
            if (!coveredByAnotherModule)
                Assert.DoesNotContain(key, actual);
        }

        // Base permissions entirely outside this module's universe are untouched.
        foreach (var key in basePermissions.Except(moduleUniverse))
            Assert.Contains(key, actual);
    }
}
