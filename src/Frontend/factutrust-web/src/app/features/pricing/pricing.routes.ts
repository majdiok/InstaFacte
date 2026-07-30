import { Routes } from '@angular/router';
import { permissionGuard } from '@core/guards/permission.guard';
import { PERMISSIONS } from '@core/config/permission-keys';

export const PRICING_ROUTES: Routes = [
  {
    path: '',
    canActivate: [permissionGuard],
    data: { permissions: [PERMISSIONS.pricing.read] },
    loadComponent: () =>
      import('@features/pricing/price-list-list/price-list-list.component').then(
        m => m.PriceListListComponent
      ),
    title: 'Grilles tarifaires - InstaFact'
  },
  {
    path: ':id',
    canActivate: [permissionGuard],
    data: { permissions: [PERMISSIONS.pricing.read] },
    loadComponent: () =>
      import('@features/pricing/price-list-detail/price-list-detail.component').then(
        m => m.PriceListDetailComponent
      ),
    title: 'Grille tarifaire - InstaFact'
  }
];
