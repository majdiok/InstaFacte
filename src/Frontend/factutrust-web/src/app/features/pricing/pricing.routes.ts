import { Routes } from '@angular/router';
import { permissionGuard } from '@core/guards/permission.guard';
import { PERMISSIONS } from '@core/config/permission-keys';

export const PRICING_ROUTES: Routes = [
  {
    path: '',
    redirectTo: '/products',
    pathMatch: 'full'
  },
  {
    path: 'promotions',
    redirectTo: '/settings/promotions',
    pathMatch: 'full'
  },
  {
    path: 'payment-terms',
    redirectTo: '/settings/payment-terms',
    pathMatch: 'full'
  },
  {
    path: ':id',
    redirectTo: '/products',
    pathMatch: 'full'
  }
];
