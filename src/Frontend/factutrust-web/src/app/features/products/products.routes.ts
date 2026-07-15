import { Routes } from '@angular/router';
import { permissionGuard } from '@core/guards/permission.guard';
import { PERMISSIONS } from '@core/config/permission-keys';

export const PRODUCTS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./product-list/product-list.component').then(m => m.ProductListComponent),
    title: 'Produits - InstaFact'
  },
  {
    path: 'new',
    canActivate: [permissionGuard],
    data: { permissions: [PERMISSIONS.products.create] },
    loadComponent: () => import('./product-form/product-form.component').then(m => m.ProductFormComponent),
    title: 'Nouveau produit - InstaFact'
  },
  {
    path: ':id/view',
    canActivate: [permissionGuard],
    data: { permissions: [PERMISSIONS.products.read] },
    loadComponent: () => import('./product-detail/product-detail.component').then(m => m.ProductDetailComponent),
    title: 'Détails produit - InstaFact'
  },
  {
    path: ':id/edit',
    canActivate: [permissionGuard],
    data: { permissions: [PERMISSIONS.products.update] },
    loadComponent: () => import('./product-form/product-form.component').then(m => m.ProductFormComponent),
    title: 'Modifier produit - InstaFact'
  }
];
