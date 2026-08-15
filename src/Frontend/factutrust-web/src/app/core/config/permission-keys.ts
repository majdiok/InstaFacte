/**
 * Mirror of backend `InstaFact.Domain.Enums.Permissions` (UserRole.cs).
 * Do not change strings without updating the domain.
 */
export const PERMISSIONS = {
  invoices: {
    create: 'invoices:create',
    read: 'invoices:read',
    update: 'invoices:update',
    delete: 'invoices:delete',
    sign: 'invoices:sign',
    send: 'invoices:send'
  },
  clients: {
    create: 'clients:create',
    read: 'clients:read',
    update: 'clients:update',
    delete: 'clients:delete'
  },
  products: {
    create: 'products:create',
    read: 'products:read',
    update: 'products:update',
    delete: 'products:delete'
  },
  payments: {
    create: 'payments:create',
    read: 'payments:read',
    update: 'payments:update'
  },
  /**
   * Trésorerie prévisionnelle par IA. Volontairement distinct de `forecasting:*` (Prévisions IA
   * ventes/stock) : le rôle Comptable possède celles-ci et pas celles-là.
   */
  treasuryForecast: {
    view: 'treasury_forecast:view',
    manage: 'treasury_forecast:manage'
  },
  users: {
    create: 'users:create',
    read: 'users:read',
    update: 'users:update',
    delete: 'users:delete'
  },
  settings: {
    read: 'settings:read',
    update: 'settings:update'
  },
  reports: {
    view: 'reports:view',
    export: 'reports:export',
    salesOwn: 'reports:sales_own'
  },
  quotes: {
    create: 'quotes:create',
    read: 'quotes:read',
    update: 'quotes:update',
    delete: 'quotes:delete'
  },
  deliveryNotes: {
    create: 'delivery_notes:create',
    read: 'delivery_notes:read',
    update: 'delivery_notes:update',
    delete: 'delivery_notes:delete'
  },
  pricing: {
    create: 'pricing:create',
    read: 'pricing:read',
    update: 'pricing:update',
    delete: 'pricing:delete'
  },
  salesOrders: {
    create: 'sales_orders:create',
    read: 'sales_orders:read',
    update: 'sales_orders:update',
    delete: 'sales_orders:delete'
  },
  suppliers: {
    create: 'suppliers:create',
    read: 'suppliers:read',
    update: 'suppliers:update',
    delete: 'suppliers:delete'
  },
  purchaseOrders: {
    create: 'purchase_orders:create',
    read: 'purchase_orders:read',
    update: 'purchase_orders:update',
    delete: 'purchase_orders:delete'
  },
  purchaseReceipts: {
    create: 'purchase_receipts:create',
    read: 'purchase_receipts:read',
    update: 'purchase_receipts:update',
    delete: 'purchase_receipts:delete'
  },
  supplierInvoices: {
    create: 'supplier_invoices:create',
    read: 'supplier_invoices:read',
    update: 'supplier_invoices:update',
    delete: 'supplier_invoices:delete'
  },
  stock: {
    create: 'stock:create',
    read: 'stock:read',
    update: 'stock:update',
    delete: 'stock:delete'
  },
  stockTransfers: {
    create: 'stock_transfers:create',
    read: 'stock_transfers:read',
    update: 'stock_transfers:update',
    delete: 'stock_transfers:delete'
  },
  inventory: {
    create: 'inventory:create',
    read: 'inventory:read',
    update: 'inventory:update',
    delete: 'inventory:delete'
  },
  accounting: {
    read: 'accounting:read',
    create: 'accounting:create',
    delete: 'accounting:delete',
    close: 'accounting:close',
    validate: 'accounting:validate',
    reverse: 'accounting:reverse',
    import: 'accounting:import',
    declare: 'accounting:declare'
  },
  audit: {
    read: 'audit:read'
  },
  crm: {
    read: 'crm:read',
    create: 'crm:create',
    update: 'crm:update',
    delete: 'crm:delete'
  },
  salesTargets: {
    read: 'sales_targets:read',
    manage: 'sales_targets:manage'
  },
  withholdingTax: {
    read: 'withholding_tax:read',
    create: 'withholding_tax:create',
    edit: 'withholding_tax:edit',
    validate: 'withholding_tax:validate',
    delete: 'withholding_tax:delete',
    export: 'withholding_tax:export'
  },
  storefront: {
    manage: 'storefront:manage'
  },
  studio: {
    designEntities: 'studio:design_entities',
    designForms: 'studio:design_forms',
    designReports: 'studio:design_reports'
  },
  customData: {
    recordsRead: 'custom_records:read',
    recordsWrite: 'custom_records:write',
    reportsView: 'custom_reports:view'
  },
  payroll: {
    read: 'payroll:read',
    manageEmployees: 'payroll:manage_employees',
    manageGarnishments: 'payroll:manage_garnishments',
    run: 'payroll:run',
    validate: 'payroll:validate',
    declare: 'payroll:declare',
    export: 'payroll:export',
    pay: 'payroll:pay',
    settings: 'payroll:settings',
    hrDocuments: 'payroll:hr_documents',
    manageTermination: 'payroll:manage_termination'
  },
  firm: {
    manage: 'firm:manage',
    usersManage: 'firm:users:manage',
    assignmentsManage: 'firm:assignments:manage'
  },
  /** Agent « Chef de mission » — distinct de `ai:chat`, qui reste réservé à l'assistant société. */
  firmAi: {
    chat: 'firm:ai:chat',
    remind: 'firm:ai:remind'
  },
  honorairesInvoices: {
    create: 'honoraires.invoices:create',
    read: 'honoraires.invoices:read',
    update: 'honoraires.invoices:update',
    delete: 'honoraires.invoices:delete',
    validate: 'honoraires.invoices:validate',
    send: 'honoraires.invoices:send'
  },
  honorairesQuotes: {
    create: 'honoraires.quotes:create',
    read: 'honoraires.quotes:read',
    update: 'honoraires.quotes:update',
    delete: 'honoraires.quotes:delete',
    convert: 'honoraires.quotes:convert'
  },
  honorairesPayments: {
    create: 'honoraires.payments:create',
    read: 'honoraires.payments:read'
  }
} as const;
