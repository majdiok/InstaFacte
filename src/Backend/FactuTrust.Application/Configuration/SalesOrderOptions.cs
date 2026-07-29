namespace FactuTrust.Application.Configuration;

/// <summary>
/// Options du module Commandes clients, liées à la section <c>Features:SalesOrders</c>.
/// </summary>
public sealed class SalesOrderOptions
{
    public const string SectionName = "Features:SalesOrders";

    /// <summary>
    /// Réservation physique du stock à la confirmation d'une commande.
    ///
    /// <b>Désactivée par défaut, et ce n'est pas un oubli.</b> L'activer fait passer
    /// <c>StockItem.QuantityReserved</c> au-dessus de zéro pour la première fois depuis
    /// l'origine du produit, ce qui change la valeur lue par huit sites — dont les transferts
    /// inter-dépôts, le contrôle de disponibilité et la déduction partielle sur facture, qui
    /// deviennent tous plus stricts.
    ///
    /// À n'activer, tenant par tenant, qu'après avoir vérifié que ces trois flux se comportent
    /// comme attendu sous réservation.
    /// </summary>
    public bool StockReservationEnabled { get; set; }
}
