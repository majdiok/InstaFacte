import { Routes } from '@angular/router';
import { permissionGuard } from '@core/guards/permission.guard';
import { PERMISSIONS } from '@core/config/permission-keys';

export const VARIANT_AXES_ROUTES: Routes = [
  {
    path: '',
    canActivate: [permissionGuard],
    data: { permissions: [PERMISSIONS.products.read] },
    loadComponent: () =>
      import('./product-attribute-list/product-attribute-list.component').then(
        m => m.ProductAttributeListComponent
      ),
    title: 'Axes de variantes - InstaFact'
  },
  {
    path: 'new',
    canActivate: [permissionGuard],
    data: { permissions: [PERMISSIONS.products.create] },
    loadComponent: () =>
      import('./product-attribute-form/product-attribute-form.component').then(
        m => m.ProductAttributeFormComponent
      ),
    title: 'Nouvel axe de variantes - InstaFact'
  },
  {
    path: ':id/edit',
    canActivate: [permissionGuard],
    data: { permissions: [PERMISSIONS.products.update] },
    loadComponent: () =>
      import('./product-attribute-form/product-attribute-form.component').then(
        m => m.ProductAttributeFormComponent
      ),
    title: 'Modifier l\'axe de variantes - InstaFact'
  }
];
