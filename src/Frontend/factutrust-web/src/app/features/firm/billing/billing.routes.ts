import { Routes } from '@angular/router';
import { firmManagerGuard } from '@core/guards/firm-manager.guard';

export const FIRM_BILLING_ROUTES: Routes = [
  {
    path: 'invoices',
    canActivate: [firmManagerGuard],
    loadComponent: () =>
      import('./pages/invoice-list/honoraires-invoice-list.component').then(m => m.HonorairesInvoiceListComponent),
    title: 'Cabinet — Factures honoraires'
  },
  {
    path: 'invoices/new',
    canActivate: [firmManagerGuard],
    loadComponent: () =>
      import('./pages/document-editor/honoraires-document-editor.component').then(
        m => m.HonorairesDocumentEditorComponent
      ),
    title: 'Cabinet — Nouvelle facture'
  },
  {
    path: 'invoices/:id',
    canActivate: [firmManagerGuard],
    loadComponent: () =>
      import('./pages/document-editor/honoraires-document-editor.component').then(
        m => m.HonorairesDocumentEditorComponent
      ),
    title: 'Cabinet — Facture honoraires'
  },
  {
    path: 'credit-notes',
    canActivate: [firmManagerGuard],
    loadComponent: () =>
      import('./pages/invoice-list/honoraires-invoice-list.component').then(m => m.HonorairesInvoiceListComponent),
    title: 'Cabinet — Avoirs honoraires',
    data: { documentType: 1 }
  },
  {
    path: 'quotes',
    canActivate: [firmManagerGuard],
    loadComponent: () =>
      import('./pages/quote-list/honoraires-quote-list.component').then(m => m.HonorairesQuoteListComponent),
    title: 'Cabinet — Devis honoraires'
  },
  {
    path: 'quotes/new',
    canActivate: [firmManagerGuard],
    loadComponent: () =>
      import('./pages/document-editor/honoraires-document-editor.component').then(
        m => m.HonorairesDocumentEditorComponent
      ),
    title: 'Cabinet — Nouveau devis',
    data: { mode: 'quote' }
  },
  {
    path: 'quotes/:id',
    canActivate: [firmManagerGuard],
    loadComponent: () =>
      import('./pages/document-editor/honoraires-document-editor.component').then(
        m => m.HonorairesDocumentEditorComponent
      ),
    title: 'Cabinet — Devis honoraires',
    data: { mode: 'quote' }
  },
  {
    path: 'payments',
    canActivate: [firmManagerGuard],
    loadComponent: () =>
      import('./pages/payment-list/honoraires-payment-list.component').then(m => m.HonorairesPaymentListComponent),
    title: 'Cabinet — Encaissements'
  },
  { path: '', redirectTo: 'invoices', pathMatch: 'full' }
];
