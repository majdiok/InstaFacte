using System.Text.Json;
using System.Text.Json.Serialization;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.Storefront;

namespace FactuTrust.Infrastructure.Services.Storefront;

public sealed class StorefrontOutboxPayloadBuilder : IStorefrontOutboxPayloadBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string BuildProductPayload(
        Guid storefrontProfileId,
        Guid tenantId,
        Product product,
        string? categoryLabel)
    {
        var slug = ProductSlugFromCode(product.Code);
        var dto = new ProductOutboxDto(
            StorefrontProfileId: storefrontProfileId,
            TenantId: tenantId,
            ProductId: product.Id,
            Slug: slug,
            Name: product.Name,
            Description: string.IsNullOrWhiteSpace(product.Description) ? null : product.Description.Trim(),
            UnitPriceAmount: product.UnitPrice.Amount,
            Currency: product.UnitPrice.Currency,
            PublicImageUrl: string.IsNullOrWhiteSpace(product.ImageUrl) ? null : product.ImageUrl.Trim(),
            ImageHash: null,
            CategoryLabel: categoryLabel,
            StockDisplayStatus: product.IsStockManaged ? StockDisplayStatus.InStock : StockDisplayStatus.OnDemand,
            IsVisible: true,
            DisplayOrder: 0);

        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    public string BuildProductRemovedPayload(Guid storefrontProfileId, Guid tenantId, Guid productId)
    {
        var dto = new ProductRemovedOutboxDto(storefrontProfileId, tenantId, productId);
        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    public string BuildProfilePayload(StorefrontProfile profile)
    {
        var dto = new ProfileOutboxDto(
            profile.Id,
            profile.TenantId,
            profile.Slug,
            profile.DisplayName,
            profile.Status.ToString());
        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    private static string ProductSlugFromCode(string code)
    {
        var s = code.Trim().ToLowerInvariant();
        s = s.Replace(' ', '-');
        foreach (var c in s)
        {
            if (c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-')
                continue;
            return "p-" + s.GetHashCode().ToString("x");
        }

        return string.IsNullOrEmpty(s) ? "produit" : s[..Math.Min(s.Length, 120)];
    }

    private sealed record ProductOutboxDto(
        Guid StorefrontProfileId,
        Guid TenantId,
        Guid ProductId,
        string Slug,
        string Name,
        string? Description,
        decimal UnitPriceAmount,
        string Currency,
        string? PublicImageUrl,
        string? ImageHash,
        string? CategoryLabel,
        StockDisplayStatus StockDisplayStatus,
        bool IsVisible,
        int DisplayOrder);

    private sealed record ProductRemovedOutboxDto(
        Guid StorefrontProfileId,
        Guid TenantId,
        Guid ProductId);

    private sealed record ProfileOutboxDto(
        Guid StorefrontProfileId,
        Guid TenantId,
        string Slug,
        string DisplayName,
        string Status);
}
