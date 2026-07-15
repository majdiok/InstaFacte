import { Routes } from '@angular/router';
import { permissionGuard } from '@core/guards/permission.guard';
import { PERMISSIONS } from '@core/config/permission-keys';

export const TRANSFERS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./transfer-list/transfer-list.component').then(m => m.TransferListComponent)
  },
  {
    path: 'create',
    canActivate: [permissionGuard],
    data: { permissions: [PERMISSIONS.stockTransfers.create] },
    loadComponent: () =>
      import('./transfer-create/transfer-create.component').then(m => m.TransferCreateComponent)
  },
  {
    path: ':id',
    loadComponent: () =>
      import('./transfer-detail/transfer-detail.component').then(m => m.TransferDetailComponent)
  }
];
