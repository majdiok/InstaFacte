using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Features.Studio.Common.SqlReport;

/// <summary>Domaine métier d'une table, utilisé pour regrouper les sources et libeller l'aperçu.</summary>
public enum ReportDomain
{
    /// <summary>Table non classée : refusée par construction (deny-by-default).</summary>
    Inconnu = 0,
    Referentiel,
    Ventes,
    Achats,
    Stock,
    Tresorerie,
    Comptabilite,
    Paie,
    Crm,
    Previsionnel,
    Projets
}

/// <summary>Une table de la base tenant ouverte au moteur d'états, avec son domaine et son droit d'accès.</summary>
public sealed record ReportTableAccess(
    string Table,
    string DisplayName,
    ReportDomain Domain,
    string RequiredPermission);

/// <summary>
/// Politique d'accès du moteur d'états sur les tables réelles du tenant. Pure et sans dépendance,
/// donc testable unitairement.
///
/// Trois règles, dans cet ordre :
/// 1. La table doit passer <see cref="SqlSchemaGuard"/> (identifiant valide, hors liste de refus) ;
/// 2. elle doit être CLASSÉE ici — une table absente de la carte est <see cref="ReportDomain.Inconnu"/>
///    et donc REFUSÉE : une table ajoutée au schéma ne devient jamais lisible par accident ;
/// 3. l'utilisateur doit détenir <see cref="BasePermission"/> ET la permission propre à la table.
///
/// Le filtrage de colonnes (<see cref="FilterColumns"/>) retire en plus les secrets et jetons de
/// concurrence, quelle que soit la table.
/// </summary>
public static class SqlReportAccessPolicy
{
    /// <summary>Socle commun : lire un état reste un droit de reporting, comme sur <c>api/Reports</c>.</summary>
    public const string BasePermission = Permissions.Reports.View;

    private static readonly IReadOnlyDictionary<string, ReportTableAccess> Catalog = Build();

    /// <summary>Renvoie le classement d'une table, ou <c>null</c> si elle n'est pas ouverte au reporting.</summary>
    public static ReportTableAccess? Describe(string? table)
    {
        if (string.IsNullOrWhiteSpace(table)
            || !SqlSchemaGuard.IsValidIdentifier(table)
            || SqlSchemaGuard.IsDenied(table!))
            return null;

        return Catalog.TryGetValue(table!, out var access) ? access : null;
    }

    /// <summary>
    /// Autorise (ou non) la lecture d'une table. <paramref name="hasPermission"/> est branché sur
    /// <c>ICurrentUser.HasPermission</c> : la politique reste pure et l'appelant fournit le contexte.
    /// </summary>
    public static bool TryAuthorize(
        string? table,
        Func<string, bool> hasPermission,
        out ReportTableAccess? access,
        out string? error)
    {
        access = null;
        error = null;

        var described = Describe(table);
        if (described is null)
        {
            error = $"La table « {table} » n'est pas ouverte aux états.";
            return false;
        }

        if (!hasPermission(BasePermission))
        {
            error = "Vous n'avez pas le droit de consulter les états.";
            return false;
        }

        if (!hasPermission(described.RequiredPermission))
        {
            error = $"Vous n'avez pas accès aux données « {DomainLabel(described.Domain)} ».";
            return false;
        }

        access = described;
        return true;
    }

