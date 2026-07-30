using FactuTrust.Domain.Services;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

/// <summary>
/// Le répartiteur est le seul point qui décide comment une remise de pied retombe sur les
/// lignes. Son invariant : la somme des parts vaut EXACTEMENT la remise annoncée, sinon le
/// document ne s'équilibre pas au millime.
/// </summary>
public sealed class GlobalDiscountAllocatorTests
{
    [Fact]
    public void Allocate_SplitsProportionally()
    {
        var bases = new[] { 100m, 300m };

        var parts = GlobalDiscountAllocator.Allocate(bases, 40m);

        Assert.Equal(10m, parts[0]);
        Assert.Equal(30m, parts[1]);
    }

    [Fact]
    public void Allocate_SumIsExactlyTheDiscount_EvenWithAwkwardRounding()
    {
        // Trois tiers d'un montant qui ne tombe pas juste : 10 / 3 = 3,333…
        var bases = new[] { 100m, 100m, 100m };

        var parts = GlobalDiscountAllocator.Allocate(bases, 10m);

        Assert.Equal(10m, parts.Sum());
    }

    [Fact]
    public void Allocate_SumIsExact_OnManyUnevenLines()
    {
        var bases = new[] { 13.333m, 7.777m, 91.111m, 0.555m, 42.424m };

        var parts = GlobalDiscountAllocator.Allocate(bases, 17.777m);

        Assert.Equal(17.777m, parts.Sum());
    }

    [Fact]
    public void Allocate_GivesTheResidueToTheHeaviestLine()
    {
        var bases = new[] { 100m, 100m, 100m };

        var parts = GlobalDiscountAllocator.Allocate(bases, 10m);

        // 3,333 × 3 = 9,999 ; le millime manquant va à une ligne, pas nulle part.
        Assert.Equal(10m, parts.Sum());
        Assert.Contains(parts, p => p != 3.333m);
    }

    [Fact]
    public void Allocate_NeverExceedsTheLineBase()
    {
        var bases = new[] { 10m, 990m };

        var parts = GlobalDiscountAllocator.Allocate(bases, 1000m);

        Assert.True(parts[0] <= bases[0]);
        Assert.True(parts[1] <= bases[1]);
    }

    [Fact]
    public void Allocate_CapsADiscountLargerThanTheTotalBase()
    {
        var bases = new[] { 100m, 100m };

        // Une remise de 500 sur une base de 200 rendrait les lignes négatives.
        var parts = GlobalDiscountAllocator.Allocate(bases, 500m);

        Assert.Equal(200m, parts.Sum());
    }

    [Fact]
    public void Allocate_ReturnsZeros_WhenThereIsNoDiscount()
    {
        var parts = GlobalDiscountAllocator.Allocate(new[] { 100m, 200m }, 0m);

        Assert.All(parts, p => Assert.Equal(0m, p));
    }

    [Fact]
    public void Allocate_ReturnsZeros_WhenAllLinesAreFree()
    {
        // Répartir « également » sur des lignes à zéro serait arbitraire.
        var parts = GlobalDiscountAllocator.Allocate(new[] { 0m, 0m }, 50m);

        Assert.All(parts, p => Assert.Equal(0m, p));
    }

    [Fact]
    public void Allocate_HandlesAnEmptyDocument()
    {
        var parts = GlobalDiscountAllocator.Allocate(Array.Empty<decimal>(), 50m);

        Assert.Empty(parts);
    }

    [Fact]
    public void FromPercent_RoundsToTheMillime()
    {
        Assert.Equal(5m, GlobalDiscountAllocator.FromPercent(100m, 5m));
        Assert.Equal(3.333m, GlobalDiscountAllocator.FromPercent(99.99m, 3.3333m));
    }
}
