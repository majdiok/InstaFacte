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

    [Fact]
    public void Administrator_cannot_exceed_role_even_if_all_modules_enabled()
    {
        var grants = Enum.GetValues<AppModule>().ToDictionary(m => m, _ => true);
        var set = EffectivePermissionsCalculator.Compute(UserRole.Client, grants);
        Assert.Contains(Permissions.Invoices.Read, set);
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

    [Fact]
    public void Client_all_modules_on_visible_list_excludes_impossible_modules()
    {
        var grants = Enum.GetValues<AppModule>().ToDictionary(m => m, _ => true);
        var effective = EffectivePermissionsCalculator.Compute(UserRole.Client, grants);
        var toggledOn = AppModuleExtensions.AllValues.ToList();
        var visible = AppModuleExtensions.FilterToModulesWithEffectivePermissions(toggledOn, effective);
        Assert.Contains(AppModule.Sales, visible);
        Assert.Contains(AppModule.Treasury, visible);
        Assert.DoesNotContain(AppModule.Administration, visible);
        Assert.DoesNotContain(AppModule.Clients, visible);
        Assert.DoesNotContain(AppModule.Products, visible);
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
        // Only 2 permissions should be in the set (reports:view and reports:export)
        Assert.Equal(2, set.Count);
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
        // Unknown feature key yields no permissions
        Assert.Empty(set);
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
        Assert.Equal(6, set.Count);
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
}
