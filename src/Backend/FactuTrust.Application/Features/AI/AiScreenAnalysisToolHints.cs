namespace FactuTrust.Application.Features.AI;

/// <summary>Suggested complementary tools per screen for screen analysis mode.</summary>
public static class AiScreenAnalysisToolHints
{
    private static readonly Dictionary<string, string[]> Hints = new(StringComparer.OrdinalIgnoreCase)
    {
        ["accounting-income-statement"] = ["get_commercial_profit", "get_accounting_dashboard"],
        ["accounting-balance-sheet"] = ["get_accounting_dashboard"],
        ["accounting-balance"] = ["get_accounting_dashboard"],
        ["accounting-aging"] = ["get_client_aging", "get_client_balances"],
        ["accounting-lettering"] = ["get_client_balances"],
        ["cash-desk"] = ["get_client_payments"],
        ["dashboard"] = ["get_sales_revenue", "get_stock_snapshot"],
        ["invoice-list"] = ["get_client_balances", "get_sales_revenue"],
        ["credit-note-list"] = ["get_client_balances", "get_sales_revenue"],
        ["stock-simple"] = ["get_stock_snapshot"],
        ["forecasting-revenue"] = ["forecast_revenue"],
        ["treasury-cash-forecast"] = ["get_cash_flow_forecast", "get_cash_flow_lines"],
        ["forecasting-replenishment"] = ["get_replenishment_recommendations"],
        ["forecasting-abc-xyz"] = ["get_abc_xyz_classification"],
        ["forecasting-promotions"] = ["get_promotion_recommendations"]
    };

    public static IReadOnlyList<string> GetHintsForScreen(string? screenId)
    {
        if (string.IsNullOrWhiteSpace(screenId))
            return Array.Empty<string>();

        return Hints.TryGetValue(screenId, out var tools) ? tools : Array.Empty<string>();
    }
}
