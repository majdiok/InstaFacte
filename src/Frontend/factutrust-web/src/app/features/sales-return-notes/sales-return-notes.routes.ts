import { Routes } from '@angular/router';
import { permissionGuard } from '@core/guards/permission.guard';
import { PERMISSIONS } from '@core/config/permission-keys';

export const SALES_RETURN_NOTES_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./pages/sales-return-note-list.component').then(m => m.SalesReturnNoteListComponent),
    title: 'Bons de retour - InstaFact'
  },
  {
    path: 'new',
    canActivate: [permissionGuard],
    data: { permissions: [PERMISSIONS.returnNotes.create] },
    loadComponent: () =>
      import('./pages/sales-return-note-form.component').then(m => m.SalesReturnNoteFormComponent),
    title: 'Nouveau bon de retour - InstaFact'
  },
  {
    path: ':id',
    loadComponent: () =>
      import('./pages/sales-return-note-detail.component').then(m => m.SalesReturnNoteDetailComponent),
    title: 'Détail bon de retour - InstaFact'
  }
];
