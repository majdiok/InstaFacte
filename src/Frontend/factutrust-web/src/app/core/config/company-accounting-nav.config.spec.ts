import { applyCompanyAccountingSidebar, isCompanyAllowedAccountingPath } from './company-accounting-nav.config';
import { NavItem } from './app-navigation.registry';

describe('company-accounting-nav.config', () => {
  it('allows états comptables routes', () => {
    expect(isCompanyAllowedAccountingPath('/accounting/journal')).toBe(true);
    expect(isCompanyAllowedAccountingPath('/accounting/balance')).toBe(true);
    expect(isCompanyAllowedAccountingPath('/accounting/financial-statements')).toBe(true);
  });

  it('allows accounting AI assistant route', () => {
    expect(isCompanyAllowedAccountingPath('/ai-assistant/comptabilite')).toBe(true);
  });

  it('blocks operational accounting routes', () => {
    expect(isCompanyAllowedAccountingPath('/accounting/manual-entry')).toBe(false);
    expect(isCompanyAllowedAccountingPath('/accounting/home')).toBe(false);
    expect(isCompanyAllowedAccountingPath('/accounting/chart')).toBe(false);
  });

  it('allows fixed-assets sub-routes', () => {
    expect(isCompanyAllowedAccountingPath('/accounting/fixed-assets')).toBe(true);
    expect(isCompanyAllowedAccountingPath('/accounting/fixed-assets/abc-uuid')).toBe(true);
    expect(isCompanyAllowedAccountingPath('/accounting/fixed-assets/depreciation-run')).toBe(true);
  });

  it('allows audit journal route', () => {
    expect(isCompanyAllowedAccountingPath('/audit')).toBe(true);
  });

  it('does not treat non-accounting routes as allowed accounting paths', () => {
    expect(isCompanyAllowedAccountingPath('/invoices')).toBe(false);
    expect(isCompanyAllowedAccountingPath('/dashboard')).toBe(false);
  });

  it('replaces Comptabilité children with the 4 company submenu entries', () => {
    const items: NavItem[] = [
      {
        label: 'Comptabilité',
        icon: 'fa-solid fa-calculator',
        children: [
          { label: 'Journal', route: '/accounting/journal' },
          { label: 'Saisie manuelle', route: '/accounting/manual-entry' }
        ]
      }
    ];

    const result = applyCompanyAccountingSidebar(items);
    const compta = result.find(i => i.label === 'Comptabilité');

    expect(compta?.children?.map(c => c.label)).toEqual([
      'Assistant Comptabilité',
      'États comptables',
      'Déclaration mensuelle',
      'Liste des immobilisations'
    ]);
  });
});
