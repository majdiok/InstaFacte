using FactuTrust.Domain.Enums;

namespace FactuTrust.API.Authorization;

/// <summary>Authorization policy names (<c>perm:...</c>) for use in <see cref="Microsoft.AspNetCore.Authorization.AuthorizeAttribute"/>.</summary>
public static class PermissionPolicies
{
    public const string ClientsCreate = "perm:" + Permissions.Clients.Create;
    public const string ClientsRead = "perm:" + Permissions.Clients.Read;
    public const string ClientsUpdate = "perm:" + Permissions.Clients.Update;
    public const string ClientsDelete = "perm:" + Permissions.Clients.Delete;

    public const string ProductsCreate = "perm:" + Permissions.Products.Create;
    public const string ProductsRead = "perm:" + Permissions.Products.Read;
    public const string ProductsUpdate = "perm:" + Permissions.Products.Update;
    public const string ProductsDelete = "perm:" + Permissions.Products.Delete;

    public const string QuotesCreate = "perm:" + Permissions.Quotes.Create;
    public const string QuotesRead = "perm:" + Permissions.Quotes.Read;
    public const string QuotesUpdate = "perm:" + Permissions.Quotes.Update;
    public const string QuotesDelete = "perm:" + Permissions.Quotes.Delete;

    public const string DeliveryNotesCreate = "perm:" + Permissions.DeliveryNotes.Create;
    public const string DeliveryNotesRead = "perm:" + Permissions.DeliveryNotes.Read;
    public const string DeliveryNotesUpdate = "perm:" + Permissions.DeliveryNotes.Update;
    public const string DeliveryNotesDelete = "perm:" + Permissions.DeliveryNotes.Delete;

    public const string InvoicesCreate = "perm:" + Permissions.Invoices.Create;
    public const string InvoicesRead = "perm:" + Permissions.Invoices.Read;
    public const string InvoicesUpdate = "perm:" + Permissions.Invoices.Update;
    public const string InvoicesDelete = "perm:" + Permissions.Invoices.Delete;
    public const string InvoicesSign = "perm:" + Permissions.Invoices.Sign;
    public const string InvoicesSend = "perm:" + Permissions.Invoices.Send;

    public const string PaymentsCreate = "perm:" + Permissions.Payments.Create;
    public const string PaymentsRead = "perm:" + Permissions.Payments.Read;
    public const string PaymentsUpdate = "perm:" + Permissions.Payments.Update;

    public const string ReportsView = "perm:" + Permissions.Reports.View;
    public const string ReportsExport = "perm:" + Permissions.Reports.Export;

    public const string SettingsRead = "perm:" + Permissions.Settings.Read;
    public const string SettingsUpdate = "perm:" + Permissions.Settings.Update;

    public const string SuppliersCreate = "perm:" + Permissions.Suppliers.Create;
    public const string SuppliersRead = "perm:" + Permissions.Suppliers.Read;
    public const string SuppliersUpdate = "perm:" + Permissions.Suppliers.Update;
    public const string SuppliersDelete = "perm:" + Permissions.Suppliers.Delete;

    public const string PurchaseOrdersCreate = "perm:" + Permissions.PurchaseOrders.Create;
    public const string PurchaseOrdersRead = "perm:" + Permissions.PurchaseOrders.Read;
    public const string PurchaseOrdersUpdate = "perm:" + Permissions.PurchaseOrders.Update;
    public const string PurchaseOrdersDelete = "perm:" + Permissions.PurchaseOrders.Delete;

    public const string PurchaseReceiptsCreate = "perm:" + Permissions.PurchaseReceipts.Create;
    public const string PurchaseReceiptsRead = "perm:" + Permissions.PurchaseReceipts.Read;
    public const string PurchaseReceiptsUpdate = "perm:" + Permissions.PurchaseReceipts.Update;
    public const string PurchaseReceiptsDelete = "perm:" + Permissions.PurchaseReceipts.Delete;

    public const string SalesOrdersCreate = "perm:" + Permissions.SalesOrders.Create;
    public const string SalesOrdersRead = "perm:" + Permissions.SalesOrders.Read;
    public const string SalesOrdersUpdate = "perm:" + Permissions.SalesOrders.Update;
    public const string SalesOrdersDelete = "perm:" + Permissions.SalesOrders.Delete;

    public const string PricingCreate = "perm:" + Permissions.Pricing.Create;
    public const string PricingRead = "perm:" + Permissions.Pricing.Read;
    public const string PricingUpdate = "perm:" + Permissions.Pricing.Update;
    public const string PricingDelete = "perm:" + Permissions.Pricing.Delete;

    public const string SupplierInvoicesCreate = "perm:" + Permissions.SupplierInvoices.Create;
    public const string SupplierInvoicesRead = "perm:" + Permissions.SupplierInvoices.Read;
    public const string SupplierInvoicesUpdate = "perm:" + Permissions.SupplierInvoices.Update;
    public const string SupplierInvoicesDelete = "perm:" + Permissions.SupplierInvoices.Delete;

    public const string StockCreate = "perm:" + Permissions.Stock.Create;
    public const string StockRead = "perm:" + Permissions.Stock.Read;
    public const string StockUpdate = "perm:" + Permissions.Stock.Update;
    public const string StockDelete = "perm:" + Permissions.Stock.Delete;

    public const string StockTransfersCreate = "perm:" + Permissions.StockTransfers.Create;
    public const string StockTransfersRead = "perm:" + Permissions.StockTransfers.Read;
    public const string StockTransfersUpdate = "perm:" + Permissions.StockTransfers.Update;
    public const string StockTransfersDelete = "perm:" + Permissions.StockTransfers.Delete;

