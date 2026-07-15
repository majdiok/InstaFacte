namespace FactuTrust.Domain.Enums;

/// <summary>
/// Represents the lifecycle status of an invoice.
/// </summary>
public enum InvoiceStatus
{
    /// <summary>
    /// Draft invoice - can be edited or deleted.
    /// </summary>
    Draft = 0,

    /// <summary>
    /// Validated invoice - ready to be signed and sent.
    /// Cannot be edited, only cancelled.
    /// </summary>
    Validated = 1,

    /// <summary>
    /// Signed invoice - has been digitally signed.
    /// </summary>
    Signed = 2,

    /// <summary>
    /// Paid invoice - payment has been received.
    /// </summary>
    Paid = 4,

    /// <summary>
    /// Partially paid invoice.
    /// </summary>
    PartiallyPaid = 5,

    /// <summary>
    /// Overdue invoice - past due date without full payment.
    /// </summary>
    Overdue = 6,

    /// <summary>
    /// Cancelled invoice - voided but kept for audit trail.
    /// </summary>
    Cancelled = 7,

    /// <summary>
    /// Archived invoice - stored for legal retention.
    /// </summary>
    Archived = 8
}

public static class InvoiceStatusExtensions
{
    public static bool CanBeEdited(this InvoiceStatus status) => status == InvoiceStatus.Draft;

    public static bool CanBeSigned(this InvoiceStatus status) => status == InvoiceStatus.Validated;

    public static bool CanBePaymentRecorded(this InvoiceStatus status) =>
        status is InvoiceStatus.Signed or InvoiceStatus.Validated or InvoiceStatus.PartiallyPaid or InvoiceStatus.Overdue;

    public static bool CanBeCancelled(this InvoiceStatus status) =>
        status is InvoiceStatus.Draft or InvoiceStatus.Validated or InvoiceStatus.Signed;

    public static bool IsFinalized(this InvoiceStatus status) =>
        status is InvoiceStatus.Paid or InvoiceStatus.Cancelled or InvoiceStatus.Archived;

    /// <summary>
    /// True when an invoice counts as realized revenue (chiffre d'affaires) : Payée ou Validée.
    /// Source de vérité backend, miroir de REALIZED_REVENUE_STATUSES côté frontend
    /// (invoice-metrics.util.ts). Note : pour une requête EF Core, inliner la condition équivalente
    /// (Status == Paid || Status == Validated) car une méthode d'extension ne se traduit pas en SQL.
    /// </summary>
    public static bool IsRealizedRevenue(this InvoiceStatus status) =>
        status is InvoiceStatus.Paid or InvoiceStatus.Validated;

    public static string ToDisplayString(this InvoiceStatus status) => status switch
    {
        InvoiceStatus.Draft => "Brouillon",
        InvoiceStatus.Validated => "Validée",
        InvoiceStatus.Signed => "Signée",
        InvoiceStatus.Paid => "Payée",
        InvoiceStatus.PartiallyPaid => "Partiellement payée",
        InvoiceStatus.Overdue => "En retard",
        InvoiceStatus.Cancelled => "Annulée",
        InvoiceStatus.Archived => "Archivée",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    public static string ToCssClass(this InvoiceStatus status) => status switch
    {
        InvoiceStatus.Draft => "status-draft",
        InvoiceStatus.Validated => "status-validated",
        InvoiceStatus.Signed => "status-signed",
        InvoiceStatus.Paid => "status-paid",
        InvoiceStatus.PartiallyPaid => "status-partial",
        InvoiceStatus.Overdue => "status-overdue",
        InvoiceStatus.Cancelled => "status-cancelled",
        InvoiceStatus.Archived => "status-archived",
        _ => "status-unknown"
    };
}
