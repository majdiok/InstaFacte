import {
  DELEGATED_FIRM_ACHATS_ALLOWED_ROUTES,
  DELEGATED_FIRM_VENTES_ALLOWED_ROUTES,
  FIRM_NATIVE_NAV,
  filterDelegatedFirmSectionChildren,
  isDelegatedFirmBlockedSalesPurchasesRoute,
  isDelegatedReadOnlyRoute,
  isFirmDelegatedReadonly
} from './firm-navigation.registry';
import { NavSubItem } from './app-navigation.registry';

describe('firm-navigation.registry — FIRM_NATIVE_NAV', () => {
  it('FIRM_NATIVE_NAV does not include fiscal schedule route', () => {
    expect(FIRM_NATIVE_NAV.some(i => i.route === '/firm/fiscal-schedule')).toBe(false);
  });
});

describe('firm-navigation.registry — delegated firm sales/purchases', () => {
  const ventesChildren: NavSubItem[] = [
    { label: 'Devis', route: '/quotes' },
    { label: 'Factures', route: '/invoices' },
    { label: 'Factures impayées', route: '/invoices/unpaid' },
    { label: 'Rapports', route: '/reports/sales' },
    { label: 'Rapport Bénéfices', route: '/reports/profit' }
  ];

  const achatsChildren: NavSubItem[] = [
    { label: 'Fournisseurs', route: '/suppliers' },
    { label: 'Factures fournisseurs', route: '/supplier-invoices' },
    { label: 'Factures impayées', route: '/supplier-invoices/unpaid' },
    { label: 'Rapports', route: '/reports/purchases' }
  ];

  it('allowlists contain exactly three ventes and achats routes', () => {
    expect(DELEGATED_FIRM_VENTES_ALLOWED_ROUTES.size).toBe(3);
    expect(DELEGATED_FIRM_ACHATS_ALLOWED_ROUTES.size).toBe(3);
  });

  it('filterDelegatedFirmSectionChildren keeps only allowed ventes items', () => {
    const filtered = filterDelegatedFirmSectionChildren('Ventes', ventesChildren);
    expect(filtered.map(c => c.label)).toEqual(['Factures', 'Factures impayées', 'Rapports']);
  });

  it('filterDelegatedFirmSectionChildren keeps only allowed achats items', () => {
    const filtered = filterDelegatedFirmSectionChildren('Achats', achatsChildren);
    expect(filtered.map(c => c.label)).toEqual([
      'Factures fournisseurs',
      'Factures impayées',
      'Rapports'
    ]);
  });

  it('isDelegatedFirmBlockedSalesPurchasesRoute blocks quotes and analytics', () => {
    expect(isDelegatedFirmBlockedSalesPurchasesRoute('/quotes')).toBe(true);
    expect(isDelegatedFirmBlockedSalesPurchasesRoute('/quotes/new')).toBe(true);
    expect(isDelegatedFirmBlockedSalesPurchasesRoute('/reports/analytics')).toBe(true);
    expect(isDelegatedFirmBlockedSalesPurchasesRoute('/reports/profit')).toBe(true);
    expect(isDelegatedFirmBlockedSalesPurchasesRoute('/suppliers')).toBe(true);
  });

  it('isDelegatedFirmBlockedSalesPurchasesRoute allows invoices and sales/purchases reports', () => {
    expect(isDelegatedFirmBlockedSalesPurchasesRoute('/invoices')).toBe(false);
    expect(isDelegatedFirmBlockedSalesPurchasesRoute('/invoices/unpaid')).toBe(false);
    expect(isDelegatedFirmBlockedSalesPurchasesRoute('/invoices/abc-123')).toBe(false);
    expect(isDelegatedFirmBlockedSalesPurchasesRoute('/reports/sales')).toBe(false);
    expect(isDelegatedFirmBlockedSalesPurchasesRoute('/reports/purchases')).toBe(false);
    expect(isDelegatedFirmBlockedSalesPurchasesRoute('/supplier-invoices')).toBe(false);
  });

  it('isDelegatedFirmBlockedSalesPurchasesRoute blocks reports hub and other report sections', () => {
    expect(isDelegatedFirmBlockedSalesPurchasesRoute('/reports')).toBe(true);
    expect(isDelegatedFirmBlockedSalesPurchasesRoute('/reports/fiches')).toBe(true);
    expect(isDelegatedFirmBlockedSalesPurchasesRoute('/reports/stock')).toBe(true);
  });

  it('isDelegatedReadOnlyRoute no longer includes quotes or suppliers', () => {
    expect(isDelegatedReadOnlyRoute('/quotes')).toBe(false);
    expect(isDelegatedReadOnlyRoute('/suppliers')).toBe(false);
    expect(isDelegatedReadOnlyRoute('/invoices')).toBe(true);
    expect(isDelegatedReadOnlyRoute('/payments')).toBe(true);
  });
});

describe('firm-navigation.registry — isFirmDelegatedReadonly', () => {
  it('returns true only for accounting firm in delegated mode', () => {
    expect(
      isFirmDelegatedReadonly({ isAccountingFirm: () => true, isDelegatedMode: () => true })
    ).toBe(true);
    expect(
      isFirmDelegatedReadonly({ isAccountingFirm: () => true, isDelegatedMode: () => false })
    ).toBe(false);
    expect(
      isFirmDelegatedReadonly({ isAccountingFirm: () => false, isDelegatedMode: () => true })
    ).toBe(false);
    expect(
      isFirmDelegatedReadonly({ isAccountingFirm: () => false, isDelegatedMode: () => false })
    ).toBe(false);
  });
});
