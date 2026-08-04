import { Routes } from '@angular/router';
import { permissionGuard } from '@core/guards/permission.guard';
import { PERMISSIONS } from '@core/config/permission-keys';

export const PURCHASE_RECEIPTS_ROUTES: Routes = [
  {
    path: '',
    canActivate: [permissionGuard],
    data: { permissions: [PERMISSIONS.purchaseReceipts.read] },
    loadComponent: () =>
      import('./pages/purchase-receipt-list/purchase-receipt-list.component').then(
        m => m.PurchaseReceiptListComponent
      ),
    title: 'Bons de réception - InstaFact'
  },
  {
    path: 'new',
    canActivate: [permissionGuard],
    data: { permissions: [PERMISSIONS.purchaseReceipts.create] },
    loadComponent: () =>
      import('./pages/purchase-receipt-form/purchase-receipt-form.component').then(
        m => m.PurchaseReceiptFormComponent
      ),
    title: 'Nouveau bon de réception - InstaFact'
  },
  {
    path: ':id/edit',
    canActivate: [permissionGuard],
    data: { permissions: [PERMISSIONS.purchaseReceipts.update] },
    loadComponent: () =>
      import('./pages/purchase-receipt-form/purchase-receipt-form.component').then(
        m => m.PurchaseReceiptFormComponent
      ),
    title: 'Modifier bon de réception - InstaFact'
  },
  {
    path: ':id',
    loadComponent: () =>
      import('./pages/purchase-receipt-detail/purchase-receipt-detail.component').then(
        m => m.PurchaseReceiptDetailComponent
      ),
    title: 'Détail bon de réception - InstaFact'
  }
];
