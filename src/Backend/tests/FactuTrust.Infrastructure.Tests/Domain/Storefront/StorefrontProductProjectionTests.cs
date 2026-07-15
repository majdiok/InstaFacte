using FactuTrust.Domain.Entities.Storefront;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain.Storefront;

/// <summary>
/// Validates the upsert semantics of <see cref="StorefrontProduct"/>, which acts as a
/// materialized projection synced from tenant outbox events. The optimistic concurrency
/// marker (SourceVersion) must prevent stale updates from overwriting fresher data.
/// </summary>
public sealed class StorefrontProductProjectionTests
{
    private static StorefrontProduct CreateDefault()
    {
        var result = StorefrontProduct.Create(
            storefrontProfileId: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            sourceProductId: Guid.NewGuid(),
            slug: "super-widget",
            name: "Super Widget",
            priceAmount: 49.990m,
            priceCurrency: "TND",
            sourceVersion: 1);

        Assert.True(result.IsSuccess, result.Error?.Description);
        return result.Value;
    }

    [Fact]
    public void ApplyUpsert_ShouldUpdate_WhenSourceVersionIsHigher()
    {
        var product = CreateDefault();

        var applied = product.ApplyUpsert(
            name: "Super Widget v2",
            descriptionSanitized: "Enhanced version",
            priceAmount: 59.990m,
            priceCurrency: "TND",
            publicImageUrl: "https://cdn.factutrust.test/p/abc.webp",
            imageHash: "deadbeef",
            categoryLabel: "Widgets",
            stockDisplayStatus: StockDisplayStatus.InStock,
            isVisible: true,
            displayOrder: 3,
            sourceVersion: 2);

        Assert.True(applied);
        Assert.Equal("Super Widget v2", product.Name);
        Assert.Equal(59.990m, product.PriceAmount);
        Assert.Equal(StockDisplayStatus.InStock, product.StockDisplayStatus);
        Assert.Equal("deadbeef", product.ImageHash);
        Assert.Equal(2, product.SourceVersion);
    }

    [Fact]
    public void ApplyUpsert_ShouldIgnoreStaleUpdate()
    {
        var product = CreateDefault();
        product.ApplyUpsert(
            name: "Super Widget v2",
            descriptionSanitized: null,
            priceAmount: 59.990m,
            priceCurrency: "TND",
            publicImageUrl: null,
            imageHash: null,
            categoryLabel: null,
            stockDisplayStatus: StockDisplayStatus.InStock,
            isVisible: true,
            displayOrder: 0,
            sourceVersion: 5);

        var staleApplied = product.ApplyUpsert(
            name: "Super Widget v1 (should not apply)",
            descriptionSanitized: null,
            priceAmount: 0.01m,
            priceCurrency: "TND",
            publicImageUrl: null,
            imageHash: null,
            categoryLabel: null,
            stockDisplayStatus: StockDisplayStatus.OutOfStock,
            isVisible: false,
            displayOrder: 99,
            sourceVersion: 3);

        Assert.False(staleApplied);
        Assert.Equal("Super Widget v2", product.Name);
        Assert.Equal(59.990m, product.PriceAmount);
        Assert.Equal(StockDisplayStatus.InStock, product.StockDisplayStatus);
        Assert.True(product.IsVisible);
        Assert.Equal(5, product.SourceVersion);
    }

    [Fact]
    public void MarkInvisible_ShouldFlipFlagWithoutVersionBump()
    {
        var product = CreateDefault();
        var initialVersion = product.SourceVersion;

        product.MarkInvisible();

        Assert.False(product.IsVisible);
        Assert.Equal(initialVersion, product.SourceVersion);
    }

    [Fact]
    public void Create_ShouldReject_NegativePrice()
    {
        var result = StorefrontProduct.Create(
            storefrontProfileId: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            sourceProductId: Guid.NewGuid(),
            slug: "bad-product",
            name: "Bad",
            priceAmount: -1m,
            priceCurrency: "TND",
            sourceVersion: 1);

        Assert.True(result.IsFailure);
    }
}
