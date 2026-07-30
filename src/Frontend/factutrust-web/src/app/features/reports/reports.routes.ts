import { Routes } from '@angular/router';

export const REPORTS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./reports-hub/reports-hub.component').then(m => m.ReportsHubComponent),
    title: 'Rapports - InstaFact'
  },
  {
    path: 'analytics',
    loadComponent: () => import('./analytics/analytics-hub.component').then(m => m.AnalyticsHubComponent),
    title: 'États Analytiques - InstaFact',
    children: [
      {
        path: '',
        redirectTo: 'revenue',
        pathMatch: 'full'
      },
      {
        path: 'revenue',
        loadComponent: () => import('./analytics/revenue/revenue-analytics.component').then(m => m.RevenueAnalyticsComponent),
        title: 'Chiffre d\'affaires - InstaFact'
      },
      {
        path: 'sales',
        loadComponent: () => import('./analytics/sales/sales-analytics.component').then(m => m.SalesAnalyticsComponent),
        title: 'Analyse des ventes - InstaFact'
      },
      {
        path: 'clients',
        loadComponent: () => import('./analytics/clients/clients-analytics.component').then(m => m.ClientsAnalyticsComponent),
        title: 'Analyse clients - InstaFact'
      }
    ]
  },
  {
    path: 'sales',
    loadComponent: () => import('./sales/sales-reports.component').then(m => m.SalesReportsComponent),
    title: 'Rapports Ventes - InstaFact'
  },
  {
    path: 'purchases-analytics',
    loadComponent: () => import('./purchases-analytics/purchases-analytics-hub.component').then(m => m.PurchasesAnalyticsHubComponent),
    title: 'États Analytiques Achats - InstaFact',
    children: [
      {
        path: '',
        redirectTo: 'expenses',
        pathMatch: 'full'
      },
      {
        path: 'expenses',
        loadComponent: () => import('./purchases-analytics/expenses/expenses-analytics.component').then(m => m.ExpensesAnalyticsComponent),
        title: 'Dépenses - InstaFact'
      },
      {
        path: 'suppliers',
        loadComponent: () => import('./purchases-analytics/suppliers/suppliers-analytics.component').then(m => m.SuppliersAnalyticsComponent),
        title: 'Analyse fournisseurs - InstaFact'
      },
      {
        path: 'orders',
        loadComponent: () => import('./purchases-analytics/orders/orders-analytics.component').then(m => m.OrdersAnalyticsComponent),
        title: 'Analyse des commandes - InstaFact'
      }
    ]
  },
  {
    path: 'purchases',
    loadComponent: () => import('./purchases/purchases-reports.component').then(m => m.PurchasesReportsComponent),
    title: 'Rapports Achats - InstaFact'
  },
  {
    path: 'stock',
    loadComponent: () => import('./stock/stock-reports.component').then(m => m.StockReportsComponent),
    title: 'Rapports Stock - InstaFact'
  },
  {
    path: 'fiches',
    loadComponent: () => import('./fiches/fiches-reports.component').then(m => m.FichesReportsComponent),
    title: 'Rapports Fiches - InstaFact'
  },
  {
    path: 'payments',
    loadComponent: () => import('./payments/payments-reports.component').then(m => m.PaymentsReportsComponent),
    title: 'Rapports Paiements - InstaFact'
  },
  {
    path: 'sales-by-line',
    loadComponent: () => import('./sales-by-line/sales-by-line-reports.component').then(m => m.SalesByLineReportsComponent),
    title: 'Détails ventes par ligne - InstaFact'
  },
  {
    path: 'profit',
    loadComponent: () => import('./profit/profit-reports.component').then(m => m.ProfitReportsComponent),
    title: 'Rapport Bénéfices - InstaFact'
  },
  {
    path: 'client-balances',
    loadComponent: () => import('./client-balances/client-balances.component').then(m => m.ClientBalancesComponent),
    title: 'Solde par client - InstaFact'
  },
  {
    path: 'supplier-balances',
    loadComponent: () => import('./supplier-balances/supplier-balances.component').then(m => m.SupplierBalancesComponent),
    title: 'Solde par fournisseur - InstaFact'
  },
  {
    path: 'product-sales-analytics',
    loadComponent: () => import('./product-sales-analytics/product-sales-analytics.component').then(m => m.ProductSalesAnalyticsComponent),
    title: 'Rapports décisionnels ventes produits - InstaFact'
  }
];
