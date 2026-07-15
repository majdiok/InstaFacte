import { Routes } from '@angular/router';
import { authGuard } from '@core/guards/auth.guard';
import { accountingFirmsFeatureGuard } from '@core/guards/accounting-firms.guard';

export const AUTH_ROUTES: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./login/login.component').then(m => m.LoginComponent),
    title: 'Connexion - InstaFact'
  },
  {
    path: 'select-warehouse',
    loadComponent: () =>
      import('./select-warehouse/select-warehouse.component').then(
        (m) => m.SelectWarehouseComponent
      ),
    canActivate: [authGuard],
    title: 'Choisir un entrepôt - InstaFact'
  },
  {
    path: 'register',
    loadComponent: () => import('./register/register.component').then(m => m.RegisterComponent),
    title: 'Inscription - InstaFact'
  },
  {
    path: 'register-firm',
    canActivate: [accountingFirmsFeatureGuard],
    loadComponent: () => import('./register-firm/register-firm.component').then(m => m.RegisterFirmComponent),
    title: 'Inscription cabinet - InstaFact'
  },
  {
    path: '',
    redirectTo: 'login',
    pathMatch: 'full'
  }
];
