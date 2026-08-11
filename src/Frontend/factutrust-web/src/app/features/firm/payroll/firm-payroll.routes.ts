import { Routes } from '@angular/router';
import { firmManagerGuard } from '@core/guards/firm-manager.guard';
import { firmPayrollReadGuard } from '@core/guards/firm-payroll-read.guard';
import { firmInternalPayrollGuard } from '@core/guards/firm-internal-payroll.guard';

const FIRM_PAYROLL_ROUTE_DATA = {
  firmInternalPayroll: true,
  payrollRouteBase: '/firm/payroll'
};

/**
 * Paie interne du cabinet.
 *
 * La racine n'exige que la lecture : le comptable cabinet possède `payroll:read` et doit pouvoir
 * consulter. Chaque route qui écrit — création, modification, cycle, paramètres, déclarations —
 * ajoute `firmManagerGuard`.
 */
export const FIRM_PAYROLL_ROUTES: Routes = [
  {
    path: '',
    canActivate: [firmInternalPayrollGuard, firmPayrollReadGuard],
    data: FIRM_PAYROLL_ROUTE_DATA,
    children: [
      {
        path: '',
        loadComponent: () =>
          import('./firm-payroll-dashboard.component').then(m => m.FirmPayrollDashboardComponent),
        title: 'Cabinet — Paie interne'
      },
      {
        path: 'employees',
        data: FIRM_PAYROLL_ROUTE_DATA,
        children: [
          {
            path: '',
            loadComponent: () =>
              import('../../payroll/employees/employee-list.component').then(m => m.EmployeeListComponent),
            title: 'Cabinet — Salariés internes'
          },
          {
            path: 'new',
            canActivate: [firmManagerGuard],
            loadComponent: () =>
              import('../../payroll/employees/employee-form.component').then(m => m.EmployeeFormComponent),
            title: 'Cabinet — Nouveau salarié'
          },
          {
            path: ':id',
            loadComponent: () =>
              import('../../payroll/employees/employee-detail.component').then(m => m.EmployeeDetailComponent),
            title: 'Cabinet — Fiche salarié'
          },
          {
            path: ':id/edit',
            canActivate: [firmManagerGuard],
            loadComponent: () =>
              import('../../payroll/employees/employee-form.component').then(m => m.EmployeeFormComponent),
            title: 'Cabinet — Modifier salarié'
          }
        ]
      },
      {
        path: 'runs',
        data: FIRM_PAYROLL_ROUTE_DATA,
        children: [
          {
            path: '',
            loadComponent: () =>
              import('../../payroll/runs/payroll-run-list.component').then(m => m.PayrollRunListComponent),
            title: 'Cabinet — Cycles de paie'
          },
          {
            path: ':id',
            canActivate: [firmManagerGuard],
            loadComponent: () =>
              import('../../payroll/runs/payroll-run-detail.component').then(m => m.PayrollRunDetailComponent),
            title: 'Cabinet — Cycle de paie'
          }
        ]
      },
      {
        // Paramètres de paie du cabinet : barèmes, jours fériés, primes annuelles.
        path: 'settings',
        canActivate: [firmManagerGuard],
        data: FIRM_PAYROLL_ROUTE_DATA,
        children: [
          {
            path: '',
            loadComponent: () =>
              import('../../payroll/settings/payroll-settings.component').then(m => m.PayrollSettingsComponent),
            title: 'Cabinet — Paramètres paie'
          },
          {
            path: 'holidays',
            loadComponent: () =>
              import('../../payroll/settings/payroll-holidays.component').then(m => m.PayrollHolidaysComponent),
            title: 'Cabinet — Jours fériés'
          },
          {
            path: 'annual-bonuses',
            loadComponent: () =>
              import('../../payroll/settings/annual-bonuses.component').then(m => m.AnnualBonusesSettingsComponent),
            title: 'Cabinet — Primes annuelles'
          }
        ]
      },
      {
        path: 'declarations',
        canActivate: [firmManagerGuard],
        data: FIRM_PAYROLL_ROUTE_DATA,
        loadComponent: () =>
          import('../../payroll/declarations/payroll-declarations.component').then(m => m.PayrollDeclarationsComponent),
        title: 'Cabinet — Déclarations sociales'
      },
      {
        path: 'reports',
        data: FIRM_PAYROLL_ROUTE_DATA,
        children: [
          {
            path: 'payroll-book',
            loadComponent: () =>
              import('../../payroll/reports/payroll-book.component').then(m => m.PayrollBookComponent),
            title: 'Cabinet — Livre de paie'
          },
          {
            path: 'payroll-journal',
            loadComponent: () =>
              import('../../payroll/reports/payroll-journal.component').then(m => m.PayrollJournalComponent),
            title: 'Cabinet — Journal de paie'
          }
        ]
      }
    ]
  }
];
