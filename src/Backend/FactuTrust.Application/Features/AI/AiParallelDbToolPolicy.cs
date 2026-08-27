using FactuTrust.Application.Features.AI.Tools;

namespace FactuTrust.Application.Features.AI;

/// <summary>
/// Whitelist des outils read-only sûrs à exécuter en parallèle (un DbContext par scope DI).
/// Les outils mutants ou ambigus restent séquentiels.
/// </summary>
public static class AiParallelDbToolPolicy
{
    private static readonly HashSet<string> ReadOnlyDbToolNames = new(StringComparer.Ordinal)
    {
        "get_sales_revenue",
        "get_client_payments",
        "get_client_balances",
        "get_commercial_profit",
        "get_stock_snapshot",
        "get_product_performance",
        "get_basket_metrics",
        "get_accounting_dashboard",
        "get_client_aging",
        "get_product_sales_trend",
        "get_supplier_balances",
        "get_products_never_sold",
        "get_stock_movements",
        "forecast_revenue",
        "get_replenishment_recommendations",
        "get_promotion_recommendations",
        "get_abc_xyz_classification",
        "compliance_check_invoice"
    };

    /// <summary>
    /// Outils firm en LECTURE SEULE, dédiés au cache inter-requêtes (Lot 3.3). Les 4 <c>get_firm_*</c>
    /// uniquement — JAMAIS <c>send_fiscal_deadline_reminder</c> (mutation). Liste DISTINCTE de
    /// <see cref="ReadOnlyDbToolNames"/> : elle ne pilote PAS le parallélisme tenant, et un rollback
    /// firm n'affecte pas la liste tenant (et réciproquement).
    /// </summary>
    private static readonly HashSet<string> FirmReadOnlyDbToolNames = new(StringComparer.Ordinal)
    {
        FirmAgentTools.PortfolioOverview,
        FirmAgentTools.FiscalDeadlines,
        FirmAgentTools.DossierHealth,
        FirmAgentTools.CollaboratorWorkload
    };

    public static bool IsSafe(string toolName)
    {
        if (!ReadOnlyDbToolNames.Contains(toolName))
            return false;

        var def = AiToolRegistry.GetToolDefinition(toolName);
        return def is not null && !def.IsMutating;
    }

    /// <summary>
    /// Indique si <paramref name="toolName"/> est un outil firm en lecture seule (<c>get_firm_*</c>),
    /// donc éligible au cache inter-requêtes firm (sous flag + scope dédiés, cf. <c>AiReadOnlyToolCache</c>).
    /// <c>send_fiscal_deadline_reminder</c> renvoie <c>false</c>.
    /// </summary>
    public static bool IsFirmReadOnly(string toolName) => FirmReadOnlyDbToolNames.Contains(toolName);
}