import { Routes } from '@angular/router';
import { fixedAssetsFeatureGuard } from './shared/fixed-assets-feature.guard';

export const ACCOUNTING_ROUTES: Routes = [
  {
    path: 'financial-statements',
    loadComponent: () =>
      import('./accounting-financial-statements/accounting-financial-statements.component').then(
        m => m.AccountingFinancialStatementsComponent
      ),
    title: 'États comptables - InstaFact'
  },
  {
    path: 'home',
    loadComponent: () =>
      import('./accounting-home/accounting-home.component').then(m => m.AccountingHomeComponent),
    title: 'Comptabilité - InstaFact'
  },
  {
    path: 'chart',
    loadComponent: () =>
      import('./chart-of-accounts/chart-of-accounts.component').then(m => m.ChartOfAccountsComponent),
    title: 'Plan comptable - InstaFact'
  },
  {
    path: 'entry-templates',
    loadComponent: () =>
      import('./entry-templates/entry-templates.component').then(m => m.EntryTemplatesComponent),
    title: "Modèles d'écriture - InstaFact"
  },
  {
    path: 'journals',
    loadComponent: () =>
      import('./journals/journals.component').then(m => m.JournalsComponent),
    title: 'Journaux & familles - InstaFact'
  },
  {
    path: 'manual-entry',
    loadComponent: () =>
      import('./manual-entry/manual-entry.component').then(m => m.ManualEntryComponent),
    title: 'Saisie manuelle - InstaFact'
  },
  {
    path: 'journal',
    loadComponent: () => import('./journal/journal.component').then(m => m.JournalComponent),
    title: 'Journal - InstaFact'
  },
  {
    path: 'ledger',
    loadComponent: () => import('./ledger/ledger.component').then(m => m.LedgerComponent),
    title: 'Grand livre - InstaFact'
  },
  {
    path: 'sub-journals',
    loadComponent: () =>
      import('./sub-journals/sub-journals.component').then(m => m.SubJournalsComponent),
    title: 'Journaux auxiliaires - InstaFact'
  },
  {
    path: 'balance',
    loadComponent: () => import('./balance/balance.component').then(m => m.BalanceComponent),
    title: 'Balance - InstaFact'
  },
  {
    path: 'aging',
    loadComponent: () => import('./aging/aging.component').then(m => m.AgingComponent),
    title: 'Balance âgée - InstaFact'
  },
  {
    path: 'auxiliary-balance',
    loadComponent: () =>
      import('./auxiliary-balance/auxiliary-balance.component').then(m => m.AuxiliaryBalanceComponent),
    title: 'Balance auxiliaire - InstaFact'
  },
  {
    path: 'third-party-ledger',
    loadComponent: () =>
      import('./third-party-ledger/third-party-ledger.component').then(m => m.ThirdPartyLedgerComponent),
    title: 'Grand livre tiers - InstaFact'
  },
  {
    path: 'third-parties',
    loadComponent: () =>
      import('./third-parties/third-parties.component').then(m => m.ThirdPartiesComponent),
    title: 'Plan tiers - InstaFact'
  },
  {
    path: 'budget-posts',
    loadComponent: () =>
      import('./budget-posts/budget-posts.component').then(m => m.BudgetPostsComponent),
    title: 'Postes budgétaires - InstaFact'
  },
  {
    path: 'budgets',
    loadComponent: () =>
      import('./budget-entry/budget-entry.component').then(m => m.BudgetEntryComponent),
    title: 'Saisie des budgets - InstaFact'
  },
  {
    path: 'budget-report',
    loadComponent: () =>
      import('./budget-report/budget-report.component').then(m => m.BudgetReportComponent),
    title: 'État budgétaire - InstaFact'
  },
  {
    path: 'vat-declaration',
    loadComponent: () =>
      import('./vat-declaration/vat-declaration.component').then(m => m.VatDeclarationComponent),
    title: 'Déclaration TVA - InstaFact'
  },
  {
    path: 'fiscal-schedule',
    loadComponent: () =>
      import('./fiscal-schedule/fiscal-schedule.component').then(m => m.FiscalScheduleComponent),
    data: { fiscalScheduleScope: 'tenant' },
    title: 'Echeancier fiscal - InstaFact'
  },
  {
    path: 'pre-closing',
    loadComponent: () =>
      import('./pre-closing/pre-closing.component').then(m => m.PreClosingComponent),
    title: 'Contrôles de pré-clôture - InstaFact'
  },
  {
    path: 'inventory-assistant',
    loadComponent: () =>
      import('./inventory-assistant/inventory-assistant.component').then(m => m.InventoryAssistantComponent),
    title: "Assistant d'inventaire - InstaFact"
  },
  {
    path: 'closing',
    loadComponent: () => import('./closing/closing.component').then(m => m.ClosingComponent),
    title: 'Clôture - InstaFact'
  },
  {
    path: 'import',
    loadComponent: () => import('./import/import.component').then(m => m.ImportComponent),
    title: 'Reprise de dossier - InstaFact'
  },
  {
    path: 'balance-sheet',
    loadComponent: () =>
      import('./balance-sheet/balance-sheet.component').then(m => m.BalanceSheetComponent),
    title: 'Bilan - InstaFact'
  },
  {
    path: 'income-statement',
    loadComponent: () =>
      import('./income-statement/income-statement.component').then(m => m.IncomeStatementComponent),
    title: 'Compte de résultat - InstaFact'
  },
  {
    path: 'nct-statements',
    loadComponent: () =>
      import('./nct-statements/nct-statements.component').then(m => m.NctStatementsComponent),
    title: 'États financiers NCT - InstaFact'
  },
  {
    path: 'lettering',
    loadComponent: () =>
      import('./lettering/lettering.component').then(m => m.LetteringComponent),
    title: 'Lettrage - InstaFact'
  },
  {
    path: 'entry-search',
    loadComponent: () =>
      import('./entry-search/entry-search.component').then(m => m.EntrySearchComponent),
    title: "Recherche d'écriture - InstaFact"
  },
  {
    path: 'account-replacement',
    loadComponent: () =>
      import('./account-replacement/account-replacement.component').then(m => m.AccountReplacementComponent),
    title: 'Remplacement de compte - InstaFact'
  },
  {
    path: 'bank-reconciliation',
    loadComponent: () =>
      import('./bank-reconciliation/bank-reconciliation.component').then(m => m.BankReconciliationComponent),
    title: 'Rapprochement bancaire - InstaFact'
  },
  {
    path: 'fixed-assets',
    loadComponent: () =>
      import('./fixed-assets/fixed-assets-list.component').then(m => m.FixedAssetsListComponent),
    canActivate: [fixedAssetsFeatureGuard],
    title: 'Immobilisations - InstaFact'
  },
  {
    path: 'fixed-assets/new',
    loadComponent: () =>
      import('./fixed-assets/fixed-asset-detail.component').then(m => m.FixedAssetDetailComponent),
    canActivate: [fixedAssetsFeatureGuard],
    data: { mode: 'new' },
    title: 'Nouvelle immobilisation - InstaFact'
  },
  {
    path: 'fixed-assets/depreciation-run',
    loadComponent: () =>
      import('./fixed-assets/depreciation-run.component').then(m => m.DepreciationRunComponent),
    canActivate: [fixedAssetsFeatureGuard],
    title: 'Dotations immobilisations - InstaFact'
  },
  {
    path: 'fixed-assets/amortization-table',
    loadComponent: () =>
      import('./fixed-assets/fixed-assets-amortization-table.component').then(m => m.FixedAssetsAmortizationTableComponent),
    canActivate: [fixedAssetsFeatureGuard],
    title: 'Tableau des amortissements - InstaFact'
  },
  {
    path: 'fixed-assets/:id',
    loadComponent: () =>
      import('./fixed-assets/fixed-asset-detail.component').then(m => m.FixedAssetDetailComponent),
    canActivate: [fixedAssetsFeatureGuard],
    title: 'Immobilisation - InstaFact'
  },
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () =>
      import('./accounting-root-redirect/accounting-root-redirect.component').then(
        m => m.AccountingRootRedirectComponent
      )
  }
];
