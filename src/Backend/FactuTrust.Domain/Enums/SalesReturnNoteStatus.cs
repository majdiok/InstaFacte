namespace FactuTrust.Domain.Enums;

/// <summary>
/// Lifecycle of a sales return note (bon de retour client, pré-facture).
/// </summary>
public enum SalesReturnNoteStatus
{
    /// <summary>Draft — editable, no stock or BL quantity impact.</summary>
    Draft = 0,

    /// <summary>Confirmed — stock restored, BL ReturnedQuantity incremented, immutable.</summary>
    Confirmed = 1
}

public static class SalesReturnNoteStatusExtensions
{
    public static bool CanBeEdited(this SalesReturnNoteStatus status) =>
        status == SalesReturnNoteStatus.Draft;

    public static bool CanBeConfirmed(this SalesReturnNoteStatus status) =>
        status == SalesReturnNoteStatus.Draft;

    public static bool CanBeDeleted(this SalesReturnNoteStatus status) =>
        status == SalesReturnNoteStatus.Draft;

    public static string ToDisplayString(this SalesReturnNoteStatus status) => status switch
    {
        SalesReturnNoteStatus.Draft => "Brouillon",
        SalesReturnNoteStatus.Confirmed => "Confirmé",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    public static string ToCssClass(this SalesReturnNoteStatus status) => status switch
    {
        SalesReturnNoteStatus.Draft => "status-draft",
        SalesReturnNoteStatus.Confirmed => "status-confirmed",
        _ => "status-unknown"
    };
}
