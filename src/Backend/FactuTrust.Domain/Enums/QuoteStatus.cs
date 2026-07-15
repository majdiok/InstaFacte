namespace FactuTrust.Domain.Enums;

/// <summary>
/// Represents the lifecycle status of a quote (devis).
/// </summary>
public enum QuoteStatus
{
    /// <summary>
    /// Draft quote - can be edited or deleted.
    /// </summary>
    Draft = 0,

    /// <summary>
    /// Sent quote - has been sent to the client.
    /// </summary>
    Sent = 1,

    /// <summary>
    /// Accepted quote - client has accepted the quote.
    /// </summary>
    Accepted = 2,

    /// <summary>
    /// Rejected quote - client has rejected the quote.
    /// </summary>
    Rejected = 3,

    /// <summary>
    /// Expired quote - validity period has expired.
    /// </summary>
    Expired = 4,

    /// <summary>
    /// Converted quote - has been converted to an invoice.
    /// </summary>
    Converted = 5,

    /// <summary>
    /// Cancelled quote - voided by the user.
    /// </summary>
    Cancelled = 6
}

public static class QuoteStatusExtensions
{
    public static bool CanBeEdited(this QuoteStatus status) => 
        status == QuoteStatus.Draft;

    public static bool CanBeSent(this QuoteStatus status) => 
        status == QuoteStatus.Draft;

    public static bool CanBeAccepted(this QuoteStatus status) => 
        status == QuoteStatus.Sent;

    public static bool CanBeRejected(this QuoteStatus status) => 
        status == QuoteStatus.Sent;

    public static bool CanBeConverted(this QuoteStatus status) => 
        status == QuoteStatus.Accepted;

    public static bool CanBeCancelled(this QuoteStatus status) => 
        status is QuoteStatus.Draft or QuoteStatus.Sent;

    public static bool IsFinalized(this QuoteStatus status) =>
        status is QuoteStatus.Converted or QuoteStatus.Cancelled or QuoteStatus.Rejected;

    /// <summary>Libellé métier pour l'UI (Brouillon / Envoyé / Accepté / Refusé / Facturé…).</summary>
    public static string ToDisplayString(this QuoteStatus status) => status switch
    {
        QuoteStatus.Draft => "Brouillon",
        QuoteStatus.Sent => "Envoyé",
        QuoteStatus.Accepted => "Accepté",
        QuoteStatus.Rejected => "Refusé",
        QuoteStatus.Expired => "Expiré",
        QuoteStatus.Converted => "Facturé",
        QuoteStatus.Cancelled => "Annulé",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    public static string ToCssClass(this QuoteStatus status) => status switch
    {
        QuoteStatus.Draft => "status-draft",
        QuoteStatus.Sent => "status-sent",
        QuoteStatus.Accepted => "status-accepted",
        QuoteStatus.Rejected => "status-rejected",
        QuoteStatus.Expired => "status-expired",
        QuoteStatus.Converted => "status-converted",
        QuoteStatus.Cancelled => "status-cancelled",
        _ => "status-unknown"
    };
}
