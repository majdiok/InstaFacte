import { Routes } from '@angular/router';
import { permissionGuard } from '@core/guards/permission.guard';
import { PERMISSIONS } from '@core/config/permission-keys';

export const RECURRING_CONTRACTS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./pages/contract-list/contract-list.component').then(m => m.ContractListComponent),
    title: 'Contrats récurrents - InstaFact'
  },
  {
    path: 'pending-drafts',
    loadComponent: () =>
      import('./pages/pending-drafts/pending-drafts.component').then(m => m.PendingDraftsComponent),
    title: 'Brouillons récurrents - InstaFact'
  },
  {
    path: 'new',
    canActivate: [permissionGuard],
    data: { permissions: [PERMISSIONS.recurringContracts.create] },
    loadComponent: () =>
      import('./pages/contract-form/contract-form.component').then(m => m.ContractFormComponent),
    title: 'Nouveau contrat - InstaFact'
  },
  {
    path: ':id/edit',
    canActivate: [permissionGuard],
    data: { permissions: [PERMISSIONS.recurringContracts.update] },
    loadComponent: () =>
      import('./pages/contract-form/contract-form.component').then(m => m.ContractFormComponent),
    title: 'Modifier contrat - InstaFact'
  },
  {
    path: ':id',
    loadComponent: () =>
      import('./pages/contract-detail/contract-detail.component').then(m => m.ContractDetailComponent),
    title: 'Détail contrat - InstaFact'
  }
];
