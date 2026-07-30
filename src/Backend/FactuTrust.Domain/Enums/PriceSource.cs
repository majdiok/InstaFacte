namespace FactuTrust.Domain.Enums;

/// <summary>
/// Origine du prix retenu par le résolveur de prix, dans l'ordre de priorité croissante.
///
/// La résolution répond à « quel prix pour ce client, ce produit, cette date » à un seul
/// endroit ; cette énumération explique <b>pourquoi</b> ce prix, ce qui est indispensable en
/// support comme en contrôle.
/// </summary>
public enum PriceSource
{
    /// <summary>Prix catalogue du produit (<c>Product.UnitPrice</c>) — repli par défaut.</summary>
    Catalog = 0,

    /// <summary>Prix issu de la grille tarifaire affectée au client.</summary>
    PriceList = 1,

    /// <summary>Prix négocié spécifiquement pour ce client et ce produit — prioritaire.</summary>
    ClientPrice = 2
}
