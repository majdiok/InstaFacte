/**
 * Mirrors backend <c>InstaFact.Domain.Enums.AppModule</c> (integer values must stay aligned).
 */
export enum AppModule {
  Clients = 0,
  Products = 1,
  Sales = 2,
  Treasury = 3,
  Reports = 4,
  Administration = 5,
  Purchases = 6,
  Stock = 7,
  Accounting = 8,
  CRM = 9,
  Fiscal = 10,
  AI = 11,
  Forecasting = 12,
  Studio = 13,
  Payroll = 14
}

export const APP_MODULE_OPTIONS: { value: AppModule; label: string }[] = [
  { value: AppModule.Clients, label: 'Clients' },
  { value: AppModule.Products, label: 'Produits et services' },
  { value: AppModule.Sales, label: 'Ventes (factures)' },
  { value: AppModule.Treasury, label: 'Trésorerie (paiements)' },
  { value: AppModule.Reports, label: 'Rapports' },
  { value: AppModule.Administration, label: 'Paramètres et utilisateurs' },
  { value: AppModule.Purchases, label: 'Achats' },
  { value: AppModule.Stock, label: 'Stock' },
  { value: AppModule.Accounting, label: 'Comptabilité' },
  { value: AppModule.CRM, label: 'CRM Commercial' },
  { value: AppModule.Fiscal, label: 'Fiscal / TEJ' },
  { value: AppModule.AI, label: 'Assistant IA' },
  { value: AppModule.Forecasting, label: 'Prévisions IA' },
  { value: AppModule.Studio, label: 'Studio (low-code)' },
  { value: AppModule.Payroll, label: 'RH & Paie' }
];
