import { AppModule } from '@core/models/app-module';

/**
 * First path segment (after /) → modules that must all be enabled (AND).
 * Empty array = no module check (any authenticated user with warehouse context).
 */
export const MODULES_REQUIRED_BY_FIRST_SEGMENT: Record<string, AppModule[]> = {
  dashboard: [],
  'ai-assistant': [AppModule.AI],
  invoices: [AppModule.Sales],
  quotes: [AppModule.Sales],
  'delivery-notes': [AppModule.Sales],
  'return-notes': [AppModule.Sales],
  clients: [AppModule.Clients],
  products: [AppModule.Products],
  'product-categories': [AppModule.Products],
  reports: [AppModule.Reports],
  payments: [AppModule.Treasury],
  treasury: [AppModule.Treasury],
  stock: [AppModule.Stock],
  inventory: [AppModule.Stock],
  transfers: [AppModule.Stock],
  settings: [AppModule.Administration],
  suppliers: [AppModule.Purchases],
  'purchase-orders': [AppModule.Purchases],
  'purchase-receipts': [AppModule.Purchases],
  'supplier-invoices': [AppModule.Purchases],
  accounting: [AppModule.Accounting],
  audit: [AppModule.Accounting],
  crm: [AppModule.CRM],
  'withholding-tax': [AppModule.Fiscal],
  payroll: [AppModule.Payroll],
  projects: [AppModule.Projects]
};

/**
 * When set, user must have every permission (JWT effective) in addition to module checks.
 * Omitted segments: module check only (or none).
 */
export const PERMISSIONS_ALL_REQUIRED_BY_FIRST_SEGMENT: Record<string, string[]> = {
  'ai-assistant': ['ai:chat'],
  quotes: ['quotes:read'],
  'delivery-notes': ['delivery_notes:read'],
  'return-notes': ['return_notes:read'],
  invoices: ['invoices:read'],
  clients: ['clients:read'],
  products: ['products:read'],
  'product-categories': ['products:read'],
  stock: ['stock:read'],
  inventory: ['inventory:read'],
  transfers: ['stock_transfers:read'],
  suppliers: ['suppliers:read'],
  'purchase-orders': ['purchase_orders:read'],
  'purchase-receipts': ['purchase_receipts:read'],
  'supplier-invoices': ['supplier_invoices:read'],
  payments: ['payments:read'],
  // Le prévisionnel s'adresse au comptable, qui n'a pas forcément `payments:read` :
  // la garde du segment porte donc sa propre permission.
  treasury: ['treasury_forecast:view'],
  reports: ['reports:view'],
  settings: ['settings:read'],
  accounting: ['accounting:read'],
  audit: ['audit:read'],
  crm: ['crm:read'],
  'withholding-tax': ['withholding_tax:read'],
  payroll: ['payroll:read'],
  projects: ['projects:read']
};

/** POS loads products, warehouses (stock), and creates invoices. */
export const POS_REQUIRED_MODULES: AppModule[] = [
  AppModule.Sales,
  AppModule.Products,
  AppModule.Stock
];

export const POS_REQUIRED_PERMISSIONS_ALL: string[] = [
  'invoices:create',
  'products:read',
  'stock:read'
];
