using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities.Pricing;

/// <summary>
/// Promotion datée : une remise temporaire visant un produit, une catégorie, ou un client.
///
/// <b>Ce qu'une promotion N'EST PAS.</b> Elle ne remplace pas le prix — c'est le rôle des
/// grilles. Elle s'applique <b>après</b> la résolution de prix, comme une remise de ligne, et
/// n'existe donc jamais dans le prix figé : un document émis pendant une promotion garde son
/// prix, et la fin de la promotion ne le change pas.
///
/// <b>Portée.</b> Une promotion cible soit un produit, soit une catégorie, soit un client, soit
/// une combinaison (produit ET client = accord ponctuel daté). Les cibles nulles valent « tous ».
/// </summary>
public sealed class Promotion : AggregateRoot
{
    public string Name { get; private set; } = null!;

    /// <summary>Produit visé, ou <c>null</c> pour tous les produits.</summary>
    public Guid? ProductId { get; private set; }

    /// <summary>Catégorie visée, ou <c>null</c>. Ignorée si un produit est visé.</summary>
    public Guid? ProductCategoryId { get; private set; }

    /// <summary>Client visé, ou <c>null</c> pour tous les clients.</summary>
    public Guid? ClientId { get; private set; }

    public PromotionDiscountType DiscountType { get; private set; }

    /// <summary>Taux de remise, si <see cref="DiscountType"/> vaut <c>Percentage</c>.</summary>
    public decimal? DiscountPercent { get; private set; }

    /// <summary>Remise en valeur par unité, si <see cref="DiscountType"/> vaut <c>Amount</c>.</summary>
    public Money? DiscountAmount { get; private set; }

    /// <summary>Quantité minimale pour déclencher la promotion (1 = aucune condition).</summary>
    public decimal MinQuantity { get; private set; } = 1m;

    public DateTime StartsOn { get; private set; }
    public DateTime EndsOn { get; private set; }
    public bool IsActive { get; private set; }

    /// <summary>
    /// Priorité en cas de promotions concurrentes : la plus élevée gagne. À égalité, c'est la
    /// plus spécifique (produit &gt; catégorie &gt; toutes) qui l'emporte — départager au hasard
    /// donnerait des remises différentes d'une saisie à l'autre.
    /// </summary>
    public int Priority { get; private set; }

    private Promotion() { }

    public static Result<Promotion> Create(
        string? name,
        DateTime startsOn,
        DateTime endsOn,
        PromotionDiscountType discountType,
        decimal? discountPercent,
        Money? discountAmount,
        Guid? productId = null,
        Guid? productCategoryId = null,
        Guid? clientId = null,
        decimal minQuantity = 1m,
        int priority = 0)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<Promotion>(Error.Validation("Name", "Le nom de la promotion est obligatoire"));

        if (name.Trim().Length > 100)
            return Result.Failure<Promotion>(Error.Validation("Name", "Le nom ne peut pas dépasser 100 caractères"));

        if (endsOn.Date < startsOn.Date)
            return Result.Failure<Promotion>(Error.Validation("EndsOn", "La fin de la promotion ne peut pas précéder son début"));

        if (minQuantity <= 0)
            return Result.Failure<Promotion>(Error.Validation("MinQuantity", "La quantité minimale doit être supérieure à zéro"));

        var validation = ValidateDiscount(discountType, discountPercent, discountAmount);
        if (validation.IsFailure)
            return Result.Failure<Promotion>(validation.Error);

