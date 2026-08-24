import { Routes } from '@angular/router';
import { authGuard } from '@core/guards/auth.guard';
import { portalRoleGuard } from '@core/guards/portal-role.guard';
import { PortalLayoutComponent } from './portal-layout.component';

export const PORTAL_ROUTES: Routes = [
  {
    path: '',
    component: PortalLayoutComponent,
    canActivate: [authGuard, portalRoleGuard],
    children: [
      { path: '', loadComponent: () => import('./portal-dashboard.component').then(m => m.PortalDashboardComponent) },
      { path: 'invoices', loadComponent: () => import('./portal-invoices.component').then(m => m.PortalInvoicesComponent) },
      { path: 'invoices/:id', loadComponent: () => import('./portal-invoice-detail.component').then(m => m.PortalInvoiceDetailComponent) },
      { path: 'payments', loadComponent: () => import('./portal-payments.component').then(m => m.PortalPaymentsComponent) },
      { path: 'statement', loadComponent: () => import('./portal-statement.component').then(m => m.PortalStatementComponent) },
      { path: 'profile', loadComponent: () => import('./portal-profile.component').then(m => m.PortalProfileComponent) }
    ]
  }
];
