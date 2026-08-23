using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

public sealed class PhysicalInventoryTests
{
    [Fact]
    public void Validate_WithoutAnyRecordCount_ConfirmsUncountedAsTheoretical()
    {
        var productId = Guid.NewGuid();
        var inventory = StartInventory((productId, "Produit A", "A", 10m));

        var result = inventory.Validate();

        Assert.True(result.IsSuccess);
        Assert.Equal(InventoryStatus.Validated, inventory.Status);
        Assert.True(inventory.IsComplete);
        var line = inventory.CountLines.Single();
        Assert.True(line.IsCounted);
        Assert.Equal(10m, line.CountedQuantity);
        Assert.Equal(0m, line.Difference);
        Assert.Empty(inventory.CountLines.Where(l => l.Difference != 0));
    }

    [Fact]
    public void Validate_WithOneDifferenceAndOneUncounted_OnlyKeepsTheRecordedGap()
    {
        var countedId = Guid.NewGuid();
        var uncountedId = Guid.NewGuid();
        var inventory = StartInventory(
            (countedId, "Produit A", "A", 10m),
            (uncountedId, "Produit B", "B", 5m));

        Assert.True(inventory.RecordCount(countedId, 12m).IsSuccess);

        var result = inventory.Validate();

        Assert.True(result.IsSuccess);
        var counted = inventory.CountLines.Single(l => l.ProductId == countedId);
        var confirmed = inventory.CountLines.Single(l => l.ProductId == uncountedId);
        Assert.Equal(12m, counted.CountedQuantity);
        Assert.Equal(2m, counted.Difference);
        Assert.Equal(5m, confirmed.CountedQuantity);
        Assert.Equal(0m, confirmed.Difference);
        Assert.Equal(1, inventory.ProductsWithDifference);
    }

    [Fact]
    public void Difference_UncountedIsZero_CountedIsDelta()
    {
        var productId = Guid.NewGuid();
        var inventory = StartInventory((productId, "Produit A", "A", 10m));
        var line = inventory.CountLines.Single();

        Assert.False(line.IsCounted);
        Assert.Equal(0m, line.Difference);

        Assert.True(inventory.RecordCount(productId, 7m).IsSuccess);
        Assert.Equal(-3m, line.Difference);
    }

    [Fact]
    public void Validate_WhenCancelled_Fails()
    {
        var inventory = StartInventory((Guid.NewGuid(), "Produit A", "A", 4m));
        Assert.True(inventory.Cancel().IsSuccess);

        var result = inventory.Validate();

        Assert.True(result.IsFailure);
        Assert.Equal(InventoryStatus.Cancelled, inventory.Status);
    }

    [Fact]
    public void RecordCount_NegativeQuantity_Fails()
    {
        var productId = Guid.NewGuid();
        var inventory = StartInventory((productId, "Produit A", "A", 4m));

        var result = inventory.RecordCount(productId, -1m);

        Assert.True(result.IsFailure);
        Assert.False(inventory.CountLines.Single().IsCounted);
    }

    private static PhysicalInventory StartInventory(
        params (Guid ProductId, string ProductName, string? ProductCode, decimal TheoreticalQuantity)[] products)
    {
        var start = PhysicalInventory.Start(
            "INVE-000001",
            Guid.NewGuid(),
            InventoryType.Complete,
            products,
            null);
        Assert.True(start.IsSuccess);
        return start.Value;
    }
}
