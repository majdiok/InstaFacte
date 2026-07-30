using FactuTrust.Domain.Common;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities.Pricing;

/// <summary>
/// Prix HT d'un produit dans une grille tarifaire.
///
/// Les paliers quantitatifs (1-9 / 10-49 / 50+) relèvent de la tranche 5B : cet objet ne porte
/// pour l'instant qu'un prix unique par produit. Il est volontairement dépourvu de logique
/// autonome — c'est la grille (<see cref="PriceList"/>) qui l'orchestre.
/// </summary>
public sealed class PriceListItem : Entity
{
    public Guid PriceListId { get; private set; }
    public Guid ProductId { get; private set; }
    public Money UnitPriceHT { get; private set; } = null!;

    private PriceListItem() { }

    internal PriceListItem(Guid priceListId, Guid productId, Money unitPriceHT)
    {
        PriceListId = priceListId;
        ProductId = productId;
        UnitPriceHT = unitPriceHT;
    }

    internal void SetUnitPrice(Money unitPriceHT) => UnitPriceHT = unitPriceHT;
}
