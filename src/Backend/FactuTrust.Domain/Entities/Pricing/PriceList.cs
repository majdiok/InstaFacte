using FactuTrust.Domain.Common;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities.Pricing;

/// <summary>
/// Grille tarifaire : un jeu de prix HT par produit, affectable à des clients.
///
/// Une grille peut être bornée dans le temps (promotion saisonnière, tarif d'une campagne) et
/// désactivée sans être supprimée — l'historique des documents déjà émis ne doit jamais dépendre
/// de la survie d'une grille. Le résolveur de prix n'y puise que si elle est <b>applicable</b>
/// (active et dans sa période de validité) à la date du document.
/// </summary>
public sealed class PriceList : AggregateRoot
{
    public string Name { get; private set; } = null!;
    public string Currency { get; private set; } = Money.DefaultCurrency;
    public bool IsActive { get; private set; }
    public DateTime? ValidFrom { get; private set; }
    public DateTime? ValidUntil { get; private set; }

    private readonly List<PriceListItem> _items = new();
    public IReadOnlyCollection<PriceListItem> Items => _items.AsReadOnly();

    private PriceList() { }

    public static Result<PriceList> Create(
        string? name,
        string currency = Money.DefaultCurrency,
        DateTime? validFrom = null,
        DateTime? validUntil = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<PriceList>(Error.Validation("Name", "Le nom de la grille est obligatoire"));

        if (name.Trim().Length > 100)
            return Result.Failure<PriceList>(Error.Validation("Name", "Le nom de la grille ne peut pas dépasser 100 caractères"));

        if (validFrom.HasValue && validUntil.HasValue && validUntil.Value.Date < validFrom.Value.Date)
            return Result.Failure<PriceList>(Error.Validation("ValidUntil", "La fin de validité ne peut pas précéder le début"));

        return Result.Success(new PriceList
        {
            Name = name.Trim(),
            Currency = string.IsNullOrWhiteSpace(currency) ? Money.DefaultCurrency : currency.Trim().ToUpperInvariant(),
            IsActive = true,
            ValidFrom = validFrom?.Date,
            ValidUntil = validUntil?.Date
        });
    }

    public Result Rename(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure(Error.Validation("Name", "Le nom de la grille est obligatoire"));
        if (name.Trim().Length > 100)
            return Result.Failure(Error.Validation("Name", "Le nom de la grille ne peut pas dépasser 100 caractères"));

        Name = name.Trim();
        return Result.Success();
    }

    public Result SetValidity(DateTime? validFrom, DateTime? validUntil)
    {
        if (validFrom.HasValue && validUntil.HasValue && validUntil.Value.Date < validFrom.Value.Date)
            return Result.Failure(Error.Validation("ValidUntil", "La fin de validité ne peut pas précéder le début"));

        ValidFrom = validFrom?.Date;
        ValidUntil = validUntil?.Date;
        return Result.Success();
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    /// <summary>
    /// Ajoute ou met à jour le prix d'un produit. La devise du prix doit être celle de la grille.
    /// </summary>
    public Result SetPrice(Guid productId, Money unitPriceHT)
    {
        if (unitPriceHT is null)
            return Result.Failure(Error.Validation("UnitPriceHT", "Le prix est obligatoire"));

        if (unitPriceHT.Amount < 0)
            return Result.Failure(Error.Validation("UnitPriceHT", "Le prix ne peut pas être négatif"));

        if (!string.Equals(unitPriceHT.Currency, Currency, StringComparison.OrdinalIgnoreCase))
            return Result.Failure(Error.Validation("UnitPriceHT",
                $"La devise du prix ({unitPriceHT.Currency}) doit être celle de la grille ({Currency})"));

        var existing = _items.FirstOrDefault(i => i.ProductId == productId);
        if (existing is null)
            _items.Add(new PriceListItem(Id, productId, unitPriceHT));
        else
            existing.SetUnitPrice(unitPriceHT);

        return Result.Success();
    }

    public void RemovePrice(Guid productId)
    {
        var existing = _items.FirstOrDefault(i => i.ProductId == productId);
        if (existing is not null)
            _items.Remove(existing);
    }

    /// <summary>Vrai si la grille peut fournir un prix à la date donnée (active et dans sa validité).</summary>
    public bool IsApplicableAt(DateTime date)
    {
        if (!IsActive)
            return false;

        var d = date.Date;
        if (ValidFrom.HasValue && d < ValidFrom.Value)
            return false;
        if (ValidUntil.HasValue && d > ValidUntil.Value)
            return false;

        return true;
    }

    /// <summary>
    /// Ajoute ou remplace un palier quantitatif sur un produit déjà tarifé.
    /// </summary>
    public Result SetTier(Guid productId, decimal minQuantity, Money unitPriceHT)
    {
        var item = _items.FirstOrDefault(i => i.ProductId == productId);
        if (item is null)
        {
            return Result.Failure(Error.Validation("ProductId",
                "Fixez d'abord le prix de base du produit dans la grille, puis ses paliers"));
        }

        return item.SetTier(minQuantity, unitPriceHT);
    }

    public void RemoveTier(Guid productId, decimal minQuantity) =>
        _items.FirstOrDefault(i => i.ProductId == productId)?.RemoveTier(minQuantity);

    /// <summary>
    /// Prix du produit dans la grille pour cette quantité, ou <c>null</c> si le produit n'y
    /// figure pas. Sans paliers, la quantité n'a aucun effet.
    /// </summary>
    public Money? TryGetUnitPrice(Guid productId, decimal quantity = 1m) =>
        _items.FirstOrDefault(i => i.ProductId == productId)?.ResolveUnitPrice(quantity);
}
