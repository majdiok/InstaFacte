using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Authorization;

/// <summary>
/// Maps stable feature keys (per <see cref="AppModule"/>) to permission strings for sub-module scoping.
/// </summary>
public static class ModuleFeatureCatalog
{
    public static IReadOnlyList<string> GetValidFeatureKeys(AppModule module) => module switch
    {
        AppModule.Sales => new[] { "sales_orders", "quotes", "delivery_notes", "return_notes", "invoices", "pricing" },
        AppModule.Clients => new[] { "read", "manage" },
        AppModule.Products => new[] { "read", "manage" },
        AppModule.Treasury => new[] { "read", "manage", "forecast_read", "forecast_manage" },
        AppModule.Reports => new[] { "sales", "purchases", "stock", "fiches", "payments" },
        AppModule.Administration => new[] { "users", "settings" },
        AppModule.Purchases => new[] { "suppliers", "purchase_orders", "purchase_receipts", "supplier_invoices" },
        AppModule.Stock => new[] { "stock", "stock_transfers", "inventory", "stock_vouchers", "stock_lots" },
        AppModule.Accounting => new[] { "journal", "ledger", "aging", "vat_declaration", "closing", "audit_log", "fixed_assets" },
        AppModule.CRM => new[] { "opportunities", "activities", "targets", "templates", "dashboard" },
        AppModule.Honoraires => new[] { "invoices", "quotes", "payments" },
        AppModule.Projects => new[] { "core", "tasks", "time", "billing", "esn", "btp" },
        AppModule.RecurringContracts => new[] { "contracts", "usage", "billing" },
        _ => Array.Empty<string>()
    };

    public static bool IsValidFeatureKey(AppModule module, string featureKey) =>
        GetValidFeatureKeys(module).Contains(featureKey, StringComparer.Ordinal);

