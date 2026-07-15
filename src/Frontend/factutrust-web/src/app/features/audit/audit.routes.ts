import { Routes } from '@angular/router';

export const AUDIT_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./audit-log/audit-log.component').then(m => m.AuditLogComponent),
    title: 'Journal d\'audit - InstaFact'
  }
];
