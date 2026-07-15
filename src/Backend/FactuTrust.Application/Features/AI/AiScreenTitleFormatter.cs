namespace FactuTrust.Application.Features.AI;

/// <summary>Maps screen ids to human-readable French labels for conversation titles.</summary>
public static class AiScreenTitleFormatter
{
    private static readonly Dictionary<string, string> ScreenLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["accounting-ledger"] = "Grand livre",
        ["accounting-journal"] = "Journal comptable",
        ["accounting-balance-sheet"] = "Bilan",
        ["accounting-income-statement"] = "Compte de résultat",
        ["accounting-balance"] = "Balance",
        ["accounting-chart"] = "Plan comptable",
        ["accounting-sub-journals"] = "Journaux auxiliaires",
        ["accounting-lettering"] = "Lettrage",
        ["accounting-closing"] = "Clôture comptable",
        ["accounting-vat-declaration"] = "Déclaration TVA",
        ["accounting-manual-entry"] = "Saisie manuelle",
        ["accounting-aging"] = "Balance âgée",
        ["invoice-list"] = "Liste des factures",
        ["cash-desk"] = "Caisse",
        ["stock-simple"] = "Stock",
        ["dashboard"] = "Tableau de bord",
        ["forecasting-replenishment"] = "Réapprovisionnement",
        ["forecasting-revenue"] = "Prévision de revenus",
        ["forecasting-abc-xyz"] = "Matrice ABC/XYZ",
        ["forecasting-promotions"] = "Promotions"
    };

    public static string FormatScreenIdForTitle(string? screenId)
    {
        if (string.IsNullOrWhiteSpace(screenId))
            return "écran";

        var trimmed = screenId.Trim();
        if (ScreenLabels.TryGetValue(trimmed, out var label))
            return label;

        return HumanizeScreenId(trimmed);
    }

    public static string BuildScreenAnalysisConversationTitle(string? screenId)
    {
        if (string.IsNullOrWhiteSpace(screenId))
            return "Analyse d'écran";

        return $"Analyse — {FormatScreenIdForTitle(screenId)}";
    }

    private static string HumanizeScreenId(string screenId)
    {
        return string.Join(' ',
            screenId.Split('-', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }
}
