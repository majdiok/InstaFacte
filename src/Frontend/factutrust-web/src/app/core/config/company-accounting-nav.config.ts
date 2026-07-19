import { AppModule as M } from '../models/app-module';
import { AuthService } from '../services/auth.service';
import {
  ACCOUNTING_MODULES,
  AccountingModuleLink
} from './accounting-modules.config';
import { NavItem, NavSearchEntry, NavSubItem } from './app-navigation.registry';

/** Section États dans ACCOUNTING_MODULES — source des liens du hub « États comptables ». */
export const COMPANY_ACCOUNTING_ETATS_MODULE_TITLE = 'États';

export const COMPANY_ACCOUNTING_AI_ASSISTANT_ROUTE = '/ai-assistant/comptabilite';

export const COMPANY_ACCOUNTING_FIXED_ASSETS_ROUTE = '/accounting/fixed-assets';

const ETATS_MODULE = ACCOUNTING_MODULES.find(m => m.title === COMPANY_ACCOUNTING_ETATS_MODULE_TITLE);

const CONFIGURATION_ASSISTANT_LINK = ACCOUNTING_MODULES.find(m => m.title === 'Configuration')?.links.find(
  l => l.route === COMPANY_ACCOUNTING_AI_ASSISTANT_ROUTE
);

function accountingModuleLinkToNavSubItem(link: AccountingModuleLink): NavSubItem {
  return {
    label: link.label,
    route: link.route,
    icon: link.icon,
    modules: link.modules ?? [M.Accounting],
    permissionsAll: link.perms
  };
}

/** Routes autorisées pour la société (assistant, états, déclaration, immobilisations). */
export const COMPANY_ACCOUNTING_ALLOWED_ROUTE_PREFIXES: readonly string[] = [
  COMPANY_ACCOUNTING_AI_ASSISTANT_ROUTE,
  '/accounting/financial-statements',
  '/accounting/vat-declaration',
  COMPANY_ACCOUNTING_FIXED_ASSETS_ROUTE,
  ...(ETATS_MODULE?.links.map(l => l.route) ?? []),
  '/audit'
];

/** Préfixes de chemins soumis au guard comptabilité société. */
export const COMPANY_ACCOUNTING_GUARDED_ROUTE_PREFIXES: readonly string[] = [
  '/accounting',
  '/audit',
  COMPANY_ACCOUNTING_AI_ASSISTANT_ROUTE
];

/** Sous-menus Comptabilité affichés dans le sidebar pour une société connectée. */
export const COMPANY_ACCOUNTING_SIDEBAR_CHILDREN: NavSubItem[] = [
  ...(CONFIGURATION_ASSISTANT_LINK ? [accountingModuleLinkToNavSubItem(CONFIGURATION_ASSISTANT_LINK)] : []),
  {
    label: 'États comptables',
    route: '/accounting/financial-statements',
    icon: 'fa-solid fa-table-list',
    modules: [M.Accounting],
    permissionsAll: ['accounting:read']
  },
  {
    label: 'Déclaration mensuelle',
    route: '/accounting/vat-declaration',
    icon: 'fa-solid fa-file-invoice',
    modules: [M.Accounting],
    permissionsAll: ['accounting:read']
  },
  {
    label: 'Liste des immobilisations',
    route: COMPANY_ACCOUNTING_FIXED_ASSETS_ROUTE,
    icon: 'fa-solid fa-building',
    modules: [M.Accounting],
    permissionsAll: ['accounting:read']
  }
];

export function isCompanyAccountingRestricted(auth: AuthService): boolean {
  return !auth.isAccountingFirm();
}

/** Zone « Pièces justificatives » de la déclaration TVA : réservée au cabinet comptable. */
export function canShowVatDeclarationDocLinks(auth: AuthService): boolean {
  return !isCompanyAccountingRestricted(auth);
}

function pathMatchesPrefix(path: string, prefix: string): boolean {
  if (path === prefix) {
    return true;
  }
  return path.startsWith(prefix + '/');
}

export function isCompanyAllowedAccountingPath(path: string): boolean {
  if (path === '/accounting') {
    return true;
  }
  return COMPANY_ACCOUNTING_ALLOWED_ROUTE_PREFIXES.some(prefix => pathMatchesPrefix(path, prefix));
}

export function isAccountingGuardedPath(path: string): boolean {
  return COMPANY_ACCOUNTING_GUARDED_ROUTE_PREFIXES.some(prefix => pathMatchesPrefix(path, prefix));
}

export function applyCompanyAccountingSidebar(items: NavItem[]): NavItem[] {
  return items.map(item => {
    if (item.label !== 'Comptabilité' || !item.children?.length) {
      return item;
    }
    return { ...item, children: [...COMPANY_ACCOUNTING_SIDEBAR_CHILDREN] };
  });
}

export function filterCompanyAccountingSearchEntries(entries: NavSearchEntry[]): NavSearchEntry[] {
  return entries.filter(entry => isCompanyAllowedAccountingPath(entry.route));
}
