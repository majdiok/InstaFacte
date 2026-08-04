import { Routes } from '@angular/router';
import { firmNativeGuard, accountingFirmsFeatureGuard } from '@core/guards/accounting-firms.guard';
import { firmManagerGuard } from '@core/guards/firm-manager.guard';
import { firmGovernanceFeatureGuard } from '@core/guards/firm-governance.guard';

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
        path: 'clients/new',
        canActivate: [firmManagerGuard],
        loadComponent: () =>
          import('./managed-clients/firm-managed-client-wizard.component').then(m => m.FirmManagedClientWizardComponent),
        title: 'Cabinet — Nouveau dossier client'
      },
      {
        path: 'affectation',
        canActivate: [firmGovernanceFeatureGuard, firmManagerGuard],
        loadComponent: () =>
          import('./affectation/firm-dossier-affectation.component').then(m => m.FirmDossierAffectationComponent),
        title: 'Cabinet — Affectation des dossiers'
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
        path: 'exchanges',
        loadChildren: () =>
          import('../exchanges/exchanges.routes').then(m => m.EXCHANGES_ROUTES),
        title: 'Cabinet — Échanges'
      },
      {
        path: 'settings',
        loadComponent: () => import('./firm-settings/firm-settings.component').then(m => m.FirmSettingsComponent),
        title: 'Cabinet — Paramètres'
      },
      {
        path: 'settings/users',
        redirectTo: '/firm/collaborateurs',
        pathMatch: 'full'
      },
      {
        path: 'settings/activity-codes',
        canActivate: [firmManagerGuard],
        loadComponent: () =>
          import('./activity-codes/firm-activity-codes.component').then(m => m.FirmActivityCodesComponent),
        title: 'Cabinet — Types d’activité'
      },
      {
        path: 'collaborateurs',
        canActivate: [firmManagerGuard],
        loadComponent: () =>
          import('./collaborators/firm-collaborators-list.component').then(m => m.FirmCollaboratorsListComponent),
        title: 'Cabinet — Collaborateurs'
      },
      {
        path: 'collaborateurs/new',
        canActivate: [firmManagerGuard],
        loadComponent: () =>
          import('./collaborators/firm-collaborator-form.component').then(m => m.FirmCollaboratorFormComponent),
        title: 'Cabinet — Nouveau collaborateur'
      },
      {
        path: 'collaborateurs/:id',
        canActivate: [firmManagerGuard],
        loadComponent: () =>
          import('./collaborators/firm-collaborator-form.component').then(m => m.FirmCollaboratorFormComponent),
        title: 'Cabinet — Consultation collaborateur'
      },
      {
        path: 'collaborateurs/:id/edit',
        canActivate: [firmManagerGuard],
        loadComponent: () =>
          import('./collaborators/firm-collaborator-form.component').then(m => m.FirmCollaboratorFormComponent),
        title: 'Cabinet — Modification collaborateur'
      },
      {
        path: 'governance',
        loadChildren: () => import('./governance/governance.routes').then(m => m.FIRM_GOVERNANCE_ROUTES)
      },
      {
        path: 'billing',
        canActivate: [firmManagerGuard],
        loadChildren: () => import('./billing/billing.routes').then(m => m.FIRM_BILLING_ROUTES)
      },
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' }
    ]
  }
];
