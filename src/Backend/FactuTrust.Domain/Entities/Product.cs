using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Represents a product or service in the catalog.
/// </summary>
public sealed class Product : AggregateRoot
{
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public ProductType Type { get; private set; }
    public Money UnitPrice { get; private set; } = null!;

    /// <summary>
    /// Purchase price (cost) for this product. Used as default in purchase orders and supplier invoices.
    /// When null, UnitPrice (selling price) is used as fallback.
    /// </summary>
    public Money? PurchasePrice { get; private set; }

    public VatRate VatRate { get; private set; }
    public string? Unit { get; private set; }

    /// <summary>
    /// Code-barres EAN-8 / EAN-13 de l'article. <c>null</c> quand l'article n'en a pas.
    ///
    /// Distinct de <see cref="Code"/>, qui est la référence interne : le scan du point de
    /// vente doit interroger CE champ, en correspondance exacte. Auparavant il cherchait dans
    /// le code interne avec repli sur une correspondance approchée, ce qui pouvait encaisser
    /// un autre article.
    /// </summary>
    public Barcode? Barcode { get; private set; }

    /// <summary>
    /// When true, FODEC (1%) applies on this product's HT amount on sales invoices.
    /// </summary>
    public bool IsFodecApplicable { get; private set; }

    /// <summary>
    /// Profit margin (%) relative to <see cref="PurchasePrice"/>. Recalculated on save from catalog prices.
    /// </summary>
    public decimal? ProfitMarginPercent { get; private set; }

    /// <summary>
    /// Last purchase price (HT) from the most recent goods receipt. Updated automatically on PO receipt.
    /// </summary>
    public Money? LastPurchasePrice { get; private set; }

    /// <summary>
    /// When true, line discounts on sales documents are capped by <see cref="MaxDiscountPercent"/>.
    /// </summary>
    public bool IsDiscountEnabled { get; private set; }

    /// <summary>
    /// Maximum allowed discount (%) on sales lines when <see cref="IsDiscountEnabled"/> is true.
    /// </summary>
    public decimal? MaxDiscountPercent { get; private set; }

    public bool IsActive { get; private set; }
    
    /// <summary>
    /// Indicates whether this product has stock management enabled.
    /// When true, stock levels will be tracked and decremented on invoice validation.
    /// </summary>
    public bool IsStockManaged { get; private set; }

    /// <summary>
    /// Product category for classification.
    /// </summary>
    public Guid CategoryId { get; private set; }
    public ProductCategory Category { get; private set; } = null!;

    /// <summary>
    /// URL of the product image. Populated automatically from external image search (Unsplash/Google).
    /// </summary>
    public string? ImageUrl { get; private set; }

    /// <summary>
    /// Opt-in flag to expose this product on the public 3D storefront.
    /// Defaults to <c>false</c>. Switching it on emits an outbox message picked up by the
    /// projection service, which then publishes the product in the Master DB.
    /// </summary>
    public bool IsPubliclyListed { get; private set; }

    // ─────────── Replenishment V2 — supplier & packaging hints (all optional, additive) ───────────
    // These fields enrich the AI Forecasting replenishment module. They are null by default,
    // so legacy data and the V1 replenishment pipeline are unaffected. Only ReplenishmentService
    // (gated by Features:Forecasting:ReplenishmentV2:Enabled) reads them.

    /// <summary>
    /// Preferred supplier used to auto-route replenishment recommendations to a draft PO.
    /// Null = no preference; the buyer must pick a supplier manually.
    /// </summary>
    public Guid? PreferredSupplierId { get; private set; }

    /// <summary>
    /// Minimum order quantity imposed by the (preferred) supplier.
    /// Recommended quantities below this value are bumped up to the MOQ at PO-prep time.
    /// </summary>
    public decimal? MinimumOrderQuantity { get; private set; }

    /// <summary>
    /// Packaging unit label (e.g. "Palette", "Carton") — informational, shown to the buyer.
    /// </summary>
    public string? PackagingUnit { get; private set; }

