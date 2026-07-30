using FactuTrust.Domain.Common;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities.Pricing;

/// <summary>
/// Prix HT d'un produit dans une grille tarifaire, éventuellement dégressif par paliers.
///
/// <see cref="UnitPriceHT"/> est le prix de base : il s'applique tant qu'aucun palier n'est
/// atteint. Les paliers (<see cref="Tiers"/>) ne couvrent que le haut de l'échelle, si bien
/// qu'un article sans palier se comporte exactement comme avant la tranche 5B.
/// </summary>
public sealed class PriceListItem : Entity
{
    public Guid PriceListId { get; private set; }
    public Guid ProductId { get; private set; }

    /// <summary>Prix de base, appliqué en deçà du premier palier.</summary>
    public Money UnitPriceHT { get; private set; } = null!;

    private readonly List<PriceListItemTier> _tiers = new();
    public IReadOnlyCollection<PriceListItemTier> Tiers => _tiers.AsReadOnly();

    private PriceListItem() { }

    internal PriceListItem(Guid priceListId, Guid productId, Money unitPriceHT)
    {
        PriceListId = priceListId;
        ProductId = productId;
        UnitPriceHT = unitPriceHT;
    }

    internal void SetUnitPrice(Money unitPriceHT) => UnitPriceHT = unitPriceHT;

    /// <summary>
    /// Prix applicable pour cette quantité : le palier le plus élevé qu'elle atteint, à défaut
    /// le prix de base. Les paliers plus bas restent en place — c'est bien le plus avantageux
    /// atteint qui gagne, et non le premier trouvé.
    /// </summary>
    public Money ResolveUnitPrice(decimal quantity) =>
        _tiers
            .Where(t => quantity >= t.MinQuantity)
            .OrderByDescending(t => t.MinQuantity)
            .FirstOrDefault()
            ?.UnitPriceHT
        ?? UnitPriceHT;

    /// <summary>Ajoute ou remplace un palier. La devise doit être celle du prix de base.</summary>
    internal Result SetTier(decimal minQuantity, Money unitPriceHT)
    {
        if (minQuantity <= 1)
        {
            return Result.Failure(Error.Validation("MinQuantity",
                "Un palier commence à une quantité supérieure à 1 ; en deçà, c'est le prix de base qui s'applique"));
        }

        if (unitPriceHT is null || unitPriceHT.Amount < 0)
            return Result.Failure(Error.Validation("UnitPriceHT", "Le prix du palier ne peut pas être négatif"));

        if (!string.Equals(unitPriceHT.Currency, UnitPriceHT.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(Error.Validation("UnitPriceHT",
                $"La devise du palier ({unitPriceHT.Currency}) doit être celle du prix de base ({UnitPriceHT.Currency})"));
        }

        var existing = _tiers.FirstOrDefault(t => t.MinQuantity == minQuantity);
        if (existing is null)
            _tiers.Add(new PriceListItemTier(Id, minQuantity, unitPriceHT));
        else
            existing.SetUnitPrice(unitPriceHT);

        return Result.Success();
    }

    internal void RemoveTier(decimal minQuantity)
    {
        var existing = _tiers.FirstOrDefault(t => t.MinQuantity == minQuantity);
        if (existing is not null)
            _tiers.Remove(existing);
    }
}
