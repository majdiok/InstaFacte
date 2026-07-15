import { Routes } from '@angular/router';

export const WITHHOLDING_TAX_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'dashboard'
  },
  {
    path: 'dashboard',
    loadComponent: () => import('./withholding-dashboard/withholding-dashboard.component').then(m => m.WithholdingDashboardComponent),
    title: 'Tableau de bord retenue à la source - InstaFact'
  },
  {
    path: 'tej-export',
    loadComponent: () => import('./tej-export/tej-export.component').then(m => m.TejExportComponent),
    title: 'Export TEJ - InstaFact'
  },
  {
    path: 'settings',
    loadComponent: () => import('./withholding-types-settings/withholding-types-settings.component').then(m => m.WithholdingTypesSettingsComponent),
    title: 'Types de retenue - InstaFact'
  }
];
