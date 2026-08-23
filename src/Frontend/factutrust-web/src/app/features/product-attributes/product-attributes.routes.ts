import { Routes } from '@angular/router';
import { permissionGuard } from '@core/guards/permission.guard';
import { PERMISSIONS } from '@core/config/permission-keys';

export const PRODUCT_ATTRIBUTES_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./product-attribute-list/product-attribute-list.component').then(
        m => m.ProductAttributeListComponent
      ),
    title: 'Attributs produits - InstaFact'
  },
  {
    path: 'new',
    canActivate: [permissionGuard],
    data: { permissions: [PERMISSIONS.products.create] },
    loadComponent: () =>
      import('./product-attribute-form/product-attribute-form.component').then(
        m => m.ProductAttributeFormComponent
      ),
    title: 'Nouvel attribut - InstaFact'
  },
  {
    path: ':id/edit',
    canActivate: [permissionGuard],
    data: { permissions: [PERMISSIONS.products.update] },
    loadComponent: () =>
      import('./product-attribute-form/product-attribute-form.component').then(
        m => m.ProductAttributeFormComponent
      ),
    title: 'Modifier l\'attribut - InstaFact'
  }
];