    /// <summary>
    /// Packaging quantity in base units (e.g. 500 units per palette). When set, recommended
    /// quantities are rounded up to the next multiple to avoid breaking pack constraints.
    /// </summary>
    public decimal? PackagingQty { get; private set; }

    /// <summary>
    /// Per-product override of the supplier lead time in days. Null = fallback to
    /// ForecastingOptions.DefaultLeadTimeDays. Used by ReplenishmentService only.
    /// </summary>
    public int? LeadTimeDaysOverride { get; private set; }

    /// <summary>Parent template when this product is a generated variant SKU.</summary>
    public Guid? ParentProductId { get; private set; }

    /// <summary>True when this product is a variant matrix template (not sellable, not stockable).</summary>
    public bool IsVariantTemplate { get; private set; }

    public TrackingMode TrackingMode { get; private set; }

    public bool HasExpiryTracking { get; private set; }

    public PickingPolicy PickingPolicy { get; private set; }

    public CostingMethod CostingMethod { get; private set; }

    public int? ExpiryAlertDays { get; private set; }

    private Product() { }

    /// <summary>
    /// Toggles public listing. Returns true if the state actually changed.
    /// </summary>
    public bool SetPubliclyListed(bool value)
    {
        if (value && IsVariantTemplate)
            return false;
        if (IsPubliclyListed == value)
            return false;
        IsPubliclyListed = value;
        return true;
    }

