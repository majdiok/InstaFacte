namespace FactuTrust.Domain.Enums;

/// <summary>
/// Direction of a generic stock voucher (bon d'entrée / bon de sortie).
/// </summary>
public enum StockVoucherKind
{
    Entry = 1,
    Issue = 2
}

public static class StockVoucherKindExtensions
{
    public static string ToDisplayString(this StockVoucherKind kind) => kind switch
    {
        StockVoucherKind.Entry => "Bon d'entrée",
        StockVoucherKind.Issue => "Bon de sortie",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public static string DefaultPrefix(this StockVoucherKind kind) => kind switch
    {
        StockVoucherKind.Entry => "BE",
        StockVoucherKind.Issue => "BS",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public static NumberingDocumentType ToNumberingDocumentType(this StockVoucherKind kind) => kind switch
    {
        StockVoucherKind.Entry => NumberingDocumentType.StockEntry,
        StockVoucherKind.Issue => NumberingDocumentType.StockIssue,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public static MovementType ToMovementType(this StockVoucherKind kind) => kind switch
    {
        StockVoucherKind.Entry => MovementType.Entry,
        StockVoucherKind.Issue => MovementType.Exit,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public static bool IsAllowedReason(this StockVoucherKind kind, MovementReason reason) =>
        kind == StockVoucherKind.Entry
            ? reason is MovementReason.InitialStock
                or MovementReason.CustomerReturn
                or MovementReason.Purchase
                or MovementReason.FoundOrOther
            : reason is MovementReason.Damage
                or MovementReason.SupplierReturn
                or MovementReason.InternalUse
                or MovementReason.GiftOrSample;

    public static string ToVoucherReasonDisplay(this StockVoucherKind kind, MovementReason reason) =>
        kind == StockVoucherKind.Entry && reason == MovementReason.Purchase
            ? "Achat hors réception"
            : reason.ToDisplayString();
}
