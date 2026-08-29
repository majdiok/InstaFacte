using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

public sealed class FixedAssetDomainTests
{
    private static readonly Guid CategoryId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0001");

    private static Result<FixedAsset> CreateValidAsset(
        decimal cost = 30_000m,
        decimal residual = 0m,
        decimal rate = 15m) =>
        FixedAsset.Create(
            "IMMO-2026-0001",
            "Machine industrielle",
            CategoryId,
            rate,
            rate > 0 ? 100m / rate : 0m,
            "213",
            "2813",
            "6813",
            cost,
            0m,
            residual,
            new DateTime(2026, 1, 15),
            description: "Test");

    [Fact]
    public void FixedAsset_Create_WithValidInput_ShouldSucceed()
    {
        var result = CreateValidAsset();

        Assert.True(result.IsSuccess);
        Assert.Equal(FixedAssetStatus.Draft, result.Value.Status);
        Assert.Equal(30_000m, result.Value.NetBookValue);
        Assert.Single(result.Value.Events);
    }

    [Fact]
    public void FixedAsset_LinkSupplierInvoiceSource_ShouldSetIds()
    {
        var result = CreateValidAsset();
        var asset = result.Value;
        var invoiceId = Guid.NewGuid();
        var lineId = Guid.NewGuid();

        asset.LinkSupplierInvoiceSource(invoiceId, lineId);

        Assert.Equal(invoiceId, asset.SupplierInvoiceId);
        Assert.Equal(lineId, asset.SupplierInvoiceLineId);
    }

    [Fact]
    public void FixedAsset_Create_WithResidualGreaterThanCost_ShouldFail()
    {
        var result = CreateValidAsset(cost: 10_000m, residual: 10_000m);

        Assert.True(result.IsFailure);
        Assert.Contains("résiduelle", result.Error.Description);
    }

    [Fact]
    public void FixedAsset_PutInService_FromDraft_ShouldSucceed()
    {
        var asset = CreateValidAsset().Value;
        var result = asset.PutInService(new DateTime(2026, 3, 1), "404");

        Assert.True(result.IsSuccess);
        Assert.Equal(FixedAssetStatus.InService, asset.Status);
        Assert.Equal(new DateTime(2026, 3, 1), asset.InServiceDate);
    }

    [Fact]
    public void FixedAsset_PutInService_BeforeAcquisition_ShouldFail()
    {
        var asset = CreateValidAsset().Value;
        var result = asset.PutInService(new DateTime(2026, 1, 1), "404");

        Assert.True(result.IsFailure);
        Assert.Contains("mise en service", result.Error.Description);
    }

    [Fact]
    public void FixedAsset_UpdateDraft_WhenInService_ShouldFail()
    {
        var asset = CreateValidAsset().Value;
        asset.PutInService(new DateTime(2026, 3, 1), "404");

        var result = asset.UpdateDraft(
            "Nouveau libellé", null, 30_000m, 0m, 0m, new DateTime(2026, 1, 15),
            15m, 6.67m, "213", "2813", "6813", null);

        Assert.True(result.IsFailure);
        Assert.Contains("brouillon", result.Error.Description);
    }

    [Fact]
    public void FixedAsset_Dispose_WhenInService_ShouldSucceed()
    {
        var asset = CreateValidAsset().Value;
        asset.PutInService(new DateTime(2026, 3, 1), "404");

        var result = asset.Dispose(new DateTime(2028, 6, 30), 12_000m, "5321");

        Assert.True(result.IsSuccess);
        Assert.Equal(FixedAssetStatus.Disposed, asset.Status);
        Assert.Equal(12_000m, asset.DisposalProceeds);
    }

