namespace FactuTrust.Domain.Enums;

/// <summary>Lifecycle status of a firm honoraires quote (devis).</summary>
public enum HonorairesQuoteStatus
{
    Draft = 0,
    Sent = 1,
    Accepted = 2,
    Rejected = 3,
    Expired = 4,
    Converted = 5,
    Cancelled = 6
}

public static class HonorairesQuoteStatusExtensions
{
    public static bool CanBeEdited(this HonorairesQuoteStatus status) =>
        status == HonorairesQuoteStatus.Draft;

    public static bool CanBeSent(this HonorairesQuoteStatus status) =>
        status == HonorairesQuoteStatus.Draft;

    public static bool CanBeAccepted(this HonorairesQuoteStatus status) =>
        status == HonorairesQuoteStatus.Sent;

    public static bool CanBeRejected(this HonorairesQuoteStatus status) =>
        status == HonorairesQuoteStatus.Sent;

    public static bool CanBeConverted(this HonorairesQuoteStatus status) =>
        status == HonorairesQuoteStatus.Accepted;

    public static bool CanBeCancelled(this HonorairesQuoteStatus status) =>
        status is HonorairesQuoteStatus.Draft or HonorairesQuoteStatus.Sent;

    public static string ToDisplayString(this HonorairesQuoteStatus status) => status switch
    {
        HonorairesQuoteStatus.Draft => "Brouillon",
        HonorairesQuoteStatus.Sent => "Envoyé",
        HonorairesQuoteStatus.Accepted => "Accepté",
        HonorairesQuoteStatus.Rejected => "Refusé",
        HonorairesQuoteStatus.Expired => "Expiré",
        HonorairesQuoteStatus.Converted => "Facturé",
        HonorairesQuoteStatus.Cancelled => "Annulé",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };
}