    /// <summary>
    /// Sets the product image URL. Used by the image search service when an image is found.
    /// </summary>
    public void SetImageUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || url.Length > 500)
            throw new ArgumentException("L'URL de l'image doit être entre 1 et 500 caractères.", nameof(url));
        ImageUrl = url.Trim();
    }

    /// <summary>
    /// Clears the product image URL.
    /// </summary>
    public void ClearImageUrl()
    {
        ImageUrl = null;
    }

    public static Result<Product> Create(
        string code,
        string name,
        ProductType type,
        Money unitPrice,
        VatRate vatRate,
        Guid categoryId,
        string? description = null,
        string? unit = null,
        bool isStockManaged = false,
        Money? purchasePrice = null,
        bool isFodecApplicable = false,
        decimal? profitMarginPercent = null,
        bool isDiscountEnabled = false,
        decimal? maxDiscountPercent = null)
    {
        if (string.IsNullOrWhiteSpace(code))
            return Result.Failure<Product>(Error.Validation("Code", "Le code produit est obligatoire"));

        if (code.Length > 50)
            return Result.Failure<Product>(Error.Validation("Code", "Le code produit ne peut pas dépasser 50 caractères"));

        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<Product>(Error.Validation("Name", "Le nom du produit est obligatoire"));

        if (name.Length > 200)
            return Result.Failure<Product>(Error.Validation("Name", "Le nom du produit ne peut pas dépasser 200 caractères"));

        if (unitPrice.Amount < 0)
            return Result.Failure<Product>(Error.Validation("UnitPrice", "Le prix unitaire ne peut pas être négatif"));

        if (purchasePrice is { Amount: < 0 })
            return Result.Failure<Product>(Error.Validation("PurchasePrice", "Le prix d'achat ne peut pas être négatif"));

        // Only physical products can have stock management
        if (isStockManaged && type is ProductType.Service or ProductType.Subscription)
            return Result.Failure<Product>(Error.Validation("IsStockManaged", "La gestion de stock n'est pas applicable aux services"));

        if (categoryId == Guid.Empty)
            return Result.Failure<Product>(Error.Validation("CategoryId", "La catégorie produit est obligatoire"));

        var discountValidation = ValidateDiscountSettings(isDiscountEnabled, maxDiscountPercent);
        if (discountValidation.IsFailure)
            return Result.Failure<Product>(discountValidation.Error);

        if (profitMarginPercent.HasValue &&
            (profitMarginPercent.Value < ProductPricingCalculator.MinProfitMarginPercent ||
             profitMarginPercent.Value > ProductPricingCalculator.MaxProfitMarginPercent))
        {
            return Result.Failure<Product>(Error.Validation(
                "ProfitMarginPercent",
                $"La marge doit être comprise entre {ProductPricingCalculator.MinProfitMarginPercent} % et {ProductPricingCalculator.MaxProfitMarginPercent} %"));
        }

        var product = new Product
        {
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            Description = description?.Trim(),
            Type = type,
            UnitPrice = unitPrice,
            PurchasePrice = purchasePrice,
            VatRate = vatRate,
            Unit = unit?.Trim(),
            IsActive = true,
            IsStockManaged = isStockManaged,
            CategoryId = categoryId,
            IsFodecApplicable = isFodecApplicable,
            ProfitMarginPercent = ProductPricingCalculator.ResolveProfitMarginPercent(
                purchasePrice?.Amount,
                unitPrice.Amount) ?? profitMarginPercent,
            IsDiscountEnabled = isDiscountEnabled,
            MaxDiscountPercent = isDiscountEnabled ? maxDiscountPercent : null,
            TrackingMode = TrackingMode.None,
            CostingMethod = CostingMethod.Average,
            PickingPolicy = PickingPolicy.None
        };

        return Result.Success(product);
    }

    public void Update(
        string name,
        string? description,
        Money unitPrice,
        VatRate vatRate,
        string? unit,
        Money? purchasePrice = null,
        Guid? categoryId = null,
        bool? isFodecApplicable = null,
        bool? isDiscountEnabled = null,
        decimal? maxDiscountPercent = null)
    {
        if (!string.IsNullOrWhiteSpace(name))
            Name = name.Trim();

        Description = description?.Trim();
        UnitPrice = unitPrice;
        VatRate = vatRate;
        Unit = unit?.Trim();

        if (purchasePrice is { Amount: < 0 })
            throw new ArgumentException("Le prix d'achat ne peut pas être négatif", nameof(purchasePrice));

        PurchasePrice = purchasePrice;

        if (categoryId.HasValue && categoryId.Value != Guid.Empty)
            CategoryId = categoryId.Value;

        if (isFodecApplicable.HasValue)
            IsFodecApplicable = isFodecApplicable.Value;

        ProfitMarginPercent = ProductPricingCalculator.ResolveProfitMarginPercent(
            PurchasePrice?.Amount,
            UnitPrice.Amount);

        if (isDiscountEnabled.HasValue || maxDiscountPercent.HasValue)
        {
            var enabled = isDiscountEnabled ?? IsDiscountEnabled;
            var maxDiscount = maxDiscountPercent ?? MaxDiscountPercent;
            ApplyDiscountSettings(enabled, maxDiscount);
        }
    }

    public void SetFodecApplicable(bool value)
    {
        IsFodecApplicable = value;
    }

    /// <summary>
    /// Affecte ou retire le code-barres. Passer <c>null</c> ou une chaîne vide le retire ;
    /// une valeur invalide (clé de contrôle fausse, longueur incorrecte) est refusée à la
    /// saisie plutôt que d'être découverte au premier scan.
    /// </summary>
    public Result SetBarcode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Barcode = null;
            return Result.Success();
        }

        var result = ValueObjects.Barcode.Create(value);
        if (result.IsFailure)
            return Result.Failure(result.Error);

        Barcode = result.Value;
        return Result.Success();
    }

    /// <summary>
    /// Updates discount settings on the product catalog.
    /// </summary>
    public Result UpdateDiscountSettings(bool isDiscountEnabled, decimal? maxDiscountPercent)
    {
        var validation = ValidateDiscountSettings(isDiscountEnabled, maxDiscountPercent);
        if (validation.IsFailure)
            return validation;

        ApplyDiscountSettings(isDiscountEnabled, maxDiscountPercent);
        return Result.Success();
    }

    /// <summary>
    /// Updates the last purchase price from a goods receipt (does not change catalog purchase price).
    /// </summary>
    public void UpdateLastPurchasePrice(Money price)
    {
        if (price.Amount < 0)
            throw new ArgumentException("Le dernier prix d'achat ne peut pas être négatif", nameof(price));

        LastPurchasePrice = price;
    }

    /// <summary>
    /// Sale price TTC derived from current catalog prices.
    /// </summary>
    public decimal CalculateSalePriceTtc() =>
        ProductPricingCalculator.CalculateSaleTtc(UnitPrice.Amount, VatRate, IsFodecApplicable);

    private void ApplyDiscountSettings(bool isDiscountEnabled, decimal? maxDiscountPercent)
    {
        IsDiscountEnabled = isDiscountEnabled;
        MaxDiscountPercent = isDiscountEnabled ? maxDiscountPercent : null;
    }

    private static Result ValidateDiscountSettings(bool isDiscountEnabled, decimal? maxDiscountPercent)
    {
        if (!isDiscountEnabled)
            return Result.Success();

        if (!maxDiscountPercent.HasValue)
        {
            return Result.Failure(Error.Validation(
                "MaxDiscountPercent",
                "La remise maximale est obligatoire lorsque la remise produit est activée"));
        }

        if (maxDiscountPercent.Value < 0 || maxDiscountPercent.Value > 100)
        {
            return Result.Failure(Error.Validation(
                "MaxDiscountPercent",
                "La remise maximale doit être comprise entre 0 % et 100 %"));
        }

        return Result.Success();
    }

    /// <summary>
    /// Returns the effective purchase price. If PurchasePrice is set, returns it; otherwise returns UnitPrice.
    /// </summary>
    public Money GetPurchasePrice() => PurchasePrice ?? UnitPrice;

    /// <summary>
    /// Updates the purchase price. Pass null to clear it.
    /// </summary>
    public void UpdatePurchasePrice(Money? newPrice)
    {
        if (newPrice is { Amount: < 0 })
            throw new ArgumentException("Le prix d'achat ne peut pas être négatif", nameof(newPrice));

        PurchasePrice = newPrice;
    }

    public void UpdatePrice(Money newPrice)
    {
        if (newPrice.Amount < 0)
            throw new ArgumentException("Le prix ne peut pas être négatif", nameof(newPrice));

        UnitPrice = newPrice;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Reactivate()
    {
        IsActive = true;
    }

    /// <summary>
    /// Enables stock management for this product.
    /// Only applicable to physical products.
    /// </summary>
    public Result EnableStockManagement()
    {
        if (Type is ProductType.Service or ProductType.Subscription)
            return Result.Failure(Error.Validation("Type", "La gestion de stock n'est pas applicable aux services et abonnements"));

        if (IsVariantTemplate)
            return Result.Failure(Error.Validation("IsVariantTemplate",
                "Un modèle de variantes ne peut pas gérer de stock. Activez le stock sur chaque SKU enfant."));

        IsStockManaged = true;
        return Result.Success();
    }

    public Result MarkAsVariantTemplate()
    {
        if (Type is ProductType.Service or ProductType.Subscription)
            return Result.Failure(Error.Validation("Type", "Un service ou abonnement ne peut pas être un modèle de variantes"));
        if (ParentProductId.HasValue)
            return Result.Failure(Error.Validation("ParentProductId", "Une variante ne peut pas devenir un modèle"));

        IsVariantTemplate = true;
        IsStockManaged = false;
        IsPubliclyListed = false;
        return Result.Success();
    }

    public Result AttachToParent(Guid parentProductId)
    {
        if (parentProductId == Guid.Empty)
            return Result.Failure(Error.Validation("ParentProductId", "Le produit parent est obligatoire"));
        if (IsVariantTemplate)
            return Result.Failure(Error.Validation("IsVariantTemplate", "Un modèle ne peut pas être enfant d'un autre modèle"));

        ParentProductId = parentProductId;
        return Result.Success();
    }

    public Result ConfigureTraceability(
        TrackingMode trackingMode,
        bool hasExpiryTracking,
        PickingPolicy pickingPolicy,
        CostingMethod costingMethod,
        int? expiryAlertDays)
    {
        if (Type is ProductType.Service or ProductType.Subscription && trackingMode != TrackingMode.None)
            return Result.Failure(Error.Validation("TrackingMode", "La traçabilité n'est pas applicable aux services"));

        if (hasExpiryTracking && trackingMode == TrackingMode.None)
            return Result.Failure(Error.Validation("HasExpiryTracking",
                "Le suivi de péremption exige un suivi par lot ou par numéro de série"));

        if (expiryAlertDays is < 0 or > 3650)
            return Result.Failure(Error.Validation("ExpiryAlertDays", "L'alerte de péremption doit être entre 0 et 3650 jours"));

        TrackingMode = trackingMode;
        HasExpiryTracking = hasExpiryTracking;
        PickingPolicy = pickingPolicy;
        CostingMethod = costingMethod;
        ExpiryAlertDays = expiryAlertDays;
        return Result.Success();
    }

    /// <summary>
    /// Disables stock management for this product.
    /// </summary>
    public void DisableStockManagement()
    {
        IsStockManaged = false;
    }

    // ─────────── Replenishment V2 setters (all optional, additive) ───────────

    /// <summary>
    /// Sets (or clears) the preferred supplier. Pass <c>null</c> to clear.
    /// No validation against Supplier existence — this is enforced at the application layer.
    /// </summary>
    public void SetPreferredSupplier(Guid? supplierId)
    {
        if (supplierId.HasValue && supplierId.Value == Guid.Empty)
            throw new ArgumentException("PreferredSupplierId must be a valid GUID or null", nameof(supplierId));
        PreferredSupplierId = supplierId;
    }

    /// <summary>
    /// Sets (or clears) the minimum order quantity. Pass <c>null</c> to clear.
    /// </summary>
    public void SetMinimumOrderQuantity(decimal? moq)
    {
        if (moq.HasValue && moq.Value <= 0)
            throw new ArgumentException("MinimumOrderQuantity must be > 0 (or null to clear)", nameof(moq));
        MinimumOrderQuantity = moq;
    }

    /// <summary>
    /// Sets (or clears) the packaging hint. Both arguments must be provided together
    /// (label + qty) or both null to clear.
    /// </summary>
    public void SetPackaging(string? unit, decimal? qty)
    {
        if ((unit is null) != (qty is null))
            throw new ArgumentException("PackagingUnit and PackagingQty must both be set or both null");
        if (qty.HasValue && qty.Value <= 0)
            throw new ArgumentException("PackagingQty must be > 0", nameof(qty));
        if (unit is not null && (string.IsNullOrWhiteSpace(unit) || unit.Length > 50))
            throw new ArgumentException("PackagingUnit must be 1..50 chars", nameof(unit));

        PackagingUnit = unit?.Trim();
        PackagingQty = qty;
    }

    /// <summary>
    /// Sets (or clears) the per-product lead-time override. Pass <c>null</c> to fall back
    /// to <see cref="Configuration.ForecastingOptions.DefaultLeadTimeDays"/>.
    /// </summary>
    public void SetLeadTimeDaysOverride(int? days)
    {
        if (days.HasValue && (days.Value < 0 || days.Value > 365))
            throw new ArgumentException("LeadTimeDaysOverride must be in [0..365] (or null to clear)", nameof(days));
        LeadTimeDaysOverride = days;
    }

    public Money CalculateVatAmount(decimal quantity)
    {
        var subtotal = UnitPrice.Multiply(quantity);
        return subtotal.ApplyPercentage(VatRate.ToDecimal());
    }

    public Money CalculateTotalWithVat(decimal quantity)
    {
        var subtotal = UnitPrice.Multiply(quantity);
        var vat = CalculateVatAmount(quantity);
        return subtotal.Add(vat);
    }
}

/// <summary>
/// Type of product or service.
/// </summary>
public enum ProductType
{
    /// <summary>
    /// Physical product.
    /// </summary>
    Product = 0,

    /// <summary>
    /// Service offering.
    /// </summary>
    Service = 1,

    /// <summary>
    /// Recurring subscription / B2B contract line item.
    /// </summary>
    Subscription = 2
}

public static class ProductTypeExtensions
{
    public static string ToDisplayString(this ProductType type) => type switch
    {
        ProductType.Product => "Produit",
        ProductType.Service => "Service",
        ProductType.Subscription => "Abonnement",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
