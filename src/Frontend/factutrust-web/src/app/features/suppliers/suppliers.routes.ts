import { Routes } from '@angular/router';
import { permissionGuard } from '@core/guards/permission.guard';
import { PERMISSIONS } from '@core/config/permission-keys';

export const SUPPLIERS_ROUTES: Routes = [
    {
        path: '',
        loadComponent: () => import('./supplier-list/supplier-list.component').then(m => m.SupplierListComponent),
        title: 'Fournisseurs - InstaFact'
    },
    {
        path: 'new',
        canActivate: [permissionGuard],
        data: { permissions: [PERMISSIONS.suppliers.create] },
        loadComponent: () => import('./supplier-form/supplier-form.component').then(m => m.SupplierFormComponent),
        title: 'Nouveau fournisseur - InstaFact'
    },
    {
        path: ':id',
        loadComponent: () => import('./supplier-detail/supplier-detail.component').then(m => m.SupplierDetailComponent),
        title: 'Détail fournisseur - InstaFact'
    },
    {
        path: ':id/edit',
        canActivate: [permissionGuard],
        data: { permissions: [PERMISSIONS.suppliers.update] },
        loadComponent: () => import('./supplier-form/supplier-form.component').then(m => m.SupplierFormComponent),
        title: 'Modifier fournisseur - InstaFact'
    }
];
