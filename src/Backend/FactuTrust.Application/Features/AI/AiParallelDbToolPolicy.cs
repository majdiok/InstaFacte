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

    public static bool IsSafe(string toolName)
    {
        if (!ReadOnlyDbToolNames.Contains(toolName))
            return false;

        var def = AiToolRegistry.GetToolDefinition(toolName);
        return def is not null && !def.IsMutating;
    }
}