import {
  isNavChildActive,
  isNavRouteActive,
  pathMatchesRoute,
  stripPathForMatch,
  findMatchingDirectChild,
  findLongestMatchingChildInParent
} from './nav-path-match';
import { NavItem } from '@core/config/app-navigation.registry';

describe('nav-path-match', () => {
  it('strips query/hash and trailing slash', () => {
    expect(stripPathForMatch('/invoices/unpaid/?x=1#y')).toBe('/invoices/unpaid');
    expect(stripPathForMatch('/')).toBe('/');
  });

  it('matches routes on segment boundaries only', () => {
    expect(pathMatchesRoute('/invoices', '/invoice')).toBeFalse();
    expect(pathMatchesRoute('/invoices/1', '/invoices')).toBeTrue();
  });

  it('detects active child with longest match', () => {
    const section: NavItem = {
      label: 'Ventes',
      children: [
        { label: 'Factures', route: '/invoices' },
        { label: 'Impayées', route: '/invoices/unpaid' }
      ]
    };
    expect(isNavChildActive('/invoices/unpaid', section)).toBeTrue();
    expect(isNavRouteActive('/accounting/home', '/accounting/home')).toBeTrue();
  });

  it('matches nested grandchildren for section and direct child active state', () => {
    const section: NavItem = {
      label: 'Comptabilité',
      children: [
        {
          label: 'États comptables',
          route: '/accounting/financial-statements',
          children: [
            { label: 'Journal', route: '/accounting/journal' },
            { label: 'Grand livre', route: '/accounting/ledger' }
          ]
        },
        { label: 'Déclaration mensuelle', route: '/accounting/vat-declaration' }
      ]
    };

    expect(isNavChildActive('/accounting/journal', section)).toBeTrue();
    expect(findLongestMatchingChildInParent('/accounting/journal', section)?.route).toBe(
      '/accounting/journal'
    );
    expect(findMatchingDirectChild('/accounting/journal', section)?.label).toBe('États comptables');
    expect(findMatchingDirectChild('/accounting/vat-declaration', section)?.label).toBe(
      'Déclaration mensuelle'
    );
  });
});
