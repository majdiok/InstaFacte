namespace FactuTrust.Application.Configuration;

/// <summary>
/// Kill-switch for the native Recurring Contracts / B2B subscriptions module.
/// Bound from <c>Features:RecurringContracts</c>.
/// </summary>
public sealed class RecurringContractsOptions
{
    public const string SectionName = "Features:RecurringContracts";

    public bool Enabled { get; set; }

    public bool BillingJobEnabled { get; set; } = true;

    /// <summary>Days before NextBillingDate to create draft invoices.</summary>
    public int BillingWindowDays { get; set; } = 3;
}
