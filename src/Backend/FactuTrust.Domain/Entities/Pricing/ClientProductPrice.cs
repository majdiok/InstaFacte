using FactuTrust.Domain.Common;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities.Pricing;

/// <summary>
/// Prix négocié pour un couple client / produit — le cas particulier qui prime sur toute grille.
///
/// Peut être borné dans le temps (accord ponctuel) et désactivé sans suppression. Comme pour les
/// grilles, un document déjà émis ne dépend jamais de la survie de cet enregistrement : le prix
/// est figé à la ligne au moment de la résolution.
/// </summary>
public sealed class ClientProductPrice : AggregateRoot
{
    public Guid ClientId { get; private set; }
    public Guid ProductId { get; private set; }
    public Money UnitPriceHT { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public DateTime? ValidFrom { get; private set; }
    public DateTime? ValidUntil { get; private set; }

    private ClientProductPrice() { }

    public static Result<ClientProductPrice> Create(
        Guid clientId,
        Guid productId,
        Money unitPriceHT,
        DateTime? validFrom = null,
        DateTime? validUntil = null)
    {
        if (clientId == Guid.Empty)
            return Result.Failure<ClientProductPrice>(Error.Validation("ClientId", "Le client est obligatoire"));

        if (productId == Guid.Empty)
            return Result.Failure<ClientProductPrice>(Error.Validation("ProductId", "Le produit est obligatoire"));

        if (unitPriceHT is null || unitPriceHT.Amount < 0)
            return Result.Failure<ClientProductPrice>(Error.Validation("UnitPriceHT", "Le prix négocié ne peut pas être négatif"));

        if (validFrom.HasValue && validUntil.HasValue && validUntil.Value.Date < validFrom.Value.Date)
            return Result.Failure<ClientProductPrice>(Error.Validation("ValidUntil", "La fin de validité ne peut pas précéder le début"));

        return Result.Success(new ClientProductPrice
        {
            ClientId = clientId,
            ProductId = productId,
            UnitPriceHT = unitPriceHT,
            IsActive = true,
            ValidFrom = validFrom?.Date,
            ValidUntil = validUntil?.Date
        });
    }

    public Result UpdatePrice(Money unitPriceHT)
    {
        if (unitPriceHT is null || unitPriceHT.Amount < 0)
            return Result.Failure(Error.Validation("UnitPriceHT", "Le prix négocié ne peut pas être négatif"));

        UnitPriceHT = unitPriceHT;
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

    /// <summary>Vrai si ce prix négocié s'applique à la date donnée (actif et dans sa validité).</summary>
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
}
