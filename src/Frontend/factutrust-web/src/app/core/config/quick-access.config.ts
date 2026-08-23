import { PERMISSIONS } from './permission-keys';
import { AuthService } from '../services/auth.service';
import {
  FIRM_DELEGATED_QUICK_ACCESS,
  FIRM_NATIVE_QUICK_ACCESS
} from './firm-navigation.registry';

export type QuickAccessSection = 'navigation' | 'create';

export type QuickAccessAction = 'returnToFirm';

export interface QuickAccessItem {
  label: string;
  icon: string;
  route?: string;
  action?: QuickAccessAction;
  permission: string;
  section: QuickAccessSection;
}

export const QUICK_ACCESS_ITEMS: QuickAccessItem[] = [
  {
    label: 'Ajouter une facture de vente',
    icon: 'fa-solid fa-file-invoice',
    route: '/invoices/new',
    permission: PERMISSIONS.invoices.create,
    section: 'create'
  },
  {
    label: 'Ajouter un avoir de vente',
    icon: 'fa-solid fa-receipt',
    route: '/invoices/credit-note/new',
    permission: PERMISSIONS.invoices.create,
    section: 'create'
  },
  {
    label: 'Mes clients',
    icon: 'fa-solid fa-users',
    route: '/clients',
    permission: PERMISSIONS.clients.read,
    section: 'navigation'
  },
  {
    label: 'Rapports',
    icon: 'fa-solid fa-chart-column',
    route: '/reports',
    permission: PERMISSIONS.reports.view,
    section: 'navigation'
  },
  {
    label: 'Ajouter un devis',
    icon: 'fa-solid fa-file-lines',
    route: '/quotes/new',
    permission: PERMISSIONS.quotes.create,
    section: 'create'
  },
  {
    label: 'Ajouter un bon de livraison',
    icon: 'fa-solid fa-truck',
    route: '/delivery-notes/new',
    permission: PERMISSIONS.deliveryNotes.create,
    section: 'create'
  },
  {
    label: 'Ajouter un bon de retour',
    icon: 'fa-solid fa-rotate-left',
    route: '/return-notes/new',
    permission: PERMISSIONS.returnNotes.create,
    section: 'create'
  },
  {
    label: "Ajouter un bon d'entrée",
    icon: 'fa-solid fa-arrow-down',
    route: '/stock/entries/new',
    permission: PERMISSIONS.stockVouchers.create,
    section: 'create'
  },
  {
    label: 'Ajouter un bon de sortie',
    icon: 'fa-solid fa-arrow-up',
    route: '/stock/issues/new',
    permission: PERMISSIONS.stockVouchers.create,
    section: 'create'
  },
  {
    label: 'Paiements',
    icon: 'fa-solid fa-credit-card',
    route: '/payments',
    permission: PERMISSIONS.payments.read,
    section: 'navigation'
  }
];

export function getVisibleQuickAccessItems(authService: AuthService): QuickAccessItem[] {
  if (authService.isAccountingFirm()) {
    if (authService.isDelegatedMode()) {
      return FIRM_DELEGATED_QUICK_ACCESS.filter(
        item => !item.permission || authService.hasPermission(item.permission)
      );
    }
    return [...FIRM_NATIVE_QUICK_ACCESS];
  }

  return QUICK_ACCESS_ITEMS.filter(item => authService.hasPermission(item.permission));
}
