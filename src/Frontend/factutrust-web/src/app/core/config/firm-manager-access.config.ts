import { NavItem, NavSubItem } from './app-navigation.registry';

/** Routes réservées au FirmManager (responsable cabinet) en mode cabinet natif. */
export const FIRM_MANAGER_ROUTE_PREFIXES = [
  '/firm/collaborateurs',
  '/firm/governance/dossier-time-profitability',
  '/firm/governance/collaborator-rentability',
  '/firm/billing',
  '/firm/affectation',
  '/firm/clients/new',
  '/firm/settings/activity-codes',
  '/firm/governance/leaves/validation',
  '/firm/governance/leaves/types',
  '/firm/governance/leaves/settings'
] as const;

function normalizePath(path: string): string {
  const q = path.indexOf('?');
  const h = path.indexOf('#');
  let end = path.length;
  if (q >= 0) {
    end = Math.min(end, q);
  }
  if (h >= 0) {
    end = Math.min(end, h);
  }
  return path.slice(0, end);
}

export function isFirmManagerRoute(path: string | undefined): boolean {
  if (!path) {
    return false;
  }
  const normalized = normalizePath(path);
  return FIRM_MANAGER_ROUTE_PREFIXES.some(
    prefix => normalized === prefix || normalized.startsWith(`${prefix}/`)
  );
}

function filterSubItems(items: NavSubItem[], isManager: boolean): NavSubItem[] {
  if (isManager) {
    return items;
  }
  return items
    .filter(item => !isFirmManagerRoute(item.route))
    .map(item =>
      item.children?.length
        ? { ...item, children: filterSubItems(item.children, isManager) }
        : item
    )
    .filter(item => !item.children || item.children.length > 0);
}

/** Masque les entrées de menu dont la route est réservée au responsable cabinet. */
export function filterFirmManagerNav(items: NavItem[], isManager: boolean): NavItem[] {
  if (isManager) {
    return items;
  }
  return items
    .map(item => {
      if (item.children?.length) {
        const children = filterSubItems(item.children, isManager);
        if (children.length === 0) {
          return null;
        }
        return { ...item, children };
      }
      return !isFirmManagerRoute(item.route) ? item : null;
    })
    .filter((item): item is NavItem => item !== null);
}
