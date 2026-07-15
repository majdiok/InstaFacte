import { Routes } from '@angular/router';
import { permissionGuard } from '@core/guards/permission.guard';
import { PERMISSIONS } from '@core/config/permission-keys';

export const PURCHASE_ORDERS_ROUTES: Routes = [
    {
        path: '',
        loadComponent: () => import('./purchase-order-list/purchase-order-list.component').then(m => m.PurchaseOrderListComponent),
        title: 'Bons de commande - InstaFact'
    },
    {
        path: 'new',
        canActivate: [permissionGuard],
        data: { permissions: [PERMISSIONS.purchaseOrders.create] },
        loadComponent: () => import('./purchase-order-create/purchase-order-create.component').then(m => m.PurchaseOrderCreateComponent),
        title: 'Nouveau bon de commande - InstaFact'
    },
    {
        path: ':id/edit',
        canActivate: [permissionGuard],
        data: { permissions: [PERMISSIONS.purchaseOrders.update] },
        loadComponent: () => import('./purchase-order-edit/purchase-order-edit.component').then(m => m.PurchaseOrderEditComponent),
        title: 'Modifier bon de commande - InstaFact'
    },
    {
        path: ':id/receive',
        canActivate: [permissionGuard],
        data: { permissions: [PERMISSIONS.purchaseOrders.update] },
        loadComponent: () => import('./purchase-order-receive/purchase-order-receive.component').then(m => m.PurchaseOrderReceiveComponent),
        title: 'Réception marchandise - InstaFact'
    },
    {
        path: ':id',
        loadComponent: () => import('./purchase-order-detail/purchase-order-detail.component').then(m => m.PurchaseOrderDetailComponent),
        title: 'Détail bon de commande - InstaFact'
    }
];

