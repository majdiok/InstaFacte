using System.Text.Json.Nodes;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Features.Studio.Common.SqlReport;

/// <summary>
/// Un état métier prêt à l'emploi : source, regroupement, mesures et filtres de base sont écrits ici,
/// en dur. Le demandeur ne choisit qu'une PÉRIODE et éventuellement un regroupement de remplacement —
/// c'est-à-dire une valeur dans un ensemble fermé, jamais une requête.
/// </summary>
public sealed record SqlReportPreset(
    string Key,
    string DisplayName,
    string Description,
    ReportDomain Domain,
    string FactTable,
    string? PeriodFieldKey,
    IReadOnlyList<string> Grouping,
    IReadOnlyList<ReportAggregation> Aggregations,
    IReadOnlyList<ReportFilter> BaseFilters,
    IReadOnlyList<ReportSort> Sort);

/// <summary>
/// Catalogue des états prêts à l'emploi. Il sert deux besoins :
/// <list type="bullet">
/// <item>le petit modèle sur CPU ne dispose que d'UN tour d'outil (<c>CpuMaxToolCallRounds = 1</c>) :
/// il ne peut pas explorer le schéma puis planifier. Choisir un préréglage lui suffit ;</item>
/// <item>les chiffres des états les plus regardés (ventes, marge, encaissements) deviennent
/// reproductibles et vérifiables ligne à ligne contre les états historiques.</item>
/// </list>
/// Un préréglage dont une colonne n'existe pas dans la base est MASQUÉ
/// (<see cref="ResolvesAgainst"/>) plutôt que d'être exécuté partiellement : un état faux coûte plus
/// cher qu'un état absent.
/// </summary>
public static class SqlReportPresetCatalog
{
    /// <summary>Factures réalisées : Validée (1) ou Payée (4) — miroir de <c>GetSalesRevenueAggregatedAsync</c>.</summary>
    private static readonly ReportFilter RealizedInvoices = new()
    {
        Field = "Invoices_Status",
        Op = "in",
        Value = new JsonArray(
            JsonValue.Create((int)InvoiceStatus.Validated),
            JsonValue.Create((int)InvoiceStatus.Paid))
    };

    private static ReportAggregation Sum(string field) => new() { Field = field, Fn = "sum" };
    private static ReportSort Desc(string field) => new() { Field = field, Dir = "desc" };
    private static ReportSort Asc(string field) => new() { Field = field, Dir = "asc" };

