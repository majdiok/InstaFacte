using FactuTrust.Domain.Common;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities.Storefront;

/// <summary>
/// Materialized projection of a tenant product, published in the Master database for
/// anonymous read access. Never contains private data (margin, stock quantity, supplier).
/// Upserted by <c>StorefrontProjectionSyncService</c> from tenant outbox events.
/// </summary>
public sealed class StorefrontProduct : Entity
{
    public const int NameMaxLength = 200;
    public const int DescriptionMaxLength = 2000;

    public Guid StorefrontProfileId { get; private set; }
    public Guid TenantId { get; private set; }

    /// <summary>Points to the originating Products.Id in the tenant database.</summary>
    public Guid SourceProductId { get; private set; }

    public string Slug { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? DescriptionSanitized { get; private set; }

    public decimal PriceAmount { get; private set; }
    public string PriceCurrency { get; private set; } = Money.DefaultCurrency;

    /// <summary>Absolute URL served from wwwroot/public-storefronts/.</summary>
    public string? PublicImageUrl { get; private set; }
    public string? ImageHash { get; private set; }

    public string? CategoryLabel { get; private set; }

    public StockDisplayStatus StockDisplayStatus { get; private set; }
    public bool IsVisible { get; private set; }
    public int DisplayOrder { get; private set; }

    public DateTime LastSyncedAt { get; private set; }
    public int SourceVersion { get; private set; }

    private StorefrontProduct() { }

    public static Result<StorefrontProduct> Create(
        Guid storefrontProfileId,
        Guid tenantId,
        Guid sourceProductId,
        string slug,
        string name,
        decimal priceAmount,
        string priceCurrency,
        int sourceVersion)
    {
        if (storefrontProfileId == Guid.Empty)
            return Result.Failure<StorefrontProduct>(Error.Validation("StorefrontProfileId", "Profil vitrine invalide"));
        if (tenantId == Guid.Empty)
            return Result.Failure<StorefrontProduct>(Error.Validation("TenantId", "Société invalide"));
        if (sourceProductId == Guid.Empty)
            return Result.Failure<StorefrontProduct>(Error.Validation("SourceProductId", "Produit source invalide"));
        if (string.IsNullOrWhiteSpace(slug))
            return Result.Failure<StorefrontProduct>(Error.Validation("Slug", "Le slug produit est obligatoire"));
        if (string.IsNullOrWhiteSpace(name) || name.Length > NameMaxLength)
            return Result.Failure<StorefrontProduct>(Error.Validation("Name", $"Nom produit obligatoire et limité à {NameMaxLength}"));
        if (priceAmount < 0)
            return Result.Failure<StorefrontProduct>(Error.Validation("PriceAmount", "Prix négatif interdit"));

        var product = new StorefrontProduct
        {
            StorefrontProfileId = storefrontProfileId,
            TenantId = tenantId,
            SourceProductId = sourceProductId,
            Slug = slug.Trim().ToLowerInvariant(),
            Name = name.Trim(),
            PriceAmount = Math.Round(priceAmount, 3),
            PriceCurrency = string.IsNullOrWhiteSpace(priceCurrency) ? Money.DefaultCurrency : priceCurrency.Trim().ToUpperInvariant(),
            StockDisplayStatus = StockDisplayStatus.OnDemand,
            IsVisible = true,
            DisplayOrder = 0,
            SourceVersion = sourceVersion,
            LastSyncedAt = DateTime.UtcNow
        };

        return Result.Success(product);
    }

    /// <summary>
    /// Applies an idempotent upsert from the sync service. The <paramref name="sourceVersion"/>
    /// acts as an optimistic concurrency marker: updates below the current known version are ignored.
    /// </summary>
    public bool ApplyUpsert(
        string name,
        string? descriptionSanitized,
        decimal priceAmount,
        string priceCurrency,
        string? publicImageUrl,
        string? imageHash,
        string? categoryLabel,
        StockDisplayStatus stockDisplayStatus,
        bool isVisible,
        int displayOrder,
        int sourceVersion)
    {
        if (sourceVersion <= SourceVersion)
            return false;

        Name = (name ?? string.Empty).Trim();
        DescriptionSanitized = string.IsNullOrWhiteSpace(descriptionSanitized) ? null : descriptionSanitized.Trim();
        PriceAmount = Math.Round(priceAmount, 3);
        PriceCurrency = string.IsNullOrWhiteSpace(priceCurrency) ? Money.DefaultCurrency : priceCurrency.Trim().ToUpperInvariant();
        PublicImageUrl = string.IsNullOrWhiteSpace(publicImageUrl) ? null : publicImageUrl.Trim();
        ImageHash = string.IsNullOrWhiteSpace(imageHash) ? null : imageHash.Trim();
        CategoryLabel = string.IsNullOrWhiteSpace(categoryLabel) ? null : categoryLabel.Trim();
        StockDisplayStatus = stockDisplayStatus;
        IsVisible = isVisible;
        DisplayOrder = displayOrder;
        SourceVersion = sourceVersion;
        LastSyncedAt = DateTime.UtcNow;
        return true;
    }

    public void MarkInvisible()
    {
        IsVisible = false;
        LastSyncedAt = DateTime.UtcNow;
    }
}
