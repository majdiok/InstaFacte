using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;
using FactuTrust.Domain.ValueObjects;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

public sealed class LotAllocationPolicyTests
{
    [Fact]
    public void Fefo_OrdersByExpiryThenReceipt()
    {
        var a = new LotCandidate(Guid.NewGuid(), "A", new DateTime(2026, 12, 1), new DateTime(2026, 1, 10), 5m);
        var b = new LotCandidate(Guid.NewGuid(), "B", new DateTime(2026, 6, 1), new DateTime(2026, 3, 1), 5m);
        var c = new LotCandidate(Guid.NewGuid(), "C", null, new DateTime(2026, 1, 1), 5m);

        var ordered = LotAllocationPolicy.OrderForPicking(new[] { a, c, b }, PickingPolicy.Fefo);
        Assert.Equal(new[] { "B", "A", "C" }, ordered.Select(x => x.LotNumber).ToArray());
    }

    [Fact]
    public void Allocate_RefusesWhenQuantityExceedsLots()
    {
        var lot = new LotCandidate(Guid.NewGuid(), "L1", DateTime.UtcNow.AddDays(10), DateTime.UtcNow, 3m);
        var result = LotAllocationPolicy.Allocate(5m, new[] { lot }, blockExpired: true, DateTime.UtcNow);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Allocate_SkipsExpiredWhenBlocked()
    {
        var expired = new LotCandidate(Guid.NewGuid(), "OLD", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(-10), 10m);
        var fresh = new LotCandidate(Guid.NewGuid(), "NEW", DateTime.UtcNow.AddDays(30), DateTime.UtcNow, 10m);
        var ordered = LotAllocationPolicy.OrderForPicking(new[] { expired, fresh }, PickingPolicy.Fefo);
        var result = LotAllocationPolicy.Allocate(5m, ordered, blockExpired: true, DateTime.UtcNow);
        Assert.True(result.IsSuccess);
        Assert.Equal("NEW", result.Value.Single().LotNumber);
    }
}

public sealed class ProductCommercialGuardsTests
{
    [Fact]
    public void Template_CannotAppearOnDocument()
    {
        var product = CreateProduct();
        Assert.True(product.MarkAsVariantTemplate().IsSuccess);
        var guard = ProductCommercialGuards.EnsureCanAppearOnDocument(product);
        Assert.True(guard.IsFailure);
    }

    [Fact]
    public void ChildSku_CanAppearOnDocument()
    {
        var product = CreateProduct();
        Assert.True(product.AttachToParent(Guid.NewGuid()).IsSuccess);
        Assert.True(ProductCommercialGuards.EnsureCanAppearOnDocument(product).IsSuccess);
    }

    private static Product CreateProduct() =>
        Product.Create(
            "P1",
            "Produit",
            ProductType.Product,
            Money.Create(1m, Money.DefaultCurrency),
            VatRate.Standard,
            Guid.NewGuid()).Value;
}

public sealed class ProductVariantSkuTests
{
    [Fact]
    public void Build_JoinsParentAndValueCodes()
    {
        var code = ProductVariantSku.Build("TSHIRT", new[] { "M", "BLU" });
        Assert.True(code.IsSuccess);
        Assert.Equal("TSHIRT-M-BLU", code.Value);
    }

    [Fact]
    public void Build_TruncatesToMaxLength()
    {
        var code = ProductVariantSku.Build(new string('A', 40), new[] { "VERYLONGVALUECODE" });
        Assert.True(code.IsSuccess);
        Assert.True(code.Value.Length <= ProductVariantSku.MaxCodeLength);
    }
}

public sealed class LotBalanceInvariantTests
{
    [Fact]
    public void AssertMatchesOnHand_DetectsDrift()
    {
        var result = LotBalanceInvariant.AssertMatchesOnHand(10m, new[] { 4m, 5m });
        Assert.True(result.IsFailure);
    }
}

public sealed class ProductLotTests
{
    [Fact]
    public void Create_NormalizesLotNumber()
    {
        var lot = ProductLot.Create(Guid.NewGuid(), "  ab-12 ");
        Assert.True(lot.IsSuccess);
        Assert.Equal("AB-12", lot.Value.LotNumber);
    }

    [Fact]
    public void Decrease_RefusesOverdraw()
    {
        var balance = StockLotBalance.Create(Guid.NewGuid(), Guid.NewGuid()).Value;
        Assert.True(balance.Increase(2m).IsSuccess);
        Assert.True(balance.Decrease(5m).IsFailure);
    }
}

public sealed class ProductSerialTests
{
    [Fact]
    public void Create_RejectsDuplicateSemanticsViaNormalizedNumber()
    {
        var productId = Guid.NewGuid();
        var a = ProductSerial.Create(productId, "sn-1", Guid.NewGuid());
        var b = ProductSerial.Create(productId, " SN-1 ", Guid.NewGuid());
        Assert.True(a.IsSuccess);
        Assert.True(b.IsSuccess);
        Assert.Equal(a.Value.SerialNumber, b.Value.SerialNumber);
    }
}
