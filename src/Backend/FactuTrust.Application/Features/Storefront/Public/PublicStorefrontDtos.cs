using FactuTrust.Domain.Entities.Storefront;

namespace FactuTrust.Application.Features.Storefront.Public;

public sealed record PublicStreetMapEntryDto
{
    public string Slug { get; init; } = null!;
    public string DisplayName { get; init; } = null!;
    public string? Tagline { get; init; }
    public string? PublicLogoUrl { get; init; }
    public StorefrontCategory Category { get; init; }
    public int StreetPositionIndex { get; init; }
    public FacadeTheme FacadeTheme { get; init; }
    public string BrandPrimaryColorHex { get; init; } = "#2563EB";
    public string BrandSecondaryColorHex { get; init; } = "#0EA5E9";
}

public sealed record PublicStorefrontDetailDto
{
    public Guid Id { get; init; }
    public string Slug { get; init; } = null!;
    public string DisplayName { get; init; } = null!;
    public string? Tagline { get; init; }
    public string? DescriptionMarkdown { get; init; }
    public string BrandPrimaryColorHex { get; init; } = "#2563EB";
    public string BrandSecondaryColorHex { get; init; } = "#0EA5E9";
    public string? PublicLogoUrl { get; init; }
    public string? PublicCoverImageUrl { get; init; }
    public StorefrontCategory Category { get; init; }
    public FacadeTheme FacadeTheme { get; init; }
    public string PublicContactEmail { get; init; } = null!;
    public string? PublicContactPhone { get; init; }
    public string? PublicContactWhatsApp { get; init; }
    public bool OrderSubmissionEnabled { get; init; }
    public int StreetPositionIndex { get; init; }
}

public sealed record PublicStorefrontProductDto
{
    public Guid Id { get; init; }
    public string Slug { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string? DescriptionSanitized { get; init; }
    public decimal PriceAmount { get; init; }
    public string PriceCurrency { get; init; } = "TND";
    public string? PublicImageUrl { get; init; }
    public string? CategoryLabel { get; init; }
    public StockDisplayStatus StockDisplayStatus { get; init; }
}

public sealed record PublicStorefrontCategoryCountDto
{
    public string CategoryKey { get; init; } = null!;
    public string Label { get; init; } = null!;
    public int Count { get; init; }
}

public sealed record SubmitPublicStorefrontOrderRequest
{
    public string GuestFullName { get; init; } = null!;
    public string GuestEmail { get; init; } = null!;
    public string GuestPhone { get; init; } = null!;
    public string DeliveryStreet { get; init; } = null!;
    public string? DeliveryStreetLine2 { get; init; }
    public string DeliveryCity { get; init; } = null!;
    public string? DeliveryPostalCode { get; init; }
    public string DeliveryGovernorate { get; init; } = null!;
    public string DeliveryCountry { get; init; } = "Tunisie";
    public string? GuestNotes { get; init; }
    public IReadOnlyList<PublicOrderLineRequest> Lines { get; init; } = Array.Empty<PublicOrderLineRequest>();
}

public sealed record PublicOrderLineRequest
{
    public Guid StorefrontProductId { get; init; }
    public decimal Quantity { get; init; }
}

public sealed record SubmitPublicStorefrontOrderResponse
{
    public Guid OrderId { get; init; }
}
