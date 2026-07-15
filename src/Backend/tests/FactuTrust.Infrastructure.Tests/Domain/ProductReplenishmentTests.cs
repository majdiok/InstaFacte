using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

/// <summary>
/// Unit tests for the Replenishment V2 enrichment fields on <see cref="Product"/>:
/// <c>PreferredSupplierId</c>, <c>MinimumOrderQuantity</c>, <c>PackagingUnit</c>/<c>PackagingQty</c>,
/// <c>LeadTimeDaysOverride</c>. These fields are all optional and default to null on legacy products.
/// </summary>
public sealed class ProductReplenishmentTests
{
    private static Product CreateProduct()
    {
        var unitPrice = Money.Create(100m, "TND");
        var result = Product.Create(
            code: "TEST-001",
            name: "Test Product",
            type: ProductType.Product,
            unitPrice: unitPrice,
            vatRate: VatRate.Standard,
            categoryId: Guid.NewGuid(),
            description: null,
            unit: "Pièce",
            isStockManaged: true);
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    // ─────────── Defaults ───────────

    [Fact]
    public void NewProduct_ReplenishmentV2Fields_AreAllNull()
    {
        var product = CreateProduct();

        Assert.Null(product.PreferredSupplierId);
        Assert.Null(product.MinimumOrderQuantity);
        Assert.Null(product.PackagingUnit);
        Assert.Null(product.PackagingQty);
        Assert.Null(product.LeadTimeDaysOverride);
    }

    // ─────────── PreferredSupplier ───────────

    [Fact]
    public void SetPreferredSupplier_WithValidGuid_StoresIt()
    {
        var product = CreateProduct();
        var supplierId = Guid.NewGuid();

        product.SetPreferredSupplier(supplierId);

        Assert.Equal(supplierId, product.PreferredSupplierId);
    }

    [Fact]
    public void SetPreferredSupplier_WithNull_ClearsIt()
    {
        var product = CreateProduct();
        product.SetPreferredSupplier(Guid.NewGuid());

        product.SetPreferredSupplier(null);

        Assert.Null(product.PreferredSupplierId);
    }

    [Fact]
    public void SetPreferredSupplier_WithEmptyGuid_Throws()
    {
        var product = CreateProduct();
        Assert.Throws<ArgumentException>(() => product.SetPreferredSupplier(Guid.Empty));
    }

    // ─────────── MOQ ───────────

    [Fact]
    public void SetMinimumOrderQuantity_WithPositive_StoresIt()
    {
        var product = CreateProduct();
        product.SetMinimumOrderQuantity(100m);
        Assert.Equal(100m, product.MinimumOrderQuantity);
    }

    [Fact]
    public void SetMinimumOrderQuantity_WithNull_ClearsIt()
    {
        var product = CreateProduct();
        product.SetMinimumOrderQuantity(100m);

        product.SetMinimumOrderQuantity(null);

        Assert.Null(product.MinimumOrderQuantity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SetMinimumOrderQuantity_WithNonPositive_Throws(decimal moq)
    {
        var product = CreateProduct();
        Assert.Throws<ArgumentException>(() => product.SetMinimumOrderQuantity(moq));
    }

    // ─────────── Packaging ───────────

    [Fact]
    public void SetPackaging_WithBothValues_StoresThem()
    {
        var product = CreateProduct();
        product.SetPackaging("Palette", 500m);

        Assert.Equal("Palette", product.PackagingUnit);
        Assert.Equal(500m, product.PackagingQty);
    }

    [Fact]
    public void SetPackaging_WithBothNull_ClearsThem()
    {
        var product = CreateProduct();
        product.SetPackaging("Palette", 500m);

        product.SetPackaging(null, null);

        Assert.Null(product.PackagingUnit);
        Assert.Null(product.PackagingQty);
    }

    [Fact]
    public void SetPackaging_WithOnlyUnit_Throws()
    {
        var product = CreateProduct();
        Assert.Throws<ArgumentException>(() => product.SetPackaging("Palette", null));
    }

    [Fact]
    public void SetPackaging_WithOnlyQty_Throws()
    {
        var product = CreateProduct();
        Assert.Throws<ArgumentException>(() => product.SetPackaging(null, 500m));
    }

    [Fact]
    public void SetPackaging_WithNonPositiveQty_Throws()
    {
        var product = CreateProduct();
        Assert.Throws<ArgumentException>(() => product.SetPackaging("Palette", 0m));
    }

    [Fact]
    public void SetPackaging_WithEmptyUnit_Throws()
    {
        var product = CreateProduct();
        Assert.Throws<ArgumentException>(() => product.SetPackaging("", 10m));
    }

    [Fact]
    public void SetPackaging_WithUnitOver50Chars_Throws()
    {
        var product = CreateProduct();
        var longUnit = new string('A', 51);
        Assert.Throws<ArgumentException>(() => product.SetPackaging(longUnit, 10m));
    }

    // ─────────── LeadTimeOverride ───────────

    [Fact]
    public void SetLeadTimeDaysOverride_WithValidDays_StoresIt()
    {
        var product = CreateProduct();
        product.SetLeadTimeDaysOverride(14);
        Assert.Equal(14, product.LeadTimeDaysOverride);
    }

    [Fact]
    public void SetLeadTimeDaysOverride_WithNull_ClearsIt()
    {
        var product = CreateProduct();
        product.SetLeadTimeDaysOverride(14);

        product.SetLeadTimeDaysOverride(null);

        Assert.Null(product.LeadTimeDaysOverride);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(366)]
    public void SetLeadTimeDaysOverride_OutOfRange_Throws(int days)
    {
        var product = CreateProduct();
        Assert.Throws<ArgumentException>(() => product.SetLeadTimeDaysOverride(days));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(365)]
    public void SetLeadTimeDaysOverride_WithBoundaryValues_Accepted(int days)
    {
        var product = CreateProduct();
        product.SetLeadTimeDaysOverride(days);
        Assert.Equal(days, product.LeadTimeDaysOverride);
    }
}