    public static IReadOnlyList<SqlReportPreset> All { get; } = new[]
    {
        // ---- Ventes (colonnes vérifiées contre la configuration EF) ----
        new SqlReportPreset(
            "ventes_par_produit", "Ventes par produit",
            "Chiffre d'affaires, quantités et TVA par produit, sur une période.",
            ReportDomain.Ventes, "InvoiceLines", "Invoices_IssueDate",
            new[] { "InvoiceLines_ProductName" },
            new[] { Sum("InvoiceLines_Total"), Sum("InvoiceLines_Quantity"), Sum("InvoiceLines_VatAmount") },
            new[] { RealizedInvoices },
            new[] { Desc("sum_InvoiceLines_Total") }),

        new SqlReportPreset(
            "ventes_par_client", "Ventes par client",
            "Chiffre d'affaires et quantités par client, sur une période.",
            ReportDomain.Ventes, "InvoiceLines", "Invoices_IssueDate",
            new[] { "Clients_Name" },
            new[] { Sum("InvoiceLines_Total"), Sum("InvoiceLines_Quantity") },
            new[] { RealizedInvoices },
            new[] { Desc("sum_InvoiceLines_Total") }),

        new SqlReportPreset(
            "ventes_par_mois", "Ventes par mois",
            "Évolution mensuelle du chiffre d'affaires et de la TVA collectée.",
            ReportDomain.Ventes, "InvoiceLines", "Invoices_IssueDate",
            new[] { "Invoices_IssueDate__month" },
            new[] { Sum("InvoiceLines_Total"), Sum("InvoiceLines_VatAmount") },
            new[] { RealizedInvoices },
            new[] { Asc("Invoices_IssueDate__month") }),

        new SqlReportPreset(
            "ventes_par_produit_et_mois", "Ventes par produit et par mois",
            "Croisement produit × mois : détecte la saisonnalité et les décrochages.",
            ReportDomain.Ventes, "InvoiceLines", "Invoices_IssueDate",
            new[] { "InvoiceLines_ProductName", "Invoices_IssueDate__month" },
            new[] { Sum("InvoiceLines_Total"), Sum("InvoiceLines_Quantity") },
            new[] { RealizedInvoices },
            new[] { Desc("sum_InvoiceLines_Total") }),

        new SqlReportPreset(
            "remises_accordees", "Remises accordées par client",
            "Montant des remises consenties, par client — repère la fuite de marge.",
            ReportDomain.Ventes, "InvoiceLines", "Invoices_IssueDate",
            new[] { "Clients_Name" },
            new[] { Sum("InvoiceLines_DiscountAmount"), Sum("InvoiceLines_Total") },
            new[] { RealizedInvoices },
            new[] { Desc("sum_InvoiceLines_DiscountAmount") }),

        new SqlReportPreset(
            "detail_lignes_ventes", "Détail des lignes de vente",
            "Lignes de facture une à une : date, client, produit, quantité, prix, total.",
            ReportDomain.Ventes, "InvoiceLines", "Invoices_IssueDate",
            Array.Empty<string>(),
            Array.Empty<ReportAggregation>(),
            new[] { RealizedInvoices },
            new[] { Desc("Invoices_IssueDate") })
        {
            // État de DÉTAIL : les colonnes projetées tiennent lieu de regroupement.
        },

        new SqlReportPreset(
            "factures_par_statut", "Factures par statut",
            "Nombre et montant des factures, réparties par statut.",
            ReportDomain.Ventes, "Invoices", "Invoices_IssueDate",
            new[] { "Invoices_Status" },
            new[] { Sum("Invoices_TotalAmount"), new ReportAggregation { Fn = "count" } },
            Array.Empty<ReportFilter>(),
            new[] { Desc("sum_Invoices_TotalAmount") }),

        // ---- Achats ----
        new SqlReportPreset(
            "achats_par_fournisseur", "Achats par fournisseur",
            "Montant acheté par fournisseur sur une période.",
            ReportDomain.Achats, "SupplierInvoices", "SupplierInvoices_InvoiceDate",
            new[] { "Suppliers_Name" },
            new[] { Sum("SupplierInvoices_TotalAmount"), new ReportAggregation { Fn = "count" } },
            Array.Empty<ReportFilter>(),
            new[] { Desc("sum_SupplierInvoices_TotalAmount") }),

        new SqlReportPreset(
            "achats_par_mois", "Achats par mois",
            "Évolution mensuelle des achats fournisseurs.",
            ReportDomain.Achats, "SupplierInvoices", "SupplierInvoices_InvoiceDate",
            new[] { "SupplierInvoices_InvoiceDate__month" },
            new[] { Sum("SupplierInvoices_TotalAmount") },
            Array.Empty<ReportFilter>(),
            new[] { Asc("SupplierInvoices_InvoiceDate__month") }),

        new SqlReportPreset(
            "achats_par_produit", "Achats par produit",
            "Quantités et montants achetés par produit.",
            ReportDomain.Achats, "SupplierInvoiceLines", null,
            new[] { "Products_Name" },
            new[] { Sum("SupplierInvoiceLines_Quantity") },
            Array.Empty<ReportFilter>(),
            new[] { Desc("sum_SupplierInvoiceLines_Quantity") }),

        // ---- Stock ----
        new SqlReportPreset(
            "mouvements_de_stock", "Mouvements de stock",
            "Mouvements par produit : entrées, sorties, ajustements.",
            ReportDomain.Stock, "StockMovements", null,
            new[] { "Products_Name" },
            new[] { Sum("StockMovements_Quantity"), new ReportAggregation { Fn = "count" } },
            Array.Empty<ReportFilter>(),
            new[] { Desc("sum_StockMovements_Quantity") }),

        new SqlReportPreset(
            "stock_par_entrepot", "Stock par entrepôt",
            "Quantités disponibles par entrepôt et par produit.",
            ReportDomain.Stock, "StockItems", null,
            new[] { "Warehouses_Name", "Products_Name" },
            new[] { Sum("StockItems_Quantity") },
            Array.Empty<ReportFilter>(),
            new[] { Desc("sum_StockItems_Quantity") }),

        // ---- Trésorerie ----
        new SqlReportPreset(
            "encaissements_par_mois", "Encaissements par mois",
            "Règlements clients encaissés, mois par mois.",
            ReportDomain.Tresorerie, "Payments", "Payments_PaymentDate",
            new[] { "Payments_PaymentDate__month" },
            new[] { Sum("Payments_Amount"), new ReportAggregation { Fn = "count" } },
            Array.Empty<ReportFilter>(),
            new[] { Asc("Payments_PaymentDate__month") }),

        new SqlReportPreset(
            "encaissements_par_mode", "Encaissements par mode de règlement",
            "Répartition des encaissements par mode (espèces, chèque, virement…).",
            ReportDomain.Tresorerie, "Payments", "Payments_PaymentDate",
            new[] { "Payments_Method" },
            new[] { Sum("Payments_Amount"), new ReportAggregation { Fn = "count" } },
            Array.Empty<ReportFilter>(),
            new[] { Desc("sum_Payments_Amount") }),

        new SqlReportPreset(
            "encaissements_par_client", "Encaissements par client",
            "Montant encaissé par client sur une période.",
            ReportDomain.Tresorerie, "Payments", "Payments_PaymentDate",
            new[] { "Clients_Name" },
            new[] { Sum("Payments_Amount") },
            Array.Empty<ReportFilter>(),
            new[] { Desc("sum_Payments_Amount") }),

        // ---- Comptabilité ----
        new SqlReportPreset(
            "ecritures_par_compte", "Écritures par compte",
            "Débit et crédit cumulés par compte du plan comptable.",
            ReportDomain.Comptabilite, "JournalEntryLines", null,
            new[] { "JournalEntryLines_AccountNumber" },
            new[] { Sum("JournalEntryLines_Debit"), Sum("JournalEntryLines_Credit") },
            Array.Empty<ReportFilter>(),
            new[] { Asc("JournalEntryLines_AccountNumber") }),

        new SqlReportPreset(
            "ecritures_par_journal", "Écritures par journal",
            "Volume et montants par journal comptable.",
            ReportDomain.Comptabilite, "JournalEntries", "JournalEntries_EntryDate",
            new[] { "Journals_Code" },
            new[] { new ReportAggregation { Fn = "count" } },
            Array.Empty<ReportFilter>(),
            new[] { Desc("count") }),

        // ---- Paie ----
        new SqlReportPreset(
            "masse_salariale_par_mois", "Masse salariale par mois",
            "Brut, net et charges par mois de paie.",
            ReportDomain.Paie, "Payslips", null,
            new[] { "PayrollRuns_Year", "PayrollRuns_Month" },
            new[] { Sum("Payslips_GrossSalary"), Sum("Payslips_NetSalary") },
            Array.Empty<ReportFilter>(),
            new[] { Asc("PayrollRuns_Year"), Asc("PayrollRuns_Month") }),

        new SqlReportPreset(
            "effectif_par_contrat", "Effectif par type de contrat",
            "Nombre de salariés par type de contrat.",
            ReportDomain.Paie, "EmploymentContracts", null,
            new[] { "EmploymentContracts_ContractType" },
            new[] { new ReportAggregation { Fn = "count" } },
            Array.Empty<ReportFilter>(),
            new[] { Desc("count") })
    };

