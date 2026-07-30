using FactuTrust.Domain.Common;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities.Pricing;

/// <summary>
/// Palier quantitatif d'un prix de grille : « à partir de <see cref="MinQuantity"/>, le prix
/// unitaire devient <see cref="UnitPriceHT"/> ».
///
/// Les paliers ne couvrent que le haut de l'échelle : le prix de base porté par
/// <see cref="PriceListItem.UnitPriceHT"/> s'applique en deçà du premier palier. Un article
/// sans palier se comporte donc exactement comme avant la tranche 5B.
/// </summary>
public sealed class PriceListItemTier : Entity
{
    public Guid PriceListItemId { get; private set; }

    /// <summary>Quantité à partir de laquelle ce palier s'applique (bornes incluses).</summary>
    public decimal MinQuantity { get; private set; }

    public Money UnitPriceHT { get; private set; } = null!;

    private PriceListItemTier() { }

    internal PriceListItemTier(Guid priceListItemId, decimal minQuantity, Money unitPriceHT)
    {
        PriceListItemId = priceListItemId;
        MinQuantity = minQuantity;
        UnitPriceHT = unitPriceHT;
    }

    internal void SetUnitPrice(Money unitPriceHT) => UnitPriceHT = unitPriceHT;
}
