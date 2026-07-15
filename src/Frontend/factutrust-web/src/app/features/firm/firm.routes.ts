import { Routes } from '@angular/router';
import { firmNativeGuard, accountingFirmsFeatureGuard } from '@core/guards/accounting-firms.guard';

export const FIRM_ROUTES: Routes = [
  {
    path: '',
    canActivate: [accountingFirmsFeatureGuard, firmNativeGuard],
    children: [
      {
        path: 'dashboard',
        loadComponent: () => import('./firm-dashboard/firm-dashboard.component').then(m => m.FirmDashboardComponent),
        title: 'Cabinet — Tableau de bord'
      },
      {
        path: 'clients',
        loadComponent: () => import('./firm-clients/firm-clients.component').then(m => m.FirmClientsComponent),
        title: 'Cabinet — Dossiers clients'
      },
      {
        path: 'fiscal-schedule',
        loadComponent: () =>
          import('../accounting/fiscal-schedule/fiscal-schedule.component').then(m => m.FiscalScheduleComponent),
        data: { fiscalScheduleScope: 'firm' },
        title: 'Cabinet - Echeancier fiscal'
      },
      {
        path: 'open/:tenantId',
        loadComponent: () =>
          import('./firm-open-dossier/firm-open-dossier.component').then(m => m.FirmOpenDossierComponent),
        title: 'Cabinet — Ouverture dossier'
      },
      {
        path: 'invitations',
        loadComponent: () => import('./firm-invitations/firm-invitations.component').then(m => m.FirmInvitationsComponent),
        title: 'Cabinet — Invitations'
      },
      {
        path: 'settings',
        loadComponent: () => import('./firm-settings/firm-settings.component').then(m => m.FirmSettingsComponent),
        title: 'Cabinet — Paramètres'
      },
      {
        path: 'settings/users',
        loadComponent: () => import('./firm-settings/firm-users.component').then(m => m.FirmUsersComponent),
        title: 'Cabinet — Utilisateurs'
      },
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' }
    ]
  }
];
