/*
    Impact du durcissement de l'accès aux tables (EnableStudioSqlSourceGuard)
    ------------------------------------------------------------------------
    À exécuter sur CHAQUE base tenant AVANT de passer EnableStudioSqlSourceGuard à true.

    Le durcissement applique aux FENÊTRES le classement par domaine de SqlReportAccessPolicy :
    une fenêtre enregistrée sur une table non classée, ou sur un domaine auquel son utilisateur
    n'a pas droit, cesserait de se charger. Ce script liste les fenêtres concernées afin de
    décider en connaissance de cause (reclasser la table, retirer la fenêtre, ou différer).

    Lecture seule — n'écrit rien.
*/

SET NOCOUNT ON;

-- Tables ouvertes au reporting : miroir de SqlReportAccessPolicy.Build().
-- À resynchroniser si le catalogue applicatif évolue.
DECLARE @Classified TABLE (TableName sysname PRIMARY KEY, Domaine nvarchar(30));

INSERT INTO @Classified (TableName, Domaine) VALUES
    -- Référentiel
    ('Clients', 'Referentiel'), ('Products', 'Referentiel'), ('ProductCategories', 'Referentiel'),
    ('Taxes', 'Referentiel'), ('Suppliers', 'Referentiel'), ('Warehouses', 'Referentiel'),
    ('PriceLists', 'Referentiel'), ('PriceListItems', 'Referentiel'), ('PriceListItemTiers', 'Referentiel'),
    ('ClientProductPrices', 'Referentiel'), ('Promotions', 'Referentiel'), ('PaymentTermTemplates', 'Referentiel'),
    -- Ventes
    ('Invoices', 'Ventes'), ('InvoiceLines', 'Ventes'), ('Quotes', 'Ventes'), ('QuoteLines', 'Ventes'),
    ('SalesOrders', 'Ventes'), ('SalesOrderLines', 'Ventes'),
    ('DeliveryNotes', 'Ventes'), ('DeliveryNoteLines', 'Ventes'),
    -- Achats
    ('PurchaseOrders', 'Achats'), ('PurchaseOrderLines', 'Achats'),
    ('PurchaseReceipts', 'Achats'), ('PurchaseReceiptLines', 'Achats'),
    ('SupplierInvoices', 'Achats'), ('SupplierInvoiceLines', 'Achats'),
    -- Stock
    ('StockItems', 'Stock'), ('StockMovements', 'Stock'),
    ('StockTransfers', 'Stock'), ('StockTransferLines', 'Stock'),
    ('PhysicalInventories', 'Stock'), ('InventoryCountLines', 'Stock'),
    -- Trésorerie
    ('Payments', 'Tresorerie'), ('SupplierPayments', 'Tresorerie'), ('CashOperations', 'Tresorerie'),
    ('BankAccounts', 'Tresorerie'), ('BankDeposits', 'Tresorerie'),
    ('BankStatements', 'Tresorerie'), ('BankStatementLines', 'Tresorerie'),
    -- Comptabilité
    ('JournalEntries', 'Comptabilite'), ('JournalEntryLines', 'Comptabilite'),
    ('Journals', 'Comptabilite'), ('JournalFamilies', 'Comptabilite'),
    ('ChartOfAccounts', 'Comptabilite'), ('AccountingPeriods', 'Comptabilite'),
    ('LetteringGroups', 'Comptabilite'), ('LetteringGroupMembers', 'Comptabilite'),
    ('VatDeclarations', 'Comptabilite'), ('FiscalScheduleEntries', 'Comptabilite'),
    ('ThirdPartyAccountingProfiles', 'Comptabilite'), ('WithholdingTaxTypes', 'Comptabilite'),
    ('BudgetPosts', 'Comptabilite'), ('BudgetYears', 'Comptabilite'), ('BudgetLines', 'Comptabilite'),
    ('FixedAssets', 'Comptabilite'), ('FixedAssetEvents', 'Comptabilite'),
    ('DepreciationScheduleLines', 'Comptabilite'), ('DepreciationRateCategories', 'Comptabilite'),
    ('Loans', 'Comptabilite'), ('LoanScheduleLines', 'Comptabilite'),
    -- Paie & RH (données sensibles)
    ('Employees', 'Paie'), ('EmploymentContracts', 'Paie'), ('ContractAllowances', 'Paie'),
    ('PayrollRuns', 'Paie'), ('Payslips', 'Paie'), ('PayslipLines', 'Paie'),
    ('PayrollPayments', 'Paie'), ('PayrollPaymentLines', 'Paie'),
    ('PayrollOvertimeLines', 'Paie'), ('PayrollVariableAllowanceLines', 'Paie'),
    ('LeaveRequests', 'Paie'), ('LeaveBalanceAccruals', 'Paie'),
    ('EmployeeAdvances', 'Paie'), ('CnssContributionPayments', 'Paie'),
    -- CRM
    ('Opportunities', 'Crm'), ('SalesActivities', 'Crm'), ('SalesTargets', 'Crm'),
    -- Prévisionnel
    ('SalesForecasts', 'Previsionnel'), ('ReplenishmentRecommendations', 'Previsionnel'),
    ('PromotionRecommendations', 'Previsionnel'), ('ProductAbcXyzClassifications', 'Previsionnel'),
    ('CashFlowForecastRuns', 'Previsionnel'), ('CashFlowForecastLines', 'Previsionnel'),
    ('CashFlowForecastBuckets', 'Previsionnel');

