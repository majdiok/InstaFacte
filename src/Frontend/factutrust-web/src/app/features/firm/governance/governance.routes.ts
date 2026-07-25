import { Routes } from '@angular/router';
import { firmGovernanceFeatureGuard } from '@core/guards/firm-governance.guard';

export const FIRM_GOVERNANCE_ROUTES: Routes = [
  {
    path: '',
    canActivate: [firmGovernanceFeatureGuard],
    children: [
      {
        path: 'dashboard',
        redirectTo: '/firm/dashboard',
        pathMatch: 'full'
      },
      {
        path: 'permanent-files',
        loadComponent: () =>
          import('./firm-permanent-files.component').then(m => m.FirmPermanentFilesComponent),
        title: 'Cabinet — Dossiers permanents'
      },
      {
        path: 'permanent-files/:assignmentId',
        loadComponent: () =>
          import('./firm-permanent-file-wizard.component').then(m => m.FirmPermanentFileWizardComponent),
        title: 'Cabinet — Dossier permanent'
      },
      {
        path: 'time-sheets',
        loadComponent: () =>
          import('./firm-time-sheets.component').then(m => m.FirmTimeSheetsComponent),
        title: 'Cabinet — Feuilles de temps'
      },
      {
        path: 'dossier-time-profitability',
        loadComponent: () =>
          import('./firm-dossier-time-profitability.component').then(m => m.FirmDossierTimeProfitabilityComponent),
        title: 'Cabinet — Rentabilité dossiers'
      },
      {
        path: 'collaborator-rentability',
        loadComponent: () =>
          import('./firm-collaborator-rentability-list.component').then(m => m.FirmCollaboratorRentabilityListComponent),
        title: 'Cabinet — Rentabilité collaborateurs'
      },
      {
        path: 'collaborator-rentability/new',
        loadComponent: () =>
          import('./firm-collaborator-rentability-form.component').then(m => m.FirmCollaboratorRentabilityFormComponent),
        title: 'Cabinet — Nouvelle rentabilité'
      },
      {
        path: 'collaborator-rentability/:id/edit',
        loadComponent: () =>
          import('./firm-collaborator-rentability-form.component').then(m => m.FirmCollaboratorRentabilityFormComponent),
        title: 'Cabinet — Modifier rentabilité'
      },
      {
        path: 'collaborator-rentability/:id',
        loadComponent: () =>
          import('./firm-collaborator-rentability-form.component').then(m => m.FirmCollaboratorRentabilityFormComponent),
        title: 'Cabinet — Détail rentabilité'
      },
      {
        path: 'expense-notes',
        loadComponent: () =>
          import('./firm-expense-notes.component').then(m => m.FirmExpenseNotesComponent),
        title: 'Cabinet — Notes de frais'
      },
      {
        path: 'social',
        loadComponent: () =>
          import('./firm-social-overview.component').then(m => m.FirmSocialOverviewComponent),
        title: 'Cabinet — Suivi social'
      },
      { path: '', redirectTo: '/firm/dashboard', pathMatch: 'full' }
    ]
  }
];
