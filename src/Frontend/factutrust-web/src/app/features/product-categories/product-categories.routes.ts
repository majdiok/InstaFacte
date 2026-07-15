import { Routes } from '@angular/router';
import { permissionGuard } from '@core/guards/permission.guard';
import { PERMISSIONS } from '@core/config/permission-keys';

export const PRODUCT_CATEGORIES_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./product-category-list/product-category-list.component').then(m => m.ProductCategoryListComponent),
    title: 'Catégories - InstaFact'
  },
  {
    path: 'new',
    canActivate: [permissionGuard],
    data: { permissions: [PERMISSIONS.products.create] },
    loadComponent: () => import('./product-category-form/product-category-form.component').then(m => m.ProductCategoryFormComponent),
    title: 'Nouvelle catégorie - InstaFact'
  },
  {
    path: ':id/edit',
    canActivate: [permissionGuard],
    data: { permissions: [PERMISSIONS.products.update] },
    loadComponent: () => import('./product-category-form/product-category-form.component').then(m => m.ProductCategoryFormComponent),
    title: 'Modifier la catégorie - InstaFact'
  }
];
