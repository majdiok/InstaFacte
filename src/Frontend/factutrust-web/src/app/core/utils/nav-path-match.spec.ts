import {
  isNavChildActive,
  isNavRouteActive,
  isNavSubItemActive,
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
    expect(isNavSubItemActive('/invoices/unpaid', section, section.children![0])).toBeFalse();
    expect(isNavSubItemActive('/invoices/unpaid', section, section.children![1])).toBeTrue();
    expect(isNavSubItemActive('/invoices', section, section.children![0])).toBeTrue();
    expect(isNavSubItemActive('/invoices', section, section.children![1])).toBeFalse();
  });

  it('selects a single Projets submenu child via longest match', () => {
    const projets: NavItem = {
      label: 'Projets',
      children: [
        { label: 'Tableau de bord', route: '/projects/dashboard' },
        { label: 'Liste des projets', route: '/projects' },
        { label: 'Saisie des temps', route: '/projects/time' }
      ]
    };
    const [dashboard, list, time] = projets.children!;

    expect(isNavSubItemActive('/projects/time', projets, time)).toBeTrue();
    expect(isNavSubItemActive('/projects/time', projets, list)).toBeFalse();
    expect(isNavSubItemActive('/projects/time', projets, dashboard)).toBeFalse();

    expect(isNavSubItemActive('/projects/dashboard', projets, dashboard)).toBeTrue();
    expect(isNavSubItemActive('/projects/dashboard', projets, list)).toBeFalse();

    expect(isNavSubItemActive('/projects', projets, list)).toBeTrue();
    expect(isNavSubItemActive('/projects', projets, dashboard)).toBeFalse();
    expect(isNavSubItemActive('/projects', projets, time)).toBeFalse();

    expect(isNavSubItemActive('/projects/abc-uuid', projets, list)).toBeTrue();
    expect(isNavSubItemActive('/projects/abc-uuid', projets, time)).toBeFalse();
    expect(findLongestMatchingChildInParent('/projects/time', projets)?.route).toBe('/projects/time');
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
    expect(isNavSubItemActive('/accounting/journal', section, section.children![0])).toBeTrue();
    expect(isNavSubItemActive('/accounting/vat-declaration', section, section.children![1])).toBeTrue();
  });
});
