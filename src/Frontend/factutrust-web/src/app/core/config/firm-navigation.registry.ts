import { AppModule as M } from '@core/models/app-module';

import { NavItem, NavSubItem } from './app-navigation.registry';



/** Cabinet home navigation (no client dossier selected). */

export const FIRM_NATIVE_NAV: NavItem[] = [

  {

    label: 'Tableau de bord',

    icon: 'fa-solid fa-gauge-high',

    route: '/firm/dashboard'

  },

  {

    label: 'Mes dossiers clients',

    icon: 'fa-solid fa-briefcase',

    route: '/firm/clients'

  },

  {

    label: 'Échéancier fiscal',

    icon: 'fa-solid fa-calendar-days',

    route: '/firm/fiscal-schedule'

  },

  {

    label: 'Dossiers permanents',

    route: '/firm/governance/permanent-files',

    icon: 'fa-solid fa-folder-open'

  },

  {

    label: 'Affectation des dossiers',

    route: '/firm/affectation',

    icon: 'fa-solid fa-user-tag'

  },

  {

    label: 'Feuilles de temps',

    route: '/firm/governance/time-sheets',

    icon: 'fa-solid fa-clock'

  },

  {

    label: 'Rentabilité de collaborateurs',

    icon: 'fa-solid fa-chart-pie',

    children: [

      {

        label: 'Feuilles de temps et rentabilité',

        route: '/firm/governance/dossier-time-profitability',

        icon: 'fa-solid fa-chart-line'

      },

      {

        label: 'Rentabilité collaborateurs',

        route: '/firm/governance/collaborator-rentability',

        icon: 'fa-solid fa-chart-pie'

      }

    ]

  },

  {

    label: 'Notes de frais dirigeants',

    route: '/firm/governance/expense-notes',

    icon: 'fa-solid fa-receipt'

  },

  {

    label: 'Suivi social',

    route: '/firm/governance/social',

    icon: 'fa-solid fa-users'

  },

  {

    label: 'Invitations',

    icon: 'fa-solid fa-envelope-open-text',

    route: '/firm/invitations'

  },

  {

    label: 'Échanges',

    icon: 'fa-solid fa-comments',

    route: '/firm/exchanges'

  },

  {

    label: 'Mon cabinet',

    icon: 'fa-solid fa-building-columns',

    children: [

      {

        label: 'Collaborateurs',

        route: '/firm/collaborateurs',

        icon: 'fa-solid fa-users'

      },

      {

        label: 'Paramètres cabinet',

        route: '/firm/settings',

        icon: 'fa-solid fa-gear'

      },

      {

        label: 'Mon profil',

        route: '/settings/profile',

        icon: 'fa-solid fa-user'

      }

    ]

  }

];



/** Labels of top-level sections available in delegated client context. */

export const DELEGATED_SECTION_LABELS = new Set([

  'Comptabilité',

  'Fiscal / TEJ',

  'RH & Paie',

  'Ventes',

  'Achats',

  'Trésorerie'

]);



/** Read-only routes under sales/purchases/treasury in delegated mode. */

export const DELEGATED_READ_ONLY_ROUTE_PREFIXES = [

  '/invoices',

  '/supplier-invoices',

  '/payments'

];



/** Sidebar submenu routes allowed for accounting firms in delegated mode — Ventes. */

export const DELEGATED_FIRM_VENTES_ALLOWED_ROUTES = new Set([

  '/invoices',

  '/invoices/unpaid',

  '/reports/client-balances',

  '/reports/sales'

]);



/** Sidebar submenu routes allowed for accounting firms in delegated mode — Achats. */

export const DELEGATED_FIRM_ACHATS_ALLOWED_ROUTES = new Set([

  '/supplier-invoices',

  '/supplier-invoices/unpaid',

  '/reports/supplier-balances',

  '/reports/purchases'

]);



const DELEGATED_FIRM_BLOCKED_SALES_PURCHASES_PREFIXES = [

  '/quotes',

  '/delivery-notes',

  '/suppliers',

  '/purchase-orders',

  '/reports/analytics',

  '/reports/purchases-analytics',

  '/reports/profit',

  '/reports/sales-by-line',

  '/reports/product-sales-analytics',

  '/ai-assistant/ventes',

  '/ai-assistant/achats'

];