    /// <summary>
    /// Returns permission keys for one feature, or empty if unknown.
    /// </summary>
    public static IReadOnlyList<string> GetPermissionsForFeature(AppModule module, string featureKey)
    {
        if (!IsValidFeatureKey(module, featureKey))
            return Array.Empty<string>();

        return (module, featureKey) switch
        {
            (AppModule.Sales, "sales_orders") => new[]
            {
                Permissions.SalesOrders.Create, Permissions.SalesOrders.Read,
                Permissions.SalesOrders.Update, Permissions.SalesOrders.Delete
            },
            (AppModule.Sales, "quotes") => new[]
            {
                Permissions.Quotes.Create, Permissions.Quotes.Read, Permissions.Quotes.Update, Permissions.Quotes.Delete
            },
            (AppModule.Sales, "delivery_notes") => new[]
            {
                Permissions.DeliveryNotes.Create, Permissions.DeliveryNotes.Read, Permissions.DeliveryNotes.Update,
                Permissions.DeliveryNotes.Delete
            },
            (AppModule.Sales, "return_notes") => new[]
            {
                Permissions.SalesReturnNotes.Create, Permissions.SalesReturnNotes.Read,
                Permissions.SalesReturnNotes.Update, Permissions.SalesReturnNotes.Delete
            },
            (AppModule.Sales, "invoices") => new[]
            {
                Permissions.Invoices.Create, Permissions.Invoices.Read, Permissions.Invoices.Update, Permissions.Invoices.Delete,
                Permissions.Invoices.Sign, Permissions.Invoices.Send
            },
            (AppModule.Sales, "pricing") => new[]
            {
                Permissions.Pricing.Create, Permissions.Pricing.Read,
                Permissions.Pricing.Update, Permissions.Pricing.Delete
            },
            (AppModule.Clients, "read") => new[] { Permissions.Clients.Read },
            (AppModule.Clients, "manage") => new[]
            {
                Permissions.Clients.Create, Permissions.Clients.Update, Permissions.Clients.Delete
            },
            (AppModule.Products, "read") => new[] { Permissions.Products.Read },
            (AppModule.Products, "manage") => new[]
            {
                Permissions.Products.Create, Permissions.Products.Update, Permissions.Products.Delete
            },
            (AppModule.Treasury, "read") => new[] { Permissions.Payments.Read },
            (AppModule.Treasury, "manage") => new[]
            {
                Permissions.Payments.Create, Permissions.Payments.Update
            },
            // Trésorerie prévisionnelle par IA — sous-features distinctes des paiements :
            // consulter la projection n'implique pas de saisir des règlements, et inversement.
            (AppModule.Treasury, "forecast_read") => new[] { Permissions.TreasuryForecast.View },
            (AppModule.Treasury, "forecast_manage") => new[] { Permissions.TreasuryForecast.Manage },
            // Reports: each sub-feature currently maps to both View+Export.
            // Explicit cases prevent the catch-all from silently matching future keys.
            (AppModule.Reports, "sales") => new[]
            {
                Permissions.Reports.View, Permissions.Reports.Export
            },
            (AppModule.Reports, "purchases") => new[]
            {
                Permissions.Reports.View, Permissions.Reports.Export
            },
            (AppModule.Reports, "stock") => new[]
            {
                Permissions.Reports.View, Permissions.Reports.Export
            },
            (AppModule.Reports, "fiches") => new[]
            {
                Permissions.Reports.View, Permissions.Reports.Export
            },
            (AppModule.Reports, "payments") => new[]
            {
                Permissions.Reports.View, Permissions.Reports.Export
            },
            (AppModule.Administration, "users") => new[]
            {
                Permissions.Users.Create, Permissions.Users.Read, Permissions.Users.Update, Permissions.Users.Delete
            },
            (AppModule.Administration, "settings") => new[]
            {
                Permissions.Settings.Read, Permissions.Settings.Update
            },
            (AppModule.Purchases, "suppliers") => new[]
            {
                Permissions.Suppliers.Create, Permissions.Suppliers.Read, Permissions.Suppliers.Update, Permissions.Suppliers.Delete
            },
            (AppModule.Purchases, "purchase_orders") => new[]
            {
                Permissions.PurchaseOrders.Create, Permissions.PurchaseOrders.Read, Permissions.PurchaseOrders.Update,
                Permissions.PurchaseOrders.Delete
            },
            (AppModule.Purchases, "purchase_receipts") => new[]
            {
                Permissions.PurchaseReceipts.Create, Permissions.PurchaseReceipts.Read, Permissions.PurchaseReceipts.Update,
                Permissions.PurchaseReceipts.Delete
            },
            (AppModule.Purchases, "supplier_invoices") => new[]
            {
                Permissions.SupplierInvoices.Create, Permissions.SupplierInvoices.Read, Permissions.SupplierInvoices.Update,
                Permissions.SupplierInvoices.Delete
            },
            (AppModule.Stock, "stock") => new[]
            {
                Permissions.Stock.Create, Permissions.Stock.Read, Permissions.Stock.Update, Permissions.Stock.Delete
            },
            (AppModule.Stock, "stock_lots") => new[]
            {
                Permissions.Stock.Create, Permissions.Stock.Read, Permissions.Stock.Update, Permissions.Stock.Delete
            },
            (AppModule.Stock, "stock_transfers") => new[]
            {
                Permissions.StockTransfers.Create, Permissions.StockTransfers.Read, Permissions.StockTransfers.Update,
                Permissions.StockTransfers.Delete
            },
            (AppModule.Stock, "stock_vouchers") => new[]
            {
                Permissions.StockVouchers.Create, Permissions.StockVouchers.Read, Permissions.StockVouchers.Update,
                Permissions.StockVouchers.Delete
            },
            (AppModule.Stock, "inventory") => new[]
            {
                Permissions.Inventory.Create, Permissions.Inventory.Read, Permissions.Inventory.Update, Permissions.Inventory.Delete
            },
            (AppModule.Accounting, "journal") => new[]
            {
                Permissions.Accounting.Read, Permissions.Accounting.Create
            },
            (AppModule.Accounting, "ledger") => new[] { Permissions.Accounting.Read },
            (AppModule.Accounting, "aging") => new[] { Permissions.Accounting.Read },
            (AppModule.Accounting, "vat_declaration") => new[] { Permissions.Accounting.Read, Permissions.Accounting.Create },
            (AppModule.Accounting, "closing") => new[]
            {
                Permissions.Accounting.Read, Permissions.Accounting.Close
            },
            (AppModule.Accounting, "audit_log") => new[] { Permissions.Audit.Read },
            (AppModule.Accounting, "fixed_assets") => new[]
            {
                Permissions.Accounting.Read, Permissions.Accounting.Create
            },
            (AppModule.CRM, "opportunities") => new[]
            {
                Permissions.CRM.Read, Permissions.CRM.Create, Permissions.CRM.Update, Permissions.CRM.Delete
            },
            (AppModule.CRM, "activities") => new[]
            {
                Permissions.CRM.Read, Permissions.CRM.Create, Permissions.CRM.Update, Permissions.CRM.Delete
            },
            (AppModule.CRM, "targets") => new[]
            {
                Permissions.SalesTargets.Read, Permissions.SalesTargets.Manage
            },
            (AppModule.CRM, "templates") => new[]
            {
                Permissions.CRM.Read, Permissions.CRM.Create, Permissions.CRM.Update, Permissions.CRM.Delete
            },
            (AppModule.CRM, "dashboard") => new[]
            {
                Permissions.CRM.Read, Permissions.Reports.SalesOwn
            },
            (AppModule.Honoraires, "invoices") => new[]
            {
                Permissions.HonorairesInvoices.Create, Permissions.HonorairesInvoices.Read,
                Permissions.HonorairesInvoices.Update, Permissions.HonorairesInvoices.Delete,
                Permissions.HonorairesInvoices.Validate, Permissions.HonorairesInvoices.Send
            },
            (AppModule.Honoraires, "quotes") => new[]
            {
                Permissions.HonorairesQuotes.Create, Permissions.HonorairesQuotes.Read,
                Permissions.HonorairesQuotes.Update, Permissions.HonorairesQuotes.Delete,
                Permissions.HonorairesQuotes.Convert
            },
            (AppModule.Honoraires, "payments") => new[]
            {
                Permissions.HonorairesPayments.Create, Permissions.HonorairesPayments.Read
            },
            (AppModule.Projects, "core") => new[]
            {
                Permissions.Projects.Read, Permissions.Projects.Create, Permissions.Projects.Update,
                Permissions.Projects.Delete, Permissions.Projects.ManageTeam
            },
            (AppModule.Projects, "tasks") => new[]
            {
                Permissions.ProjectTasks.Create, Permissions.ProjectTasks.Read,
                Permissions.ProjectTasks.Update, Permissions.ProjectTasks.Delete
            },
            (AppModule.Projects, "time") => new[]
            {
                Permissions.ProjectTime.Create, Permissions.ProjectTime.Read,
                Permissions.ProjectTime.Submit, Permissions.ProjectTime.Validate
            },
            (AppModule.Projects, "billing") => new[]
            {
                Permissions.ProjectBilling.Read, Permissions.ProjectBilling.Create
            },
            (AppModule.Projects, "esn") => new[]
            {
                Permissions.ProjectBilling.Read, Permissions.ProjectBilling.Create,
                Permissions.Projects.Read, Permissions.ProjectTime.Read
            },
            (AppModule.Projects, "btp") => new[]
            {
                Permissions.ProjectBilling.Read, Permissions.ProjectBilling.Create,
                Permissions.Projects.Read, Permissions.PurchaseOrders.Read
            },
            (AppModule.RecurringContracts, "contracts") => new[]
            {
                Permissions.RecurringContracts.Read, Permissions.RecurringContracts.Create,
                Permissions.RecurringContracts.Update, Permissions.RecurringContracts.Delete,
                Permissions.RecurringContracts.Manage
            },
            (AppModule.RecurringContracts, "usage") => new[]
            {
                Permissions.RecurringContracts.Read, Permissions.RecurringContracts.RecordUsage
            },
            (AppModule.RecurringContracts, "billing") => new[]
            {
                Permissions.RecurringContracts.Read, Permissions.RecurringContracts.TriggerBilling
            },
            _ => Array.Empty<string>()
        };
    }

    /// <summary>
    /// Expands selected feature keys into permission strings. Unknown keys are skipped.
    /// </summary>
    public static void AddPermissionsForFeatures(
        AppModule module,
        IEnumerable<string> featureKeys,
        ISet<string> target)
    {
        foreach (var key in featureKeys)
        {
            foreach (var p in GetPermissionsForFeature(module, key))
                target.Add(p);
        }
    }
}
