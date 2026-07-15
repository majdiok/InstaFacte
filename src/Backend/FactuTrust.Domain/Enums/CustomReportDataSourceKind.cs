namespace FactuTrust.Domain.Enums;

/// <summary>
/// Where a custom report reads its data from. Append-only for migration safety.
/// </summary>
public enum CustomReportDataSourceKind
{
    /// <summary>A custom entity (records stored as JSON in CustomRecord).</summary>
    CustomEntity = 0,

    /// <summary>A whitelisted, read-only existing first-party source (e.g. Invoices, Clients).</summary>
    ExistingSource = 1
}
