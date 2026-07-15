import { Routes } from '@angular/router';

export const PAYMENTS_ROUTES: Routes = [
  {
    path: '',
    redirectTo: 'clients',
    pathMatch: 'full'
  },
  {
    path: 'clients',
    loadComponent: () => import('./payments.component').then(m => m.PaymentsComponent),
    data: { paymentType: 'client' },
    title: 'Paiements clients - InstaFact'
  },
  {
    path: 'suppliers',
    loadComponent: () => import('./payments.component').then(m => m.PaymentsComponent),
    data: { paymentType: 'supplier' },
    title: 'Paiements fournisseurs - InstaFact'
  },
  {
    path: 'cash-desk',
    loadComponent: () => import('./cash-desk/cash-desk.component').then(m => m.CashDeskComponent),
    title: 'Caisse de trésorerie - InstaFact'
  },
  {
    path: 'bank-accounts',
    loadComponent: () => import('./bank-accounts/bank-accounts.component').then(m => m.BankAccountsComponent),
    title: 'Comptes bancaires - InstaFact'
  }
];
