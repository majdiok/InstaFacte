namespace FactuTrust.Domain.Enums;

/// <summary>
/// Cycle de vie d'une commande client.
///
/// Deux avancements coexistent et ne progressent pas au même rythme : ce qui est LIVRÉ et ce
/// qui est FACTURÉ. Une commande peut être intégralement livrée mais partiellement facturée
/// (facturation périodique), ou l'inverse (facturation sur commande, livraison échelonnée).
/// Le statut porte l'état d'avancement le moins avancé des deux, pour que « Terminée » ne
/// puisse jamais masquer un reliquat.
/// </summary>
public enum SalesOrderStatus
{
    /// <summary>Brouillon : seul état où les lignes sont modifiables.</summary>
    Draft = 0,

    /// <summary>Confirmée par le client : engagement ferme, stock réservé.</summary>
    Confirmed = 1,

    /// <summary>Une partie des quantités commandées a été livrée.</summary>
    PartiallyDelivered = 2,

    /// <summary>Tout est livré, mais tout n'est pas encore facturé.</summary>
    Delivered = 3,

    /// <summary>Tout est livré et tout est facturé : la commande est soldée.</summary>
    Completed = 4,

    /// <summary>Annulée avant tout mouvement. Les réservations de stock sont libérées.</summary>
    Cancelled = 5,

    /// <summary>
    /// Soldée manuellement alors qu'il restait des quantités à livrer : le client renonce au
    /// reliquat. Distinct de Cancelled, qui suppose qu'aucun mouvement n'a eu lieu.
    /// </summary>
    Closed = 6
}

public static class SalesOrderStatusExtensions
{
    /// <summary>Seul un brouillon peut voir ses lignes modifiées.</summary>
    public static bool CanBeEdited(this SalesOrderStatus status) =>
        status == SalesOrderStatus.Draft;

    public static bool CanBeConfirmed(this SalesOrderStatus status) =>
        status == SalesOrderStatus.Draft;

    /// <summary>Une commande confirmée ou partiellement livrée peut recevoir des livraisons.</summary>
    public static bool CanBeDelivered(this SalesOrderStatus status) =>
        status is SalesOrderStatus.Confirmed or SalesOrderStatus.PartiallyDelivered;

    /// <summary>
    /// On facture ce qui est livré. Une commande confirmée peut être facturée d'avance
    /// (facture proforma convertie), d'où l'inclusion de Confirmed.
    /// </summary>
    public static bool CanBeInvoiced(this SalesOrderStatus status) =>
        status is SalesOrderStatus.Confirmed
            or SalesOrderStatus.PartiallyDelivered
            or SalesOrderStatus.Delivered;

    /// <summary>
    /// L'annulation pure n'est possible que tant qu'aucun mouvement n'a eu lieu. Au-delà,
    /// c'est une clôture avec reliquat abandonné (voir <see cref="CanBeClosed"/>).
    /// </summary>
    public static bool CanBeCancelled(this SalesOrderStatus status) =>
        status is SalesOrderStatus.Draft or SalesOrderStatus.Confirmed;

    /// <summary>Solder une commande entamée en abandonnant le reste à livrer.</summary>
    public static bool CanBeClosed(this SalesOrderStatus status) =>
        status is SalesOrderStatus.Confirmed
            or SalesOrderStatus.PartiallyDelivered
            or SalesOrderStatus.Delivered;

    /// <summary>États terminaux : plus aucun mouvement attendu.</summary>
    public static bool IsFinalized(this SalesOrderStatus status) =>
        status is SalesOrderStatus.Completed
            or SalesOrderStatus.Cancelled
            or SalesOrderStatus.Closed;

    /// <summary>Vrai tant que la commande pèse sur le carnet de commandes.</summary>
    public static bool IsOpen(this SalesOrderStatus status) =>
        !status.IsFinalized();

    /// <summary>Vrai si le stock doit être réservé dans cet état.</summary>
    public static bool HoldsStockReservation(this SalesOrderStatus status) =>
        status is SalesOrderStatus.Confirmed
            or SalesOrderStatus.PartiallyDelivered;

    public static string ToDisplayString(this SalesOrderStatus status) => status switch
    {
        SalesOrderStatus.Draft => "Brouillon",
        SalesOrderStatus.Confirmed => "Confirmée",
        SalesOrderStatus.PartiallyDelivered => "Partiellement livrée",
        SalesOrderStatus.Delivered => "Livrée",
        SalesOrderStatus.Completed => "Soldée",
        SalesOrderStatus.Cancelled => "Annulée",
        SalesOrderStatus.Closed => "Clôturée (reliquat abandonné)",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    public static string ToCssClass(this SalesOrderStatus status) => status switch
    {
        SalesOrderStatus.Draft => "status-draft",
        SalesOrderStatus.Confirmed => "status-confirmed",
        SalesOrderStatus.PartiallyDelivered => "status-partial",
        SalesOrderStatus.Delivered => "status-received",
        SalesOrderStatus.Completed => "status-invoiced",
        SalesOrderStatus.Cancelled => "status-cancelled",
        SalesOrderStatus.Closed => "status-cancelled",
        _ => "status-unknown"
    };
}
