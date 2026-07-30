namespace FactuTrust.Domain.Enums;

/// <summary>
/// Functional modules for per-user access control. Maps to subsets of <see cref="Permissions"/>.
/// </summary>
public enum AppModule
{
    Clients = 0,
    Products = 1,
    Sales = 2,
    Treasury = 3,
    Reports = 4,
    Administration = 5,
    Purchases = 6,
    Stock = 7,
    Accounting = 8,
    CRM = 9,
    Fiscal = 10,
    AI = 11,
    Forecasting = 12,
    Studio = 13,
    Payroll = 14
}

public static class AppModuleExtensions
{
    public static string ToDisplayString(this AppModule module) => module switch
    {
        AppModule.Clients => "Clients",
        AppModule.Products => "Produits et services",
        AppModule.Sales => "Ventes (factures)",
        AppModule.Treasury => "Trésorerie (paiements)",
        AppModule.Reports => "Rapports",
        AppModule.Administration => "Paramètres et utilisateurs",
        AppModule.Purchases => "Achats",
        AppModule.Stock => "Stock",
        AppModule.Accounting => "Comptabilité",
        AppModule.CRM => "CRM Commercial",
        AppModule.Fiscal => "Fiscal / TEJ",
        AppModule.AI => "Assistant IA",
        AppModule.Forecasting => "Prévisions IA",
        AppModule.Studio => "Studio (low-code)",
        AppModule.Payroll => "RH & Paie",
        _ => throw new ArgumentOutOfRangeException(nameof(module))
    };

