using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Services;

/// <summary>Tenant feature flags required to validate product traceability settings.</summary>
public sealed record ProductTraceabilityFeatureFlags(
    bool LotTrackingEnabled,
    bool SerialTrackingEnabled,
    bool ExpiryTrackingEnabled,
    bool FifoLifoValuationEnabled);

/// <summary>Normalized traceability snapshot for a product form or API payload.</summary>
public sealed record ProductTraceabilityState(
    TrackingMode TrackingMode,
    bool HasExpiryTracking,
    PickingPolicy PickingPolicy,
    CostingMethod CostingMethod,
    int? ExpiryAlertDays);

/// <summary>Cross-field validation and normalization for product traceability.</summary>
public static class ProductTraceabilityRules
{
    public static ProductTraceabilityState Defaults { get; } = new(
        TrackingMode.None,
        HasExpiryTracking: false,
        PickingPolicy.None,
        CostingMethod.Average,
        ExpiryAlertDays: null);

    /// <summary>
    /// Normalizes traceability fields in-place semantics (returns new state).
    /// Does not fail on invalid combinations — use <see cref="Validate"/> after normalization.
    /// </summary>
    public static ProductTraceabilityState Normalize(
        ProductType type,
        bool isStockManaged,
        ProductTraceabilityFeatureFlags features,
        ProductTraceabilityState state)
    {
        if (type is ProductType.Service or ProductType.Subscription || !isStockManaged)
            return Defaults;

        var mode = state.TrackingMode;
        var hasExpiry = state.HasExpiryTracking;
        var picking = state.PickingPolicy;
        var costing = state.CostingMethod;
        var expiryAlertDays = state.ExpiryAlertDays;

        if (picking == PickingPolicy.Manual)
            picking = PickingPolicy.None;

        if (!features.LotTrackingEnabled && mode == TrackingMode.Lot)
            mode = TrackingMode.None;

        if (!features.SerialTrackingEnabled && mode == TrackingMode.Serial)
            mode = TrackingMode.None;

        if (mode == TrackingMode.None)
        {
            picking = PickingPolicy.None;
            hasExpiry = false;
            expiryAlertDays = null;
        }
        else if (mode == TrackingMode.Serial)
        {
            picking = PickingPolicy.None;
        }

        if (!features.ExpiryTrackingEnabled)
        {
            hasExpiry = false;
            expiryAlertDays = null;
        }

        if (mode != TrackingMode.Lot)
            picking = PickingPolicy.None;

        if (picking == PickingPolicy.Fefo && !hasExpiry)
            picking = PickingPolicy.FifoPhysical;

        if (!features.FifoLifoValuationEnabled && costing is CostingMethod.Fifo or CostingMethod.Lifo)
            costing = CostingMethod.Average;

        return new ProductTraceabilityState(mode, hasExpiry, picking, costing, expiryAlertDays);
    }

    /// <summary>Validates normalized state; returns failure for API-rejected combinations.</summary>
    public static Result Validate(
        ProductType type,
        bool isStockManaged,
        ProductTraceabilityFeatureFlags features,
        ProductTraceabilityState state)
    {
        if (type is ProductType.Service or ProductType.Subscription)
        {
            if (state.TrackingMode != TrackingMode.None
                || state.HasExpiryTracking
                || state.PickingPolicy != PickingPolicy.None
                || state.CostingMethod is CostingMethod.Fifo or CostingMethod.Lifo
                || state.ExpiryAlertDays.HasValue)
            {
                return Result.Failure(Error.Validation("TrackingMode",
                    "La traçabilité n'est pas applicable aux services et abonnements."));
            }

            return Result.Success();
        }

        if (!isStockManaged)
        {
            if (!IsDefaultState(state))
            {
                return Result.Failure(Error.Validation("TrackingMode",
                    "La traçabilité exige que la gestion de stock soit activée."));
            }

            return Result.Success();
        }

        if (state.TrackingMode == TrackingMode.Lot && !features.LotTrackingEnabled)
        {
            return Result.Failure(Error.Validation("TrackingMode",
                "Le suivi par lot n'est pas activé pour cette entreprise."));
        }

        if (state.TrackingMode == TrackingMode.Serial && !features.SerialTrackingEnabled)
        {
            return Result.Failure(Error.Validation("TrackingMode",
                "Le suivi par numéro de série n'est pas activé pour cette entreprise."));
        }

        if (state.HasExpiryTracking && !features.ExpiryTrackingEnabled)
        {
            return Result.Failure(Error.Validation("HasExpiryTracking",
                "Le suivi de péremption (DLUO) n'est pas activé pour cette entreprise."));
        }

        if (state.HasExpiryTracking && state.TrackingMode == TrackingMode.None)
        {
            return Result.Failure(Error.Validation("HasExpiryTracking",
                "Le suivi de péremption exige un suivi par lot ou par numéro de série."));
        }

        if (state.ExpiryAlertDays is < 0 or > 3650)
        {
            return Result.Failure(Error.Validation("ExpiryAlertDays",
                "L'alerte de péremption doit être entre 0 et 3650 jours."));
        }

        if (state.TrackingMode != TrackingMode.Lot && state.PickingPolicy != PickingPolicy.None)
        {
            return Result.Failure(Error.Validation("PickingPolicy",
                "La politique de prélèvement ne s'applique qu'aux articles suivis par lot."));
        }

        if (state.PickingPolicy == PickingPolicy.Fefo && !state.HasExpiryTracking)
        {
            return Result.Failure(Error.Validation("PickingPolicy",
                "FEFO exige le suivi de péremption (DLUO) activé."));
        }

        if (state.CostingMethod is CostingMethod.Fifo or CostingMethod.Lifo && !features.FifoLifoValuationEnabled)
        {
            return Result.Failure(Error.Validation("CostingMethod",
                "La valorisation FIFO/LIFO n'est pas activée pour cette entreprise."));
        }

        if (!Enum.IsDefined(typeof(PickingPolicy), state.PickingPolicy))
        {
            return Result.Failure(Error.Validation("PickingPolicy",
                "Politique de prélèvement invalide."));
        }

        return Result.Success();
    }

    public static Result<ProductTraceabilityState> ValidateAndNormalize(
        ProductType type,
        bool isStockManaged,
        ProductTraceabilityFeatureFlags features,
        ProductTraceabilityState state)
    {
        var normalized = Normalize(type, isStockManaged, features, state);
        var validation = Validate(type, isStockManaged, features, normalized);
        return validation.IsFailure
            ? Result.Failure<ProductTraceabilityState>(validation.Error)
            : Result.Success(normalized);
    }

    private static bool IsDefaultState(ProductTraceabilityState state) =>
        state.TrackingMode == TrackingMode.None
        && !state.HasExpiryTracking
        && state.PickingPolicy == PickingPolicy.None
        && state.CostingMethod == CostingMethod.Average
        && state.ExpiryAlertDays is null;
}
