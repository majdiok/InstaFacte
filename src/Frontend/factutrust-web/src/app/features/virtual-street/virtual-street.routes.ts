import { Routes } from '@angular/router';
import { storefrontFeatureGuard } from './guards/storefront-feature.guard';

export const VIRTUAL_STREET_ROUTES: Routes = [
  {
    path: '',
    canActivate: [storefrontFeatureGuard],
    loadComponent: () =>
      import('./pages/street-page/street-page.component').then(m => m.StreetPageComponent),
    title: 'InstaFact — Visite virtuelle 3D'
  },
  {
    path: 'liste',
    canActivate: [storefrontFeatureGuard],
    loadComponent: () =>
      import('./pages/accessibility-list/accessibility-list.component').then(
        m => m.AccessibilityListComponent
      ),
    title: 'Liste des vitrines — InstaFact'
  },
  {
    path: 'checkout',
    canActivate: [storefrontFeatureGuard],
    loadComponent: () =>
      import('./pages/checkout-page/checkout-page.component').then(m => m.CheckoutPageComponent),
    title: 'Panier — InstaFact'
  },
  {
    path: ':slug',
    canActivate: [storefrontFeatureGuard],
    loadComponent: () =>
      import('./pages/storefront-detail-page/storefront-detail-page.component').then(
        m => m.StorefrontDetailPageComponent
      ),
    title: 'Vitrine — InstaFact'
  }
];
