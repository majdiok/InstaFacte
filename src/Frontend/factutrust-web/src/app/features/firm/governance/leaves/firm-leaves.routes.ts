import { Routes } from '@angular/router';
import { firmManagerGuard } from '@core/guards/firm-manager.guard';

export const FIRM_LEAVES_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./firm-leaves-shell.component').then(m => m.FirmLeavesShellComponent),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'overview' },
      {
        path: 'overview',
        loadComponent: () =>
          import('./firm-leaves-overview.component').then(m => m.FirmLeavesOverviewComponent),
        title: 'Cabinet — Congés · Vue d’ensemble'
      },
      {
        path: 'calendar',
        loadComponent: () =>
          import('./firm-leaves-calendar.component').then(m => m.FirmLeavesCalendarComponent),
        title: 'Cabinet — Congés · Calendrier'
      },
      {
        path: 'requests',
        loadComponent: () =>
          import('./firm-leaves-requests.component').then(m => m.FirmLeavesRequestsComponent),
        title: 'Cabinet — Congés · Demandes'
      },
      {
        path: 'validation',
        canActivate: [firmManagerGuard],
        loadComponent: () =>
          import('./firm-leaves-validation.component').then(m => m.FirmLeavesValidationComponent),
        title: 'Cabinet — Congés · Validation'
      },
      {
        path: 'balances',
        loadComponent: () =>
          import('./firm-leaves-balances.component').then(m => m.FirmLeavesBalancesComponent),
        title: 'Cabinet — Congés · Soldes'
      },
      {
        path: 'types',
        canActivate: [firmManagerGuard],
        loadComponent: () =>
          import('./firm-leaves-types.component').then(m => m.FirmLeavesTypesComponent),
        title: 'Cabinet — Congés · Types'
      },
      {
        path: 'settings',
        canActivate: [firmManagerGuard],
        loadComponent: () =>
          import('./firm-leaves-settings.component').then(m => m.FirmLeavesSettingsComponent),
        title: 'Cabinet — Congés · Paramètres'
      }
    ]
  }
];
