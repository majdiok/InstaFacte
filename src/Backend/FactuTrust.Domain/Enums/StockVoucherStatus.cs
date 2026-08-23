namespace FactuTrust.Domain.Enums;

/// <summary>
/// Lifecycle of a generic stock voucher. Stock moves only on validation.
/// </summary>
public enum StockVoucherStatus
{
    Draft = 0,
    Validated = 1,
    Cancelled = 2
}

public static class StockVoucherStatusExtensions
{
    public static bool CanBeEdited(this StockVoucherStatus status) =>
        status == StockVoucherStatus.Draft;

    public static bool CanBeValidated(this StockVoucherStatus status) =>
        status == StockVoucherStatus.Draft;

    public static bool CanBeCancelled(this StockVoucherStatus status) =>
        status is StockVoucherStatus.Draft or StockVoucherStatus.Validated;

    public static bool CanBeDeleted(this StockVoucherStatus status) =>
        status == StockVoucherStatus.Draft;

    public static string ToDisplayString(this StockVoucherStatus status) => status switch
    {
        StockVoucherStatus.Draft => "Brouillon",
        StockVoucherStatus.Validated => "Validé",
        StockVoucherStatus.Cancelled => "Annulé",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    public static string ToCssClass(this StockVoucherStatus status) => status switch
    {
        StockVoucherStatus.Draft => "status-draft",
        StockVoucherStatus.Validated => "status-validated",
        StockVoucherStatus.Cancelled => "status-cancelled",
        _ => "status-unknown"
    };
}
