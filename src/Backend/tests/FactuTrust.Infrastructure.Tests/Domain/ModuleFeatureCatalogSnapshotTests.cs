using FactuTrust.Domain.Authorization;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

/// <summary>
/// Plan §5.1 — feature-key parity pin between the backend source of truth
/// (<see cref="ModuleFeatureCatalog"/>) and the frontend mirror
/// (<c>module-features.config.ts</c>). Any change to either side's feature keys must update BOTH
/// this test and the frontend's <c>module-features.config.spec.ts</c> pin in lock-step — that
/// synchronized breakage is the whole point (same contract technique as
/// <c>registration-catalog.spec.ts</c> for the segment/domain matrix).
///
/// TODO (plan §5.1, out of v1 scope): long-term the users screen could consume
/// GET /api/tenant-users/module-catalog?role= (already served by the backend) instead of
/// hardcoding a duplicate list on each side, reducing module-features.config.ts to French labels
/// only. Not done here to keep this phase purely additive.
/// </summary>
public sealed class ModuleFeatureCatalogSnapshotTests
{
    [Fact]
    public void Pin_des_feature_keys_par_module()
    {
        Assert.Equal(
            new[] { "sales_orders", "quotes", "delivery_notes", "return_notes", "invoices", "pricing" },
            ModuleFeatureCatalog.GetValidFeatureKeys(AppModule.Sales));

        Assert.Equal(
            new[] { "read", "manage" },
            ModuleFeatureCatalog.GetValidFeatureKeys(AppModule.Clients));

        Assert.Equal(
            new[] { "read", "manage" },
            ModuleFeatureCatalog.GetValidFeatureKeys(AppModule.Products));

        Assert.Equal(
            new[] { "read", "manage", "forecast_read", "forecast_manage" },
            ModuleFeatureCatalog.GetValidFeatureKeys(AppModule.Treasury));

        Assert.Equal(
            new[] { "sales", "purchases", "stock", "fiches", "payments" },
            ModuleFeatureCatalog.GetValidFeatureKeys(AppModule.Reports));

        Assert.Equal(
            new[] { "users", "settings" },
            ModuleFeatureCatalog.GetValidFeatureKeys(AppModule.Administration));

        Assert.Equal(
            new[] { "suppliers", "purchase_orders", "purchase_receipts", "supplier_invoices" },
            ModuleFeatureCatalog.GetValidFeatureKeys(AppModule.Purchases));

        Assert.Equal(
            new[] { "stock", "stock_transfers", "inventory", "stock_vouchers", "stock_lots" },
            ModuleFeatureCatalog.GetValidFeatureKeys(AppModule.Stock));

        Assert.Equal(
            new[] { "journal", "ledger", "aging", "vat_declaration", "closing", "audit_log", "fixed_assets" },
            ModuleFeatureCatalog.GetValidFeatureKeys(AppModule.Accounting));

        Assert.Equal(
            new[] { "opportunities", "activities", "targets", "templates", "dashboard" },
            ModuleFeatureCatalog.GetValidFeatureKeys(AppModule.CRM));

        Assert.Equal(
            new[] { "invoices", "quotes", "payments" },
            ModuleFeatureCatalog.GetValidFeatureKeys(AppModule.Honoraires));

        Assert.Equal(
            new[] { "core", "tasks", "time", "billing", "esn", "btp" },
            ModuleFeatureCatalog.GetValidFeatureKeys(AppModule.Projects));

        Assert.Equal(
            new[] { "contracts", "usage", "billing" },
            ModuleFeatureCatalog.GetValidFeatureKeys(AppModule.RecurringContracts));

        // No sub-feature scoping defined for these modules: the module-level grant is all-or-nothing.
        // The frontend mirror must keep an empty array too — never invent keys with no backend
        // counterpart (that was the pre-existing Forecasting view/manage bug this phase fixes).
        Assert.Empty(ModuleFeatureCatalog.GetValidFeatureKeys(AppModule.Fiscal));
        Assert.Empty(ModuleFeatureCatalog.GetValidFeatureKeys(AppModule.AI));
        Assert.Empty(ModuleFeatureCatalog.GetValidFeatureKeys(AppModule.Forecasting));
        Assert.Empty(ModuleFeatureCatalog.GetValidFeatureKeys(AppModule.Studio));
        Assert.Empty(ModuleFeatureCatalog.GetValidFeatureKeys(AppModule.Payroll));
    }

    /// <summary>
    /// Every AppModule value is covered by the pin above — catches a future enum addition that
    /// forgets to extend this test (and, by the parity contract, the frontend mirror).
    /// </summary>
    [Fact]
    public void Pin_couvre_les_18_modules_de_AppModule()
    {
        Assert.Equal(18, AppModuleExtensions.AllValues.Length);
    }
}
