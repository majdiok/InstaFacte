import { Routes } from '@angular/router';
import { permissionGuard } from '@core/guards/permission.guard';
import { PERMISSIONS } from '@core/config/permission-keys';

export const INVENTORY_ROUTES: Routes = [
    {
        path: '',
        loadComponent: () => import('./inventory-list/inventory-list.component')
            .then(m => m.InventoryListComponent),
        title: 'Inventaire - InstaFact'
    },
    {
        path: 'new',
        canActivate: [permissionGuard],
        data: { permissions: [PERMISSIONS.inventory.create] },
        loadComponent: () => import('./inventory-form/inventory-form.component')
            .then(m => m.InventoryFormComponent),
        title: 'Nouvel inventaire - InstaFact'
    },
    {
        path: 'wizard',
        canActivate: [permissionGuard],
        data: { permissions: [PERMISSIONS.inventory.read] },
        loadComponent: () => import('./inventory-wizard/inventory-wizard.component')
            .then(m => m.InventoryWizardComponent),
        title: 'Inventaire physique (mode assistant) - InstaFact'
    },
    {
        path: ':id',
        loadComponent: () => import('./inventory-detail/inventory-detail.component')
            .then(m => m.InventoryDetailComponent),
        title: 'Détail inventaire - InstaFact'
    }
];
