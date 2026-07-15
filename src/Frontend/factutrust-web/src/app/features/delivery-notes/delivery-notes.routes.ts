import { Routes } from '@angular/router';
import { permissionGuard } from '@core/guards/permission.guard';
import { PERMISSIONS } from '@core/config/permission-keys';

export const DELIVERY_NOTES_ROUTES: Routes = [
    {
        path: '',
        loadComponent: () => import('./pages/delivery-note-list/delivery-note-list.component').then(m => m.DeliveryNoteListComponent),
        title: 'Bons de Livraison - InstaFact'
    },
    {
        path: 'new',
        canActivate: [permissionGuard],
        data: { permissions: [PERMISSIONS.deliveryNotes.create] },
        loadComponent: () => import('./pages/delivery-note-form/delivery-note-form.component').then(m => m.DeliveryNoteFormComponent),
        title: 'Nouveau bon de livraison - InstaFact'
    },
    {
        path: ':id',
        loadComponent: () => import('./pages/delivery-note-detail/delivery-note-detail.component').then(m => m.DeliveryNoteDetailComponent),
        title: 'Détail bon de livraison - InstaFact'
    }
];