    /// <summary>
    /// All permissions that belong to this module (before role intersection).
    /// </summary>
    public static IReadOnlyList<string> GetPermissionKeys(this AppModule module) => module switch
    {
        AppModule.Clients => new[]
        {
            Permissions.Clients.Create,
            Permissions.Clients.Read,
            Permissions.Clients.Update,
            Permissions.Clients.Delete
        },
        AppModule.Products => new[]
        {
            Permissions.Products.Create,
            Permissions.Products.Read,
            Permissions.Products.Update,
            Permissions.Products.Delete
        },
        AppModule.Sales => new[]
        {
            Permissions.SalesOrders.Create,
            Permissions.SalesOrders.Read,
            Permissions.SalesOrders.Update,
            Permissions.SalesOrders.Delete,
            Permissions.Quotes.Create,
            Permissions.Quotes.Read,
            Permissions.Quotes.Update,
            Permissions.Quotes.Delete,
            Permissions.DeliveryNotes.Create,
            Permissions.DeliveryNotes.Read,
            Permissions.DeliveryNotes.Update,
            Permissions.DeliveryNotes.Delete,
            Permissions.Invoices.Create,
            Permissions.Invoices.Read,
            Permissions.Invoices.Update,
            Permissions.Invoices.Delete,
            Permissions.Invoices.Sign,
            Permissions.Invoices.Send,
            Permissions.Pricing.Create,
            Permissions.Pricing.Read,
            Permissions.Pricing.Update,
            Permissions.Pricing.Delete
        },
        AppModule.Treasury => new[]
        {
            Permissions.Payments.Create,
            Permissions.Payments.Read,
            Permissions.Payments.Update
        },
        AppModule.Reports => new[]
        {
            Permissions.Reports.View,
            Permissions.Reports.Export
        },
        AppModule.Administration => new[]
        {
            Permissions.Users.Create,
            Permissions.Users.Read,
            Permissions.Users.Update,
            Permissions.Users.Delete,
            Permissions.Settings.Read,
            Permissions.Settings.Update
        },
        AppModule.Purchases => new[]
        {
            Permissions.Suppliers.Create,
            Permissions.Suppliers.Read,
            Permissions.Suppliers.Update,
            Permissions.Suppliers.Delete,
            Permissions.PurchaseOrders.Create,
            Permissions.PurchaseOrders.Read,
            Permissions.PurchaseOrders.Update,
            Permissions.PurchaseOrders.Delete,
            Permissions.SupplierInvoices.Create,
            Permissions.SupplierInvoices.Read,
            Permissions.SupplierInvoices.Update,
            Permissions.SupplierInvoices.Delete
        },
        AppModule.Stock => new[]
        {
            Permissions.Stock.Create,
            Permissions.Stock.Read,
            Permissions.Stock.Update,
            Permissions.Stock.Delete,
            Permissions.StockTransfers.Create,
            Permissions.StockTransfers.Read,
            Permissions.StockTransfers.Update,
            Permissions.StockTransfers.Delete,
            Permissions.Inventory.Create,
            Permissions.Inventory.Read,
            Permissions.Inventory.Update,
            Permissions.Inventory.Delete
        },
        AppModule.Accounting => new[]
        {
            Permissions.Accounting.Read,
            Permissions.Accounting.Create,
            Permissions.Accounting.Close,
            Permissions.Accounting.Validate,
            Permissions.Accounting.Reverse,
            Permissions.Accounting.Import,
            Permissions.Accounting.Declare,
            Permissions.Audit.Read
        },
        AppModule.CRM => new[]
        {
            Permissions.CRM.Read,
            Permissions.CRM.Create,
            Permissions.CRM.Update,
            Permissions.CRM.Delete,
            Permissions.SalesTargets.Read,
            Permissions.SalesTargets.Manage,
            Permissions.Reports.SalesOwn
        },
        AppModule.Fiscal => new[]
        {
            Permissions.WithholdingTax.Read,
            Permissions.WithholdingTax.Create,
            Permissions.WithholdingTax.Edit,
            Permissions.WithholdingTax.Validate,
            Permissions.WithholdingTax.Delete,
            Permissions.WithholdingTax.Export
        },
        AppModule.AI => new[]
        {
            Permissions.AI.Chat
        },
        AppModule.Forecasting => new[]
        {
            Permissions.Forecasting.View,
            Permissions.Forecasting.Manage
        },
        AppModule.Studio => new[]
        {
            Permissions.Studio.DesignEntities,
            Permissions.Studio.DesignForms,
            Permissions.Studio.DesignReports,
            Permissions.CustomData.RecordsRead,
            Permissions.CustomData.RecordsWrite,
            Permissions.CustomData.ReportsView
        },
        AppModule.Payroll => new[]
        {
            Permissions.Payroll.Read,
            Permissions.Payroll.ManageEmployees,
            Permissions.Payroll.RunPayroll,
            Permissions.Payroll.Validate,
            Permissions.Payroll.Declare,
            Permissions.Payroll.Export,
            Permissions.Payroll.Settings
        },
        _ => Array.Empty<string>()
    };

    public static readonly AppModule[] AllValues = Enum.GetValues<AppModule>();

    /// <summary>
    /// True if <paramref name="effectivePermissions"/> contains at least one permission key mapped to this module.
    /// </summary>
    public static bool HasAnyEffectivePermission(this AppModule module, IReadOnlySet<string> effectivePermissions)
    {
        foreach (var key in module.GetPermissionKeys())
        {
            if (effectivePermissions.Contains(key))
                return true;
        }

        return false;
    }

    /// <summary>
    /// When per-user module grants exist, UI/JWT module list must match what the role can actually do:
    /// keep only modules toggled on whose keys intersect <paramref name="effectivePermissions"/>.
    /// </summary>
    public static IReadOnlyList<AppModule> FilterToModulesWithEffectivePermissions(
        IReadOnlyList<AppModule> modulesEnabledByGrantToggle,
        IReadOnlySet<string> effectivePermissions)
    {
        var result = new List<AppModule>();
        foreach (var m in modulesEnabledByGrantToggle)
        {
            if (m.HasAnyEffectivePermission(effectivePermissions))
                result.Add(m);
        }

        return result;
    }
}