function pathMatchesPrefix(path: string, prefix: string): boolean {

  return path === prefix || path.startsWith(prefix + '/');

}



export function filterDelegatedFirmSectionChildren(

  sectionLabel: string,

  children: NavSubItem[]

): NavSubItem[] {

  if (sectionLabel === 'Ventes') {

    return children.filter(c => c.route && DELEGATED_FIRM_VENTES_ALLOWED_ROUTES.has(c.route));

  }

  if (sectionLabel === 'Achats') {

    return children.filter(c => c.route && DELEGATED_FIRM_ACHATS_ALLOWED_ROUTES.has(c.route));

  }

  return children;

}



/**

 * True when an accounting firm in delegated mode must not access this frontend route

 * (sales/purchases areas outside the strict allowlist).

 */

export function isDelegatedFirmBlockedSalesPurchasesRoute(path: string): boolean {

  const normalized = path.split('?')[0].split('#')[0];

  if (

    DELEGATED_FIRM_BLOCKED_SALES_PURCHASES_PREFIXES.some(p => pathMatchesPrefix(normalized, p))

  ) {

    return true;

  }

  if (normalized === '/reports' || normalized.startsWith('/reports/')) {

    if (pathMatchesPrefix(normalized, '/reports/sales')) {

      return false;

    }

    if (pathMatchesPrefix(normalized, '/reports/purchases')) {

      return false;

    }

    return true;

  }

  return false;

}



export function isDelegatedReadOnlyRoute(path: string): boolean {

  return DELEGATED_READ_ONLY_ROUTE_PREFIXES.some(p => path === p || path.startsWith(p + '/'));

}

/** True when an accounting firm user operates on a client dossier in delegated read-only context. */
export function isFirmDelegatedReadonly(auth: {
  isAccountingFirm: () => boolean;
  isDelegatedMode: () => boolean;
}): boolean {
  return auth.isAccountingFirm() && auth.isDelegatedMode();
}



/** Quick access items shown to accounting firms in native mode. */

export const FIRM_NATIVE_QUICK_ACCESS = [

  {

    label: 'Mes dossiers',

    icon: 'fa-solid fa-briefcase',

    route: '/firm/clients',

    permission: '',

    section: 'navigation' as const

  },

  {

    label: 'Invitations en attente',

    icon: 'fa-solid fa-envelope-open-text',

    route: '/firm/invitations',

    permission: '',

    section: 'navigation' as const

  }

];



/** Quick access when operating inside a client dossier. */

export const FIRM_DELEGATED_QUICK_ACCESS = [

  {

    label: 'Journal comptable',

    icon: 'fa-solid fa-book',

    route: '/accounting/journal',

    permission: 'accounting:read',

    section: 'navigation' as const

  },

  {

    label: 'Plan comptable',

    icon: 'fa-solid fa-sitemap',

    route: '/accounting/chart',

    permission: 'accounting:read',

    section: 'navigation' as const

  },

  {

    label: 'Cycles de paie',

    icon: 'fa-solid fa-calendar-check',

    route: '/payroll/runs',

    permission: 'payroll:read',

    section: 'navigation' as const

  },

  {

    label: 'Retour au cabinet',

    icon: 'fa-solid fa-arrow-left',

    action: 'returnToFirm' as const,

    permission: '',

    section: 'navigation' as const

  }

];

function isFirmGovernanceNavRoute(route?: string): boolean {
  if (!route) {
    return false;
  }
  return (
    route.startsWith('/firm/governance') ||
    route === '/firm/affectation' ||
    route.startsWith('/firm/affectation/')
  );
}

/** Masque les entrées Gouvernance lorsque le feature flag cabinet est désactivé. */
export function filterFirmGovernanceNav(items: NavItem[], governanceEnabled: boolean): NavItem[] {
  if (governanceEnabled) {
    return items;
  }

  return items
    .filter(item => !isFirmGovernanceNavRoute(item.route))
    .map(item =>
      item.children?.length
        ? {
            ...item,
            children: item.children.filter(c => !isFirmGovernanceNavRoute(c.route))
          }
        : item
    )
    .filter(item => !item.children || item.children.length > 0);
}