    /// <summary>Toutes les tables lisibles par cet utilisateur, triées par domaine puis par libellé.</summary>
    public static IReadOnlyList<ReportTableAccess> AllowedFor(Func<string, bool> hasPermission)
    {
        if (!hasPermission(BasePermission))
            return Array.Empty<ReportTableAccess>();

        return Catalog.Values
            .Where(a => hasPermission(a.RequiredPermission))
            .OrderBy(a => a.Domain)
            .ThenBy(a => a.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Retire les colonnes interdites (secrets, empreintes, jeton de concurrence).</summary>
    public static IReadOnlyList<SqlColumnInfo> FilterColumns(IEnumerable<SqlColumnInfo>? columns) =>
        (columns ?? Array.Empty<SqlColumnInfo>())
            .Where(c => !SqlSchemaGuard.IsDeniedColumn(c.Name))
            .ToList();

    public static string DomainLabel(ReportDomain domain) => domain switch
    {
        ReportDomain.Referentiel => "Référentiel",
        ReportDomain.Ventes => "Ventes",
        ReportDomain.Achats => "Achats",
        ReportDomain.Stock => "Stock",
        ReportDomain.Tresorerie => "Trésorerie",
        ReportDomain.Comptabilite => "Comptabilité",
        ReportDomain.Paie => "Paie & RH",
        ReportDomain.Crm => "CRM",
        ReportDomain.Previsionnel => "Prévisionnel",
        ReportDomain.Projets => "Projets",
        _ => "Non classé"
    };

    // ---- Carte des sources ----------------------------------------------------------------
    // Volontairement explicite : une table absente d'ici est refusée. Ajouter une entrée est une
    // décision d'ouverture de données, elle doit se voir en revue de code.

    private static IReadOnlyDictionary<string, ReportTableAccess> Build()
    {
        var map = new Dictionary<string, ReportTableAccess>(StringComparer.OrdinalIgnoreCase);

        void Add(string table, string label, ReportDomain domain, string permission) =>
            map[table] = new ReportTableAccess(table, label, domain, permission);

        // --- Référentiel ---
        Add("Clients", "Clients", ReportDomain.Referentiel, Permissions.Clients.Read);
        Add("Products", "Produits", ReportDomain.Referentiel, Permissions.Products.Read);
        Add("ProductCategories", "Catégories de produits", ReportDomain.Referentiel, Permissions.Products.Read);
        Add("Taxes", "Taxes", ReportDomain.Referentiel, Permissions.Products.Read);
        Add("Suppliers", "Fournisseurs", ReportDomain.Referentiel, Permissions.Suppliers.Read);
        Add("Warehouses", "Entrepôts", ReportDomain.Referentiel, Permissions.Stock.Read);
        Add("PriceLists", "Grilles tarifaires", ReportDomain.Referentiel, Permissions.Pricing.Read);
        Add("PriceListItems", "Lignes de grille tarifaire", ReportDomain.Referentiel, Permissions.Pricing.Read);
        Add("PriceListItemTiers", "Paliers de grille tarifaire", ReportDomain.Referentiel, Permissions.Pricing.Read);
        Add("ClientProductPrices", "Prix négociés par client", ReportDomain.Referentiel, Permissions.Pricing.Read);
        Add("Promotions", "Promotions", ReportDomain.Referentiel, Permissions.Pricing.Read);
        Add("PaymentTermTemplates", "Conditions de règlement", ReportDomain.Referentiel, Permissions.Settings.Read);

        // --- Ventes ---
        Add("Invoices", "Factures de vente", ReportDomain.Ventes, Permissions.Invoices.Read);
        Add("InvoiceLines", "Lignes de facture de vente", ReportDomain.Ventes, Permissions.Invoices.Read);
        Add("Quotes", "Devis", ReportDomain.Ventes, Permissions.Quotes.Read);
        Add("QuoteLines", "Lignes de devis", ReportDomain.Ventes, Permissions.Quotes.Read);
        Add("SalesOrders", "Commandes clients", ReportDomain.Ventes, Permissions.SalesOrders.Read);
        Add("SalesOrderLines", "Lignes de commande client", ReportDomain.Ventes, Permissions.SalesOrders.Read);
        Add("DeliveryNotes", "Bons de livraison", ReportDomain.Ventes, Permissions.DeliveryNotes.Read);
        Add("DeliveryNoteLines", "Lignes de bon de livraison", ReportDomain.Ventes, Permissions.DeliveryNotes.Read);
        Add("SalesReturnNotes", "Bons de retour", ReportDomain.Ventes, Permissions.SalesReturnNotes.Read);
        Add("SalesReturnNoteLines", "Lignes de bon de retour", ReportDomain.Ventes, Permissions.SalesReturnNotes.Read);

        // --- Achats ---
        Add("PurchaseOrders", "Commandes fournisseurs", ReportDomain.Achats, Permissions.PurchaseOrders.Read);
        Add("PurchaseOrderLines", "Lignes de commande fournisseur", ReportDomain.Achats, Permissions.PurchaseOrders.Read);
        Add("PurchaseReceipts", "Réceptions fournisseurs", ReportDomain.Achats, Permissions.PurchaseReceipts.Read);
        Add("PurchaseReceiptLines", "Lignes de réception", ReportDomain.Achats, Permissions.PurchaseReceipts.Read);
        Add("SupplierInvoices", "Factures fournisseurs", ReportDomain.Achats, Permissions.SupplierInvoices.Read);
        Add("SupplierInvoiceLines", "Lignes de facture fournisseur", ReportDomain.Achats, Permissions.SupplierInvoices.Read);

        // --- Stock ---
        Add("StockItems", "Stock par entrepôt", ReportDomain.Stock, Permissions.Stock.Read);
        Add("StockMovements", "Mouvements de stock", ReportDomain.Stock, Permissions.Stock.Read);
        Add("StockTransfers", "Transferts de stock", ReportDomain.Stock, Permissions.StockTransfers.Read);
        Add("StockTransferLines", "Lignes de transfert", ReportDomain.Stock, Permissions.StockTransfers.Read);
        Add("PhysicalInventories", "Inventaires physiques", ReportDomain.Stock, Permissions.Inventory.Read);
        Add("InventoryCountLines", "Lignes d'inventaire", ReportDomain.Stock, Permissions.Inventory.Read);

        // --- Trésorerie ---
        Add("Payments", "Encaissements clients", ReportDomain.Tresorerie, Permissions.Payments.Read);
        Add("SupplierPayments", "Décaissements fournisseurs", ReportDomain.Tresorerie, Permissions.Payments.Read);
        Add("CashOperations", "Opérations de caisse", ReportDomain.Tresorerie, Permissions.Payments.Read);
        Add("BankAccounts", "Comptes bancaires", ReportDomain.Tresorerie, Permissions.Payments.Read);
        Add("BankDeposits", "Remises en banque", ReportDomain.Tresorerie, Permissions.Payments.Read);
        Add("BankStatements", "Relevés bancaires", ReportDomain.Tresorerie, Permissions.Accounting.Read);
        Add("BankStatementLines", "Lignes de relevé bancaire", ReportDomain.Tresorerie, Permissions.Accounting.Read);

        // --- Comptabilité ---
        Add("JournalEntries", "Écritures comptables", ReportDomain.Comptabilite, Permissions.Accounting.Read);
        Add("JournalEntryLines", "Lignes d'écriture", ReportDomain.Comptabilite, Permissions.Accounting.Read);
        Add("Journals", "Journaux", ReportDomain.Comptabilite, Permissions.Accounting.Read);
        Add("JournalFamilies", "Familles de journaux", ReportDomain.Comptabilite, Permissions.Accounting.Read);
        Add("ChartOfAccounts", "Plan comptable", ReportDomain.Comptabilite, Permissions.Accounting.Read);
        Add("AccountingPeriods", "Périodes comptables", ReportDomain.Comptabilite, Permissions.Accounting.Read);
        Add("LetteringGroups", "Groupes de lettrage", ReportDomain.Comptabilite, Permissions.Accounting.Read);
        Add("LetteringGroupMembers", "Membres de lettrage", ReportDomain.Comptabilite, Permissions.Accounting.Read);
        Add("VatDeclarations", "Déclarations de TVA", ReportDomain.Comptabilite, Permissions.Accounting.Read);
        Add("FiscalScheduleEntries", "Échéancier fiscal", ReportDomain.Comptabilite, Permissions.Accounting.Read);
        Add("ThirdPartyAccountingProfiles", "Comptes de tiers", ReportDomain.Comptabilite, Permissions.Accounting.Read);
        Add("WithholdingTaxTypes", "Types de retenue à la source", ReportDomain.Comptabilite, Permissions.Accounting.Read);
        Add("BudgetPosts", "Postes budgétaires", ReportDomain.Comptabilite, Permissions.Accounting.Read);
        Add("BudgetYears", "Exercices budgétaires", ReportDomain.Comptabilite, Permissions.Accounting.Read);
        Add("BudgetLines", "Lignes de budget", ReportDomain.Comptabilite, Permissions.Accounting.Read);
        Add("FixedAssets", "Immobilisations", ReportDomain.Comptabilite, Permissions.Accounting.Read);
        Add("FixedAssetEvents", "Événements d'immobilisation", ReportDomain.Comptabilite, Permissions.Accounting.Read);
        Add("DepreciationScheduleLines", "Plan d'amortissement", ReportDomain.Comptabilite, Permissions.Accounting.Read);
        Add("DepreciationRateCategories", "Catégories d'amortissement", ReportDomain.Comptabilite, Permissions.Accounting.Read);
        Add("Loans", "Emprunts", ReportDomain.Comptabilite, Permissions.Accounting.Read);
        Add("LoanScheduleLines", "Échéances d'emprunt", ReportDomain.Comptabilite, Permissions.Accounting.Read);

        // --- Paie & RH (données sensibles : permission dédiée) ---
        Add("Employees", "Salariés", ReportDomain.Paie, Permissions.Payroll.Read);
        Add("EmploymentContracts", "Contrats de travail", ReportDomain.Paie, Permissions.Payroll.Read);
        Add("ContractAllowances", "Primes contractuelles", ReportDomain.Paie, Permissions.Payroll.Read);
        Add("PayrollRuns", "Mois de paie", ReportDomain.Paie, Permissions.Payroll.Read);
        Add("Payslips", "Bulletins de paie", ReportDomain.Paie, Permissions.Payroll.Read);
        Add("PayslipLines", "Lignes de bulletin", ReportDomain.Paie, Permissions.Payroll.Read);
        Add("PayrollPayments", "Règlements de paie", ReportDomain.Paie, Permissions.Payroll.Read);
        Add("PayrollPaymentLines", "Lignes de règlement de paie", ReportDomain.Paie, Permissions.Payroll.Read);
        Add("PayrollOvertimeLines", "Heures supplémentaires", ReportDomain.Paie, Permissions.Payroll.Read);
        Add("PayrollVariableAllowanceLines", "Primes variables", ReportDomain.Paie, Permissions.Payroll.Read);
        Add("LeaveRequests", "Demandes de congé", ReportDomain.Paie, Permissions.Payroll.Read);
        Add("LeaveBalanceAccruals", "Acquisition de congés", ReportDomain.Paie, Permissions.Payroll.Read);
        Add("EmployeeAdvances", "Avances sur salaire", ReportDomain.Paie, Permissions.Payroll.Read);
        Add("CnssContributionPayments", "Cotisations CNSS", ReportDomain.Paie, Permissions.Payroll.Read);

        // --- CRM ---
        Add("Opportunities", "Opportunités", ReportDomain.Crm, Permissions.CRM.Read);
        Add("SalesActivities", "Activités commerciales", ReportDomain.Crm, Permissions.CRM.Read);
        Add("SalesTargets", "Objectifs commerciaux", ReportDomain.Crm, Permissions.SalesTargets.Read);

        // --- Prévisionnel ---
        Add("SalesForecasts", "Prévisions de vente", ReportDomain.Previsionnel, Permissions.Forecasting.View);
        Add("ReplenishmentRecommendations", "Recommandations de réappro", ReportDomain.Previsionnel, Permissions.Forecasting.View);
        Add("PromotionRecommendations", "Recommandations de promotion", ReportDomain.Previsionnel, Permissions.Forecasting.View);
        Add("ProductAbcXyzClassifications", "Classification ABC/XYZ", ReportDomain.Previsionnel, Permissions.Forecasting.View);
        Add("CashFlowForecastRuns", "Runs de prévision de trésorerie", ReportDomain.Previsionnel, Permissions.TreasuryForecast.View);
        Add("CashFlowForecastLines", "Lignes de prévision de trésorerie", ReportDomain.Previsionnel, Permissions.TreasuryForecast.View);
        Add("CashFlowForecastBuckets", "Périodes de prévision de trésorerie", ReportDomain.Previsionnel, Permissions.TreasuryForecast.View);

        Add("Projects", "Projets", ReportDomain.Projets, Permissions.Projects.Read);
        Add("ProjectTasks", "Tâches projet", ReportDomain.Projets, Permissions.ProjectTasks.Read);
        Add("ProjectTimeEntries", "Temps projet", ReportDomain.Projets, Permissions.ProjectTime.Read);
        Add("ProjectCostLines", "Coûts projet", ReportDomain.Projets, Permissions.Projects.Read);
        Add("ProjectSituations", "Situations de travaux", ReportDomain.Projets, Permissions.ProjectBilling.Read);
        Add("ProjectMilestones", "Jalons projet", ReportDomain.Projets, Permissions.ProjectBilling.Read);
        Add("ProjectMembers", "Équipe projet", ReportDomain.Projets, Permissions.Projects.Read);

        return map;
    }
}