    public const string InventoryCreate = "perm:" + Permissions.Inventory.Create;
    public const string InventoryRead = "perm:" + Permissions.Inventory.Read;
    public const string InventoryUpdate = "perm:" + Permissions.Inventory.Update;
    public const string InventoryDelete = "perm:" + Permissions.Inventory.Delete;

    public const string AccountingRead = "perm:" + Permissions.Accounting.Read;
    public const string AccountingCreate = "perm:" + Permissions.Accounting.Create;
    public const string AccountingClose = "perm:" + Permissions.Accounting.Close;
    public const string AccountingValidate = "perm:" + Permissions.Accounting.Validate;

    /// <summary>Accounting firm operating on a delegated client dossier (not native firm home).</summary>
    public const string FirmDelegatedContext = "firm:delegated-context";
    public const string AccountingReverse = "perm:" + Permissions.Accounting.Reverse;
    public const string AccountingImport = "perm:" + Permissions.Accounting.Import;
    public const string AccountingDeclare = "perm:" + Permissions.Accounting.Declare;

    public const string AuditRead = "perm:" + Permissions.Audit.Read;

    public const string CrmRead = "perm:" + Permissions.CRM.Read;
    public const string CrmCreate = "perm:" + Permissions.CRM.Create;
    public const string CrmUpdate = "perm:" + Permissions.CRM.Update;
    public const string CrmDelete = "perm:" + Permissions.CRM.Delete;

    public const string SalesTargetsRead = "perm:" + Permissions.SalesTargets.Read;
    public const string SalesTargetsManage = "perm:" + Permissions.SalesTargets.Manage;

    public const string ReportsSalesOwn = "perm:" + Permissions.Reports.SalesOwn;

    public const string WithholdingTaxRead = "perm:" + Permissions.WithholdingTax.Read;
    public const string WithholdingTaxCreate = "perm:" + Permissions.WithholdingTax.Create;
    public const string WithholdingTaxEdit = "perm:" + Permissions.WithholdingTax.Edit;
    public const string WithholdingTaxValidate = "perm:" + Permissions.WithholdingTax.Validate;
    public const string WithholdingTaxDelete = "perm:" + Permissions.WithholdingTax.Delete;
    public const string WithholdingTaxExport = "perm:" + Permissions.WithholdingTax.Export;

    public const string AiChat = "perm:" + Permissions.AI.Chat;

    /// <summary>
    /// Policy required to manage the tenant's public storefront (opt-in, profile, products
    /// visibility, review, unpublish). Wired to <c>Permissions.Storefront.Manage</c>.
    /// </summary>
    public const string StorefrontTenantOwner = "perm:" + Permissions.Storefront.Manage;

    // AI Forecasting module (sales/revenue forecasting, replenishment, promotions, ABC/XYZ).
    public const string ForecastingView = "perm:" + Permissions.Forecasting.View;
    public const string ForecastingManage = "perm:" + Permissions.Forecasting.Manage;

    // Studio (low-code) — design-time.
    public const string StudioDesignEntities = "perm:" + Permissions.Studio.DesignEntities;
    public const string StudioDesignForms = "perm:" + Permissions.Studio.DesignForms;
    public const string StudioDesignReports = "perm:" + Permissions.Studio.DesignReports;

    // Studio (low-code) — runtime consumption of custom data.
    public const string CustomRecordsRead = "perm:" + Permissions.CustomData.RecordsRead;
    public const string CustomRecordsWrite = "perm:" + Permissions.CustomData.RecordsWrite;
    public const string CustomReportsView = "perm:" + Permissions.CustomData.ReportsView;

    // Payroll (RH & Paie).
    public const string PayrollRead = "perm:" + Permissions.Payroll.Read;
    public const string PayrollManageEmployees = "perm:" + Permissions.Payroll.ManageEmployees;
    public const string PayrollRun = "perm:" + Permissions.Payroll.RunPayroll;
    public const string PayrollValidate = "perm:" + Permissions.Payroll.Validate;
    public const string PayrollDeclare = "perm:" + Permissions.Payroll.Declare;
    public const string PayrollExport = "perm:" + Permissions.Payroll.Export;
    public const string PayrollSettings = "perm:" + Permissions.Payroll.Settings;

    public const string FirmUsersManage = "perm:" + Permissions.Firm.UsersManage;

    public const string HonorairesInvoicesCreate = "perm:" + Permissions.HonorairesInvoices.Create;
    public const string HonorairesInvoicesRead = "perm:" + Permissions.HonorairesInvoices.Read;
    public const string HonorairesInvoicesUpdate = "perm:" + Permissions.HonorairesInvoices.Update;
    public const string HonorairesInvoicesDelete = "perm:" + Permissions.HonorairesInvoices.Delete;
    public const string HonorairesInvoicesValidate = "perm:" + Permissions.HonorairesInvoices.Validate;
    public const string HonorairesInvoicesSend = "perm:" + Permissions.HonorairesInvoices.Send;

    public const string HonorairesQuotesCreate = "perm:" + Permissions.HonorairesQuotes.Create;
    public const string HonorairesQuotesRead = "perm:" + Permissions.HonorairesQuotes.Read;
    public const string HonorairesQuotesUpdate = "perm:" + Permissions.HonorairesQuotes.Update;
    public const string HonorairesQuotesDelete = "perm:" + Permissions.HonorairesQuotes.Delete;
    public const string HonorairesQuotesConvert = "perm:" + Permissions.HonorairesQuotes.Convert;

    public const string HonorairesPaymentsCreate = "perm:" + Permissions.HonorairesPayments.Create;
    public const string HonorairesPaymentsRead = "perm:" + Permissions.HonorairesPayments.Read;
}
