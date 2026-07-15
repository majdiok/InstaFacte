namespace FactuTrust.Domain.Enums;

/// <summary>
/// Represents the lifecycle status of a stock transfer between warehouses.
/// </summary>
public enum StockTransferStatus
{
    Draft = 0,
    Confirmed = 1,
    InTransit = 2,
    Completed = 3,
    Cancelled = 4
}

public static class StockTransferStatusExtensions
{
    public static bool CanBeEdited(this StockTransferStatus status) =>
        status == StockTransferStatus.Draft;

    public static bool CanBeConfirmed(this StockTransferStatus status) =>
        status == StockTransferStatus.Draft;

    public static bool CanStartTransit(this StockTransferStatus status) =>
        status == StockTransferStatus.Confirmed;

    public static bool CanBeCompleted(this StockTransferStatus status) =>
        status is StockTransferStatus.Confirmed or StockTransferStatus.InTransit;

    public static bool CanBeCancelled(this StockTransferStatus status) =>
        status is StockTransferStatus.Draft or StockTransferStatus.Confirmed;

    public static string ToDisplayString(this StockTransferStatus status) => status switch
    {
        StockTransferStatus.Draft => "Brouillon",
        StockTransferStatus.Confirmed => "Confirmé",
        StockTransferStatus.InTransit => "En transit",
        StockTransferStatus.Completed => "Terminé",
        StockTransferStatus.Cancelled => "Annulé",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    public static string ToCssClass(this StockTransferStatus status) => status switch
    {
        StockTransferStatus.Draft => "status-draft",
        StockTransferStatus.Confirmed => "status-confirmed",
        StockTransferStatus.InTransit => "status-transit",
        StockTransferStatus.Completed => "status-completed",
        StockTransferStatus.Cancelled => "status-cancelled",
        _ => "status-unknown"
    };
}
