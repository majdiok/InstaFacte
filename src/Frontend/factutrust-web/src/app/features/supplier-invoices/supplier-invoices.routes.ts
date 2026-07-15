import { Routes } from '@angular/router';

export const SUPPLIER_INVOICES_ROUTES: Routes = [
    {
        path: '',
        loadComponent: () => import('./supplier-invoice-list/supplier-invoice-list.component').then(m => m.SupplierInvoiceListComponent),
        title: 'Factures fournisseurs - InstaFact'
    },
    {
        path: 'unpaid',
        loadComponent: () => import('./supplier-invoice-list/supplier-invoice-list.component').then(m => m.SupplierInvoiceListComponent),
        title: 'Factures impayées fournisseurs - InstaFact',
        data: { unpaidOnly: true }
    },
    {
        path: 'unpaid/:id',
        loadComponent: () => import('./supplier-invoice-detail/supplier-invoice-detail.component').then(m => m.SupplierInvoiceDetailComponent),
        title: 'Détail facture fournisseur - InstaFact',
        data: { unpaidOnly: true }
    },
    {
        path: ':id',
        loadComponent: () => import('./supplier-invoice-detail/supplier-invoice-detail.component').then(m => m.SupplierInvoiceDetailComponent),
        title: 'Détail facture fournisseur - InstaFact'
    }
];