    [Fact]
    public void FixedAsset_Dispose_FromDraft_ShouldFail()
    {
        var asset = CreateValidAsset().Value;
        var result = asset.Dispose(new DateTime(2028, 6, 30), 0m, "5321");

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void FixedAsset_Create_AcceleratedWithInvalidCoefficient_ShouldFail()
    {
        var result = FixedAsset.Create(
            "IMMO-2026-0003", "Machine", CategoryId, 15m, 6.67m,
            "213", "2813", "6813", 10_000m, 0m, 0m, new DateTime(2026, 1, 15),
            depreciationMethod: DepreciationMethod.Accelerated,
            accelerationCoefficient: 1m);

        Assert.True(result.IsFailure);
        Assert.Contains("coefficient", result.Error.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FixedAsset_Create_AcceleratedWithValidCoefficient_ShouldSucceed()
    {
        var result = FixedAsset.Create(
            "IMMO-2026-0004", "Machine", CategoryId, 15m, 6.67m,
            "213", "2813", "6813", 10_000m, 0m, 0m, new DateTime(2026, 1, 15),
            depreciationMethod: DepreciationMethod.Accelerated,
            accelerationCoefficient: 2m);

        Assert.True(result.IsSuccess);
        Assert.Equal(DepreciationMethod.Accelerated, result.Value.DepreciationMethod);
        Assert.Equal(2m, result.Value.AccelerationCoefficient);
    }

    [Theory]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public void FixedAsset_Create_AcceleratedWithAllowedCoefficients_ShouldSucceed(decimal coefficient)
    {
        var result = FixedAsset.Create(
            "IMMO-2026-0005", "Machine", CategoryId, 15m, 6.67m,
            "213", "2813", "6813", 10_000m, 0m, 0m, new DateTime(2026, 1, 15),
            depreciationMethod: DepreciationMethod.Accelerated,
            accelerationCoefficient: coefficient);

        Assert.True(result.IsSuccess);
        Assert.Equal(coefficient, result.Value.AccelerationCoefficient);
    }

    [Fact]
    public void FixedAsset_UpdateDraft_ShouldUpdateMethodAndVat()
    {
        var asset = CreateValidAsset().Value;

        var result = asset.UpdateDraft(
            "Machine modifiée", null, 30_000m, 0m, 0m, new DateTime(2026, 1, 15),
            15m, 6.67m, "213", "2813", "6813", null,
            depreciationMethod: DepreciationMethod.Integral,
            vatAmount: 5_700m);

        Assert.True(result.IsSuccess);
        Assert.Equal(DepreciationMethod.Integral, asset.DepreciationMethod);
        Assert.Equal(5_700m, asset.VatAmount);
    }

    [Fact]
    public void DepreciationScheduleLine_Create_WithValidInput_ShouldSucceed()
    {
        var assetId = Guid.NewGuid();
        var result = DepreciationScheduleLine.Create(
            assetId, 2026, null, 30_000m, 4_500m, 0m, 3_750m, 3_750m, 26_250m);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsPosted);
    }

    [Fact]
    public void DepreciationRateCategory_UsefulLifeYears_ShouldDeriveFromRate()
    {
        var cat = DepreciationRateCategory.Create(
            "VEH_UTIL", "Véhicule utilitaire", 20m, "228", "2828", "68112", false, 10);

        Assert.Equal(5m, cat.UsefulLifeYears);
    }

    [Fact]
    public void DepreciationRateCategory_NonDepreciable_ShouldHaveZeroLife()
    {
        var cat = DepreciationRateCategory.Create(
            "LAND", "Terrains", 0m, "211", "2811", "6811", true, 1);

        Assert.Equal(0m, cat.UsefulLifeYears);
    }

    // ------------------------------------------------------------------
    // T2 — TVA capitalisée (véhicules de tourisme)
    // ------------------------------------------------------------------

    [Fact]
    public void FixedAsset_Create_VatCapitalized_TotalCapitalizedCost_IncludesVat()
    {
        var result = FixedAsset.Create(
            "IMMO-2026-0010", "Véhicule de tourisme", CategoryId, 20m, 5m,
            "2244", "2824", "68112", 50_000m, 0m, 55_000m, new DateTime(2026, 1, 15),
            vatAmount: 9_500m, vatCapitalized: true);

        Assert.True(result.IsSuccess);
        var asset = result.Value;
        Assert.Equal(59_500m, asset.TotalCapitalizedCost);
        Assert.Equal(59_500m, asset.NetBookValue);
        Assert.Equal(59_500m, asset.DepreciableBase + asset.ResidualValue);
    }

    [Fact]
    public void FixedAsset_Create_VatCapitalized_ResidualBelowTtc_IsAccepted()
    {
        // residualValue = 55 000 < total TTC 59 500 → accepté (alors qu'il serait invalide
        // comparé à la seule base HT 50 000).
        var result = FixedAsset.Create(
            "IMMO-2026-0011", "Véhicule de tourisme", CategoryId, 20m, 5m,
            "2244", "2824", "68112", 50_000m, 0m, 55_000m, new DateTime(2026, 1, 15),
            vatAmount: 9_500m, vatCapitalized: true);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void FixedAsset_Create_VatCapitalized_ResidualAboveTtc_IsRejected()
    {
        var result = FixedAsset.Create(
            "IMMO-2026-0012", "Véhicule de tourisme", CategoryId, 20m, 5m,
            "2244", "2824", "68112", 50_000m, 0m, 60_000m, new DateTime(2026, 1, 15),
            vatAmount: 9_500m, vatCapitalized: true);

        Assert.True(result.IsFailure);
        Assert.Contains("résiduelle", result.Error.Description);
    }

    [Fact]
    public void FixedAsset_Create_StandardAsset_TotalCapitalizedCost_ExcludesVat()
    {
        // Actif standard (VatCapitalized = false, défaut) : la TVA est déductible (43662), pas
        // capitalisée — base amortissable et VNC restent en HT, invariant historique inchangé.
        var result = FixedAsset.Create(
            "IMMO-2026-0013", "Machine industrielle", CategoryId, 15m, 6.67m,
            "213", "2813", "6813", 50_000m, 0m, 0m, new DateTime(2026, 1, 15),
            vatAmount: 9_500m);

        Assert.True(result.IsSuccess);
        var asset = result.Value;
        Assert.False(asset.VatCapitalized);
        Assert.Equal(50_000m, asset.TotalCapitalizedCost);
        Assert.Equal(50_000m, asset.NetBookValue);
    }

    [Fact]
    public void FixedAsset_UpdateDraft_TogglingVatCapitalized_RecomputesNetBookValue()
    {
        var asset = CreateValidAsset(cost: 50_000m, residual: 0m, rate: 20m).Value;

        var result = asset.UpdateDraft(
            "Véhicule de tourisme", null, 50_000m, 0m, 55_000m, new DateTime(2026, 1, 15),
            20m, 5m, "2244", "2824", "68112", null,
            vatAmount: 9_500m, vatCapitalized: true);

        Assert.True(result.IsSuccess);
        Assert.True(asset.VatCapitalized);
        Assert.Equal(59_500m, asset.TotalCapitalizedCost);
        Assert.Equal(59_500m, asset.NetBookValue);
    }

    [Fact]
    public void FixedAsset_UpdateDraft_ExistingAsset_DefaultVatCapitalized_IsUnchanged()
    {
        // Anti-régression : ne pas passer vatCapitalized => la valeur existante (false par défaut)
        // est conservée — aucun actif existant ne change de base ni de VNC.
        var asset = CreateValidAsset(cost: 30_000m).Value;

        var result = asset.UpdateDraft(
            "Nouveau libellé", null, 30_000m, 0m, 0m, new DateTime(2026, 1, 15),
            15m, 6.67m, "213", "2813", "6813", null);

        Assert.True(result.IsSuccess);
        Assert.False(asset.VatCapitalized);
        Assert.Equal(30_000m, asset.NetBookValue);
    }

}