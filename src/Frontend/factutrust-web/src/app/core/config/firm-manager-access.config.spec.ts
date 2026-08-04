import { FIRM_NATIVE_NAV } from './firm-navigation.registry';
import { NavItem } from './app-navigation.registry';
import {
  FIRM_MANAGER_ROUTE_PREFIXES,
  filterFirmManagerNav,
  isFirmManagerRoute
} from './firm-manager-access.config';

describe('firm-manager-access.config', () => {
  it('isFirmManagerRoute matches every configured prefix', () => {
    for (const prefix of FIRM_MANAGER_ROUTE_PREFIXES) {
      expect(isFirmManagerRoute(prefix)).withContext(prefix).toBeTrue();
      expect(isFirmManagerRoute(`${prefix}/child`)).withContext(`${prefix}/child`).toBeTrue();
    }
  });

  it('isFirmManagerRoute ignores query strings', () => {
    expect(isFirmManagerRoute('/firm/collaborateurs?tab=active')).toBeTrue();
    expect(isFirmManagerRoute('/firm/governance/time-sheets')).toBeFalse();
  });

  it('filterFirmManagerNav keeps all items for managers', () => {
    const filtered = filterFirmManagerNav(FIRM_NATIVE_NAV, true);
    expect(filtered).toEqual(FIRM_NATIVE_NAV);
  });

  it('filterFirmManagerNav hides manager-only routes for accountants', () => {
    const routes = collectRoutes(filterFirmManagerNav(FIRM_NATIVE_NAV, false));
    expect(routes).not.toContain('/firm/collaborateurs');
    expect(routes).not.toContain('/firm/billing/invoices');
    expect(routes).not.toContain('/firm/billing/payments');
    expect(routes).not.toContain('/firm/governance/dossier-time-profitability');
    expect(routes).not.toContain('/firm/governance/collaborator-rentability');
    expect(routes).not.toContain('/firm/affectation');
    expect(routes).toContain('/firm/governance/time-sheets');
    expect(routes).toContain('/firm/settings');
  });
});

function collectRoutes(items: NavItem[]): string[] {
  const routes: string[] = [];
  for (const item of items) {
    if (item.route) {
      routes.push(item.route);
    }
    for (const child of item.children ?? []) {
      if (child.route) {
        routes.push(child.route);
      }
    }
  }
  return routes;
}
