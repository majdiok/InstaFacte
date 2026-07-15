import { Routes } from '@angular/router';
import { permissionGuard } from '@core/guards/permission.guard';
import { PERMISSIONS } from '@core/config/permission-keys';

export const INVOICES_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./invoice-list/invoice-list.component').then(m => m.InvoiceListComponent),
    title: 'Factures - InstaFact'
  },
  {
    path: 'new',
    canActivate: [permissionGuard],
    loadComponent: () => import('./invoice-wizard/invoice-wizard.component').then(m => m.InvoiceWizardComponent),
    title: 'Nouvelle facture - InstaFact',
    data: {
      fullWidth: true,
      hideLayout: true,
      permissions: [PERMISSIONS.invoices.create]
    }
  },
  {
    path: 'new/draft/:draftId',
    canActivate: [permissionGuard],
    loadComponent: () => import('./invoice-wizard/invoice-wizard.component').then(m => m.InvoiceWizardComponent),
    title: 'Brouillon facture - InstaFact',
    data: {
      fullWidth: true,
      hideLayout: true,
      permissions: [PERMISSIONS.invoices.create]
    }
  },
  {
    path: 'unpaid',
    loadComponent: () => import('./invoice-list/invoice-list.component').then(m => m.InvoiceListComponent),
    title: 'Factures impayées - InstaFact',
    data: { unpaidOnly: true }
  },
  {
    path: 'unpaid/:id',
    loadComponent: () => import('./invoice-detail/invoice-detail.component').then(m => m.InvoiceDetailComponent),
    title: 'Détail facture - InstaFact',
    data: { unpaidOnly: true }
  },
  {
    path: ':id',
    loadComponent: () => import('./invoice-detail/invoice-detail.component').then(m => m.InvoiceDetailComponent),
    title: 'Détail facture - InstaFact'
  },
  {
    path: ':id/credit-note',
    canActivate: [permissionGuard],
    loadComponent: () => import('./invoice-wizard/invoice-wizard.component').then(m => m.InvoiceWizardComponent),
    title: "Facture d'avoir - InstaFact",
    data: {
      fullWidth: true,
      hideLayout: true,
      isCreditNote: true,
      permissions: [PERMISSIONS.invoices.create]
    }
  }
];