    public static SqlReportPreset? Find(string? key) =>
        string.IsNullOrWhiteSpace(key)
            ? null
            : All.FirstOrDefault(p => string.Equals(p.Key, key!.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Vrai si TOUTES les colonnes du préréglage existent dans le schéma photographié. Faux ⇒ le
    /// préréglage est masqué : jamais exécuté à moitié.
    /// </summary>
    public static bool ResolvesAgainst(SqlReportPreset preset, SqlReportSchemaSnapshot snapshot)
    {
        if (!string.Equals(preset.FactTable, snapshot.FactTable, StringComparison.OrdinalIgnoreCase))
            return false;

        foreach (var key in preset.Grouping)
            if (!SqlReportSqlBuilder.CanResolve(snapshot, key)) return false;

        foreach (var aggregation in preset.Aggregations)
            if (!string.Equals(aggregation.Fn, "count", StringComparison.OrdinalIgnoreCase)
                && !SqlReportSqlBuilder.CanResolve(snapshot, aggregation.Field)) return false;

        foreach (var filter in preset.BaseFilters)
            if (!SqlReportSqlBuilder.CanResolve(snapshot, filter.Field)) return false;

        return preset.PeriodFieldKey is null || SqlReportSqlBuilder.CanResolve(snapshot, preset.PeriodFieldKey);
    }

    /// <summary>
    /// Transforme un préréglage en <see cref="ReportDefinition"/> : les filtres de base sont
    /// TOUJOURS conservés (ils portent la définition métier de l'état), la période et les filtres
    /// supplémentaires viennent s'y ajouter.
    /// </summary>
    public static ReportDefinition Materialize(
        SqlReportPreset preset,
        DateOnly? from = null,
        DateOnly? to = null,
        IEnumerable<ReportFilter>? extraFilters = null,
        IReadOnlyList<string>? overrideGrouping = null)
    {
        var filters = new List<ReportFilter>(preset.BaseFilters);

        if (preset.PeriodFieldKey is not null && (from is not null || to is not null))
        {
            if (from is not null && to is not null)
                filters.Add(new ReportFilter
                {
                    Field = preset.PeriodFieldKey,
                    Op = "between",
                    Value = JsonValue.Create(from.Value.ToString("yyyy-MM-dd")),
                    Value2 = JsonValue.Create(to.Value.ToString("yyyy-MM-dd"))
                });
            else if (from is not null)
                filters.Add(new ReportFilter
                {
                    Field = preset.PeriodFieldKey,
                    Op = "gte",
                    Value = JsonValue.Create(from.Value.ToString("yyyy-MM-dd"))
                });
            else
                filters.Add(new ReportFilter
                {
                    Field = preset.PeriodFieldKey,
                    Op = "lte",
                    Value = JsonValue.Create(to!.Value.ToString("yyyy-MM-dd"))
                });
        }

        if (extraFilters is not null)
            filters.AddRange(extraFilters);

        var grouping = overrideGrouping is { Count: > 0 } ? overrideGrouping : preset.Grouping;

        return new ReportDefinition
        {
            Grouping = grouping,
            Aggregations = grouping.Count > 0 ? preset.Aggregations : Array.Empty<ReportAggregation>(),
            Filters = filters,
            Sort = preset.Sort
        };
    }
}