        return Result.Success(new Promotion
        {
            Name = name.Trim(),
            StartsOn = startsOn.Date,
            EndsOn = endsOn.Date,
            DiscountType = discountType,
            DiscountPercent = discountType == PromotionDiscountType.Percentage ? discountPercent : null,
            DiscountAmount = discountType == PromotionDiscountType.Amount ? discountAmount : null,
            ProductId = productId,
            ProductCategoryId = productId.HasValue ? null : productCategoryId,
            ClientId = clientId,
            MinQuantity = minQuantity,
            Priority = priority,
            IsActive = true
        });
    }

    private static Result ValidateDiscount(
        PromotionDiscountType type, decimal? percent, Money? amount)
    {
        if (type == PromotionDiscountType.Percentage)
        {
            if (percent is null or <= 0 or > 100)
                return Result.Failure(Error.Validation("DiscountPercent", "Le taux doit être compris entre 0 % et 100 %"));
        }
        else
        {
            if (amount is null || amount.Amount <= 0)
                return Result.Failure(Error.Validation("DiscountAmount", "Le montant de remise doit être supérieur à zéro"));
        }

        return Result.Success();
    }

    public Result Update(
        string? name,
        DateTime startsOn,
        DateTime endsOn,
        PromotionDiscountType discountType,
        decimal? discountPercent,
        Money? discountAmount,
        decimal minQuantity,
        int priority)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure(Error.Validation("Name", "Le nom de la promotion est obligatoire"));

        if (endsOn.Date < startsOn.Date)
            return Result.Failure(Error.Validation("EndsOn", "La fin de la promotion ne peut pas précéder son début"));

        if (minQuantity <= 0)
            return Result.Failure(Error.Validation("MinQuantity", "La quantité minimale doit être supérieure à zéro"));

        var validation = ValidateDiscount(discountType, discountPercent, discountAmount);
        if (validation.IsFailure)
            return validation;

        Name = name.Trim();
        StartsOn = startsOn.Date;
        EndsOn = endsOn.Date;
        DiscountType = discountType;
        DiscountPercent = discountType == PromotionDiscountType.Percentage ? discountPercent : null;
        DiscountAmount = discountType == PromotionDiscountType.Amount ? discountAmount : null;
        MinQuantity = minQuantity;
        Priority = priority;

        return Result.Success();
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    /// <summary>Vrai si la promotion court à cette date (active et dans sa fenêtre).</summary>
    public bool IsRunningAt(DateTime date)
    {
        if (!IsActive) return false;

        var d = date.Date;
        return d >= StartsOn && d <= EndsOn;
    }

    /// <summary>
    /// Vrai si la promotion vise ce produit / cette catégorie / ce client à cette quantité.
    /// Une cible nulle vaut « tous » ; la date n'est pas testée ici (voir <see cref="IsRunningAt"/>).
    /// </summary>
    public bool Matches(Guid productId, Guid? categoryId, Guid? clientId, decimal quantity)
    {
        if (quantity < MinQuantity) return false;

        if (ProductId.HasValue && ProductId.Value != productId) return false;

        if (!ProductId.HasValue && ProductCategoryId.HasValue
            && (categoryId is null || ProductCategoryId.Value != categoryId.Value))
        {
            return false;
        }

        if (ClientId.HasValue && (clientId is null || ClientId.Value != clientId.Value)) return false;

        return true;
    }

    /// <summary>
    /// Spécificité de la promotion, pour départager deux promotions de même priorité :
    /// produit + client (3) &gt; produit ou client seul (2) &gt; catégorie (1) &gt; tous (0).
    /// </summary>
    public int Specificity =>
        (ProductId.HasValue ? 2 : ProductCategoryId.HasValue ? 1 : 0) + (ClientId.HasValue ? 1 : 0);

    /// <summary>
    /// Remise en valeur pour une ligne, à partir du prix unitaire résolu et de la quantité.
    /// Ne dépasse jamais le montant de la ligne : une remise ne rend pas d'argent.
    /// </summary>
    public Money ComputeDiscount(Money unitPrice, decimal quantity)
    {
        var lineTotal = unitPrice.Multiply(quantity);

        var raw = DiscountType == PromotionDiscountType.Percentage
            ? lineTotal.ApplyPercentage(DiscountPercent ?? 0m)
            : (DiscountAmount ?? Money.Zero(unitPrice.Currency)).Multiply(quantity);

        return raw.Amount > lineTotal.Amount ? lineTotal : raw;
    }
}
