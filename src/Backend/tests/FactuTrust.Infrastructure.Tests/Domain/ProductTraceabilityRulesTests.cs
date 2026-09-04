using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

public sealed class ProductTraceabilityRulesTests
{
    private static ProductTraceabilityFeatureFlags AllEnabled { get; } = new(
        LotTrackingEnabled: true,
        SerialTrackingEnabled: true,
        ExpiryTrackingEnabled: true,
        FifoLifoValuationEnabled: true);

    private static ProductTraceabilityFeatureFlags AllDisabled { get; } = new(
        LotTrackingEnabled: false,
        SerialTrackingEnabled: false,
        ExpiryTrackingEnabled: false,
        FifoLifoValuationEnabled: false);

    [Fact]
    public void Lot_Fefo_WithExpiry_IsValid()
    {
        var state = new ProductTraceabilityState(
            TrackingMode.Lot,
            HasExpiryTracking: true,
            PickingPolicy.Fefo,
            CostingMethod.Average,
            ExpiryAlertDays: 30);

        var result = ProductTraceabilityRules.ValidateAndNormalize(
            ProductType.Product, isStockManaged: true, AllEnabled, state);

        Assert.True(result.IsSuccess);
        Assert.Equal(PickingPolicy.Fefo, result.Value.PickingPolicy);
    }

    [Fact]
    public void Serial_WithFefo_FailsValidation()
    {
        var state = new ProductTraceabilityState(
            TrackingMode.Serial,
            HasExpiryTracking: true,
            PickingPolicy.Fefo,
            CostingMethod.Average,
            null);

        var normalized = ProductTraceabilityRules.Normalize(
            ProductType.Product, isStockManaged: true, AllEnabled, state);

        Assert.Equal(PickingPolicy.None, normalized.PickingPolicy);

        var raw = new ProductTraceabilityState(
            TrackingMode.Serial, true, PickingPolicy.Fefo, CostingMethod.Average, null);
        var validation = ProductTraceabilityRules.Validate(
            ProductType.Product, true, AllEnabled, raw);
        Assert.True(validation.IsFailure);
    }

    [Fact]
    public void None_WithFefo_FailsValidation()
    {
        var state = new ProductTraceabilityState(
            TrackingMode.None,
            HasExpiryTracking: false,
            PickingPolicy.Fefo,
            CostingMethod.Average,
            null);

        var validation = ProductTraceabilityRules.Validate(
            ProductType.Product, true, AllEnabled, state);
        Assert.True(validation.IsFailure);
    }

    [Fact]
    public void FifoCosting_WithoutFlag_FailsValidation()
    {
        var state = new ProductTraceabilityState(
            TrackingMode.None,
            HasExpiryTracking: false,
            PickingPolicy.None,
            CostingMethod.Fifo,
            null);

        var normalized = ProductTraceabilityRules.Normalize(
            ProductType.Product, true, AllDisabled, state);
        Assert.Equal(CostingMethod.Average, normalized.CostingMethod);

        var validation = ProductTraceabilityRules.Validate(
            ProductType.Product, true, AllDisabled,
            new ProductTraceabilityState(TrackingMode.None, false, PickingPolicy.None, CostingMethod.Fifo, null));
        Assert.True(validation.IsFailure);
    }

    [Fact]
    public void ManualPicking_NormalizedToNone()
    {
        var state = new ProductTraceabilityState(
            TrackingMode.Lot,
            HasExpiryTracking: false,
            PickingPolicy.Manual,
            CostingMethod.Average,
            null);

        var normalized = ProductTraceabilityRules.Normalize(
            ProductType.Product, true, AllEnabled, state);

        Assert.Equal(PickingPolicy.None, normalized.PickingPolicy);
    }

    [Fact]
    public void NotStockManaged_ResetsToDefaults()
    {
        var state = new ProductTraceabilityState(
            TrackingMode.Lot,
            HasExpiryTracking: true,
            PickingPolicy.Fefo,
            CostingMethod.Fifo,
            15);

        var normalized = ProductTraceabilityRules.Normalize(
            ProductType.Product, isStockManaged: false, AllEnabled, state);

        Assert.Equal(ProductTraceabilityRules.Defaults, normalized);
    }

    [Fact]
    public void NotStockManaged_WithTraceSettings_FailsValidation()
    {
        var state = new ProductTraceabilityState(
            TrackingMode.Lot, true, PickingPolicy.Fefo, CostingMethod.Fifo, 10);

        var validation = ProductTraceabilityRules.Validate(
            ProductType.Product, isStockManaged: false, AllEnabled, state);
        Assert.True(validation.IsFailure);
    }

    [Fact]
    public void FefoWithoutExpiry_NormalizedToFifoPhysical()
    {
        var state = new ProductTraceabilityState(
            TrackingMode.Lot,
            HasExpiryTracking: false,
            PickingPolicy.Fefo,
            CostingMethod.Average,
            null);

        var normalized = ProductTraceabilityRules.Normalize(
            ProductType.Product, true, AllEnabled, state);

        Assert.Equal(PickingPolicy.FifoPhysical, normalized.PickingPolicy);
    }

    [Fact]
    public void Service_WithTracking_FailsValidation()
    {
        var state = new ProductTraceabilityState(
            TrackingMode.Lot, false, PickingPolicy.None, CostingMethod.Average, null);

        var validation = ProductTraceabilityRules.Validate(
            ProductType.Service, true, AllEnabled, state);
        Assert.True(validation.IsFailure);
    }

    [Fact]
    public void Lot_WithoutLotFlag_FailsValidation()
    {
        var flags = AllEnabled with { LotTrackingEnabled = false };
        var state = new ProductTraceabilityState(
            TrackingMode.Lot, false, PickingPolicy.None, CostingMethod.Average, null);

        var validation = ProductTraceabilityRules.Validate(ProductType.Product, true, flags, state);
        Assert.True(validation.IsFailure);
    }

    [Fact]
    public void ExpiryAlertDays_OutOfRange_FailsValidation()
    {
        var state = new ProductTraceabilityState(
            TrackingMode.Lot, true, PickingPolicy.None, CostingMethod.Average, 4000);

        var validation = ProductTraceabilityRules.Validate(ProductType.Product, true, AllEnabled, state);
        Assert.True(validation.IsFailure);
    }
}
