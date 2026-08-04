namespace FactuTrust.Domain.Enums;

/// <summary>Lifecycle status of a firm honoraires invoice / credit note.</summary>
public enum HonorairesInvoiceStatus
{
    Draft = 0,
    Validated = 1,
    Paid = 4,
    PartiallyPaid = 5,
    Cancelled = 7
}

public static class HonorairesInvoiceStatusExtensions
{
    public static bool CanBeEdited(this HonorairesInvoiceStatus status) =>
        status == HonorairesInvoiceStatus.Draft;

    public static bool CanBePaymentRecorded(this HonorairesInvoiceStatus status) =>
        status is HonorairesInvoiceStatus.Validated or HonorairesInvoiceStatus.PartiallyPaid;

    public static bool CanBeCancelled(this HonorairesInvoiceStatus status) =>
        status is HonorairesInvoiceStatus.Draft or HonorairesInvoiceStatus.Validated;

    public static string ToDisplayString(this HonorairesInvoiceStatus status) => status switch
    {
        HonorairesInvoiceStatus.Draft => "Brouillon",
        HonorairesInvoiceStatus.Validated => "Validée",
        HonorairesInvoiceStatus.Paid => "Payée",
        HonorairesInvoiceStatus.PartiallyPaid => "Partiellement payée",
        HonorairesInvoiceStatus.Cancelled => "Annulée",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };
}
