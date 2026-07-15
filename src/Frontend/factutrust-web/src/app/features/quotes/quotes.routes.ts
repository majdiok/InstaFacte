import { Routes } from '@angular/router';
import { permissionGuard } from '@core/guards/permission.guard';
import { PERMISSIONS } from '@core/config/permission-keys';

export const QUOTES_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('@features/quotes/quote-list/quote-list.component').then(m => m.QuoteListComponent),
    title: 'Devis - InstaFact'
  },
  {
    path: 'new',
    canActivate: [permissionGuard],
    data: { permissions: [PERMISSIONS.quotes.create] },
    loadComponent: () =>
      import('@features/quotes/quote-form/quote-form.component').then(m => m.QuoteFormComponent),
    title: 'Nouveau devis - InstaFact'
  },
  {
    path: ':id',
    loadComponent: () =>
      import('@features/quotes/quote-detail/quote-detail.component').then(m => m.QuoteDetailComponent),
    title: 'Détail devis - InstaFact'
  }
];
