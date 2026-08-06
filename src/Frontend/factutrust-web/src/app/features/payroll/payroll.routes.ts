import { Routes } from '@angular/router';
import { permissionGuard } from '@core/guards/permission.guard';
import { PERMISSIONS } from '@core/config/permission-keys';

export const PAYROLL_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'employees' },
  {
    path: 'employees',
    loadComponent: () => import('./employees/employee-list.component').then(m => m.EmployeeListComponent),
    title: 'Salariés - InstaFact',
    canActivate: [permissionGuard],
    data: { permissions: [PERMISSIONS.payroll.read] }
  },
  {
    path: 'employees/new',
    loadComponent: () => import('./employees/employee-form.component').then(m => m.EmployeeFormComponent),
    title: 'Nouveau salarié - InstaFact',
    canActivate: [permissionGuard],
    data: { permissions: [PERMISSIONS.payroll.manageEmployees] }
  },
  {
    path: 'employees/:id/edit',
    loadComponent: () => import('./employees/employee-form.component').then(m => m.EmployeeFormComponent),
    title: 'Modifier salarié - InstaFact',
    canActivate: [permissionGuard],
    data: { permissions: [PERMISSIONS.payroll.manageEmployees] }
  },
  {
    path: 'employees/:id',
    loadComponent: () => import('./employees/employee-detail.component').then(m => m.EmployeeDetailComponent),
    title: 'Fiche salarié - InstaFact',
    canActivate: [permissionGuard],
    data: { permissions: [PERMISSIONS.payroll.read] }
  },
  {
    path: 'runs',
    loadComponent: () => import('./runs/payroll-run-list.component').then(m => m.PayrollRunListComponent),
    title: 'Cycles de paie - InstaFact',
    canActivate: [permissionGuard],
    data: { permissions: [PERMISSIONS.payroll.read] }
  },
  {
    path: 'runs/:id',
    loadComponent: () => import('./runs/payroll-run-detail.component').then(m => m.PayrollRunDetailComponent),
    title: 'Cycle de paie - InstaFact',
    canActivate: [permissionGuard],
    data: { permissions: [PERMISSIONS.payroll.read] }
  },
  {
    path: 'regularization',
    loadComponent: () => import('./regularization/irpp-regularization.component').then(m => m.IrppRegularizationComponent),
    title: 'Régularisation IRPP - InstaFact',
    canActivate: [permissionGuard],
    // Lecture pour consulter et calculer ; l'enregistrement est gaté dans le composant
    // par canRunPayroll (même approche que le détail de cycle).
    data: { permissions: [PERMISSIONS.payroll.read] }
  },
  {
    path: 'settings',
    loadComponent: () => import('./settings/payroll-settings.component').then(m => m.PayrollSettingsComponent),
    title: 'Paramètres paie - InstaFact',
    canActivate: [permissionGuard],
    data: { permissions: [PERMISSIONS.payroll.settings] }
  },
  {
    path: 'reports/payroll-book',
    loadComponent: () => import('./reports/payroll-book.component').then(m => m.PayrollBookComponent),
    title: 'Livre de paie - InstaFact',
    canActivate: [permissionGuard],
    data: { permissions: [PERMISSIONS.payroll.read] }
  },
  {
    path: 'reports/payroll-journal',
    loadComponent: () => import('./reports/payroll-journal.component').then(m => m.PayrollJournalComponent),
    title: 'Journal de paie - InstaFact',
    canActivate: [permissionGuard],
    data: { permissions: [PERMISSIONS.payroll.read] }
  },
  {
    path: 'declarations',
    loadComponent: () => import('./declarations/payroll-declarations.component').then(m => m.PayrollDeclarationsComponent),
    title: 'Déclarations paie - InstaFact',
    canActivate: [permissionGuard],
    data: { permissions: [PERMISSIONS.payroll.declare] }
  }
];
