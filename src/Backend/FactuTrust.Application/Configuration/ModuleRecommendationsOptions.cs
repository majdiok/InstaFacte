namespace FactuTrust.Application.Configuration;

/// <summary>
/// Plan §3.3 — module usage recommendations kill-switch and thresholds. All thresholds are counted
/// over a rolling <see cref="LookbackDays"/> window (default 30 ≈ "per month").
/// </summary>
public sealed class ModuleRecommendationsOptions
{
    public const string SectionName = "Features:ModuleRecommendations";

    public bool Enabled { get; set; } = true;

    /// <summary>Rolling window, in days, used to compute the "per month" business-activity counts.</summary>
    public int LookbackDays { get; set; } = 30;

    /// <summary>Quotes issued in the window at/above this count, with CRM inactive, suggest CRM.</summary>
    public int MinQuotesPerMonthForCrm { get; set; } = 5;

    /// <summary>Delivery notes issued in the window at/above this count, with Stock inactive, suggest Stock.</summary>
    public int MinDeliveryNotesPerMonthForStock { get; set; } = 5;

    /// <summary>
    /// Invoices issued in the window at/above this count, with RecurringContracts inactive, suggest
    /// RecurringContracts (recurring-looking invoicing volume).
    /// </summary>
    public int MinInvoicesPerMonthForRecurringContracts { get; set; } = 8;
}