/* ---------------------------------------------------------------------------
   1. Fenêtres qui deviendraient INACCESSIBLES (table non classée ⇒ refusée).
      Chaque ligne demande une décision explicite avant d'activer le drapeau.
   --------------------------------------------------------------------------- */
SELECT
    'A RECLASSER OU RETIRER' AS Verdict,
    v.[Key]                  AS CleFenetre,
    v.DisplayName            AS Libelle,
    v.SourceTable            AS TableSource,
    v.IsActive               AS Active,
    v.CreatedAt
FROM dbo.CustomViewDefinitions AS v
LEFT JOIN @Classified AS c ON c.TableName = v.SourceTable
WHERE c.TableName IS NULL
ORDER BY v.IsActive DESC, v.SourceTable, v.DisplayName;

/* ---------------------------------------------------------------------------
   2. Fenêtres sur un domaine SENSIBLE : elles resteront accessibles, mais
      seulement aux utilisateurs détenant la permission du domaine (payroll:read,
      accounting:read…). Vérifier qui les consulte aujourd'hui.
   --------------------------------------------------------------------------- */
SELECT
    'PERMISSION DE DOMAINE REQUISE' AS Verdict,
    c.Domaine,
    v.[Key]       AS CleFenetre,
    v.DisplayName AS Libelle,
    v.SourceTable AS TableSource,
    v.IsActive    AS Active
FROM dbo.CustomViewDefinitions AS v
INNER JOIN @Classified AS c ON c.TableName = v.SourceTable
WHERE c.Domaine IN ('Paie', 'Comptabilite', 'Tresorerie')
ORDER BY c.Domaine, v.DisplayName;

/* ---------------------------------------------------------------------------
   3. Synthèse : si la première colonne vaut 0, le drapeau peut être activé
      sans casser de fenêtre existante.
   --------------------------------------------------------------------------- */
SELECT
    SUM(CASE WHEN c.TableName IS NULL AND v.IsActive = 1 THEN 1 ELSE 0 END) AS FenetresActivesCassees,
    SUM(CASE WHEN c.TableName IS NULL THEN 1 ELSE 0 END)                    AS FenetresTotalCassees,
    COUNT(*)                                                                AS FenetresTotal
FROM dbo.CustomViewDefinitions AS v
LEFT JOIN @Classified AS c ON c.TableName = v.SourceTable;
