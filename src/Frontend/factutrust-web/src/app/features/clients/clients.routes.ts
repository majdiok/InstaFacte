import { Routes } from '@angular/router';
import { permissionGuard } from '@core/guards/permission.guard';
import { PERMISSIONS } from '@core/config/permission-keys';

export const CLIENTS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./client-list/client-list.component').then(m => m.ClientListComponent),
    title: 'Clients - InstaFact'
  },
  {
    path: 'new',
    canActivate: [permissionGuard],
    data: { permissions: [PERMISSIONS.clients.create] },
    loadComponent: () => import('./client-form/client-form.component').then(m => m.ClientFormComponent),
    title: 'Nouveau client - InstaFact'
  },
  {
    path: ':id',
    loadComponent: () => import('./client-detail/client-detail.component').then(m => m.ClientDetailComponent),
    title: 'Détail client - InstaFact'
  },
  {
    path: ':id/edit',
    canActivate: [permissionGuard],
    data: { permissions: [PERMISSIONS.clients.update] },
    loadComponent: () => import('./client-form/client-form.component').then(m => m.ClientFormComponent),
    title: 'Modifier client - InstaFact'
  }
];
