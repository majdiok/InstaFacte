import { AppModule } from '@core/models/app-module';
import type { UserModuleAccessItem } from '@core/services/tenant-users.service';
import { allFeatureKeysForModule, withSubFeatureToggled } from './module-features.config';

/**
 * Plan §5.1 — feature-key parity pin, mirror of the backend
 * `ModuleFeatureCatalogSnapshotTests.Pin_des_feature_keys_par_module` (FactuTrust.Infrastructure.Tests).
 * Same literals on both sides is the contract: any drift here or on the backend must fail one of
 * the two pins and force a synchronized fix (same technique as registration-catalog.spec.ts for
 * the segment/domain matrix).
 *
 * TODO (plan §5.1, out of v1 scope): the users screen could consume
 * GET /api/tenant-users/module-catalog?role= (already served by the backend) instead of this
 * hardcoded mirror, reducing module-features.config.ts to French labels only.
 */
describe('module-features.config — parity pin with ModuleFeatureCatalog.cs', () => {
  it('Sales', () => {
    expect(allFeatureKeysForModule(AppModule.Sales)).toEqual([
      'sales_orders', 'quotes', 'delivery_notes', 'return_notes', 'invoices', 'pricing'
    ]);
  });

  it('Clients', () => {
    expect(allFeatureKeysForModule(AppModule.Clients)).toEqual(['read', 'manage']);
  });

  it('Products', () => {
    expect(allFeatureKeysForModule(AppModule.Products)).toEqual(['read', 'manage']);
  });

  it('Treasury', () => {
    expect(allFeatureKeysForModule(AppModule.Treasury)).toEqual([
      'read', 'manage', 'forecast_read', 'forecast_manage'
    ]);
  });

  it('Reports', () => {
    expect(allFeatureKeysForModule(AppModule.Reports)).toEqual([
      'sales', 'purchases', 'stock', 'fiches', 'payments'
    ]);
  });

  it('Administration', () => {
    expect(allFeatureKeysForModule(AppModule.Administration)).toEqual(['users', 'settings']);
  });

  it('Purchases', () => {
    expect(allFeatureKeysForModule(AppModule.Purchases)).toEqual([
      'suppliers', 'purchase_orders', 'purchase_receipts', 'supplier_invoices'
    ]);
  });

  it('Stock', () => {
    expect(allFeatureKeysForModule(AppModule.Stock)).toEqual([
      'stock', 'stock_transfers', 'inventory', 'stock_vouchers', 'stock_lots'
    ]);
  });

  it('Accounting', () => {
    expect(allFeatureKeysForModule(AppModule.Accounting)).toEqual([
      'journal', 'ledger', 'aging', 'vat_declaration', 'closing', 'audit_log', 'fixed_assets'
    ]);
  });

  it('CRM', () => {
    expect(allFeatureKeysForModule(AppModule.CRM)).toEqual([
      'opportunities', 'activities', 'targets', 'templates', 'dashboard'
    ]);
  });

  it('Honoraires', () => {
    expect(allFeatureKeysForModule(AppModule.Honoraires)).toEqual(['invoices', 'quotes', 'payments']);
  });

  it('Projects', () => {
    expect(allFeatureKeysForModule(AppModule.Projects)).toEqual([
      'core', 'tasks', 'time', 'billing', 'esn', 'btp'
    ]);
  });

  it('RecurringContracts', () => {
    expect(allFeatureKeysForModule(AppModule.RecurringContracts)).toEqual(['contracts', 'usage', 'billing']);
  });

  it('Fiscal, AI, Forecasting, Studio, Payroll have no backend-side sub-features', () => {
    expect(allFeatureKeysForModule(AppModule.Fiscal)).toEqual([]);
    expect(allFeatureKeysForModule(AppModule.AI)).toEqual([]);
    expect(allFeatureKeysForModule(AppModule.Forecasting)).toEqual([]);
    expect(allFeatureKeysForModule(AppModule.Studio)).toEqual([]);
    expect(allFeatureKeysForModule(AppModule.Payroll)).toEqual([]);
  });
});

describe('withSubFeatureToggled', () => {
  const visible = ['read', 'manage', 'forecast_read'];

  function treasury(keys: string[] | null): UserModuleAccessItem {
    return { module: AppModule.Treasury, enabled: true, enabledFeatureKeys: keys };
  }

  it('null + décocher une clé visible n’inclut pas une clé masquée du catalogue statique', () => {
    const next = withSubFeatureToggled(treasury(null), 'manage', false, visible);
    expect(next.enabledFeatureKeys).toEqual(['read', 'forecast_read']);
    expect(next.enabledFeatureKeys).not.toContain('forecast_manage');
  });

  it('liste explicite contenant une clé masquée → la clé est retirée au toggle d’une clé visible', () => {
    const next = withSubFeatureToggled(
      treasury(['read', 'forecast_manage']),
      'forecast_read',
      true,
      visible
    );
    expect(next.enabledFeatureKeys).toEqual(['read', 'forecast_read']);
    expect(next.enabledFeatureKeys).not.toContain('forecast_manage');
  });
});
