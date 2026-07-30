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
    path: 'promotions',
    canActivate: [permissionGuard],
    data: { permissions: [PERMISSIONS.pricing.read] },
    loadComponent: () =>
      import('@features/pricing/promotions/promotions.component').then(m => m.PromotionsComponent),
    title: 'Promotions - InstaFact'
  },
  {
    path: 'payment-terms',
    canActivate: [permissionGuard],
    data: { permissions: [PERMISSIONS.pricing.read] },
    loadComponent: () =>
      import('@features/pricing/payment-terms/payment-terms.component').then(
        m => m.PaymentTermsComponent
      ),
    title: 'Conditions de règlement - InstaFact'
  },
  {
    // Après les chemins fixes, sinon « promotions » serait pris pour un identifiant.
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
