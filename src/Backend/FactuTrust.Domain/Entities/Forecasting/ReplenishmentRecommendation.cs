using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Events.Forecasting;

namespace FactuTrust.Domain.Entities.Forecasting;

/// <summary>
/// A persisted replenishment recommendation for a single product/warehouse pair, produced by the
/// forecasting engine when QuantityOnHand falls at or below the computed reorder point (ROP).
/// The user must explicitly approve, dismiss, or convert the recommendation into a draft purchase order
/// (no automatic mutations on stock or supplier orders).
/// </summary>
public sealed class ReplenishmentRecommendation : Entity
{
    public Guid ProductId { get; private set; }
    public Guid WarehouseId { get; private set; }

    public DateTime GeneratedAt { get; private set; }

    /// <summary>Recommended quantity to reorder (units). Always &gt; 0.</summary>
    public decimal RecommendedQty { get; private set; }

    /// <summary>Reorder point: stock-on-hand level that triggered the recommendation. ROP = d × L + SS.</summary>
    public decimal Rop { get; private set; }

    /// <summary>Safety stock: SS = Z × σ_demand × √L (Z=1.65 for 95% in V1).</summary>
    public decimal SafetyStock { get; private set; }

    /// <summary>Lead time used for the calculation (days).</summary>
    public int LeadTimeDays { get; private set; }

    /// <summary>Forecasted daily demand used in the calculation.</summary>
    public decimal DailyDemand { get; private set; }

    /// <summary>JSON array of reason codes (e.g. ["LowStock", "SeasonalRamadan", "ClassAX"]) for the user.</summary>
    public string ReasonCodesJson { get; private set; } = "[]";

    public ReplenishmentStatus Status { get; private set; }

    public string? DismissedReason { get; private set; }

    /// <summary>Set when the recommendation is converted to a draft purchase order (status=Approved or Ordered).</summary>
    public Guid? LinkedPurchaseOrderId { get; private set; }

    public DateTime? ProcessedAt { get; private set; }
    public string? ProcessedBy { get; private set; }

    // ─────────── Replenishment V2 additive fields (all nullable / defaulted) ───────────
    // Populated only when ReplenishmentService is active. V1 leaves them at default values
    // so that legacy data and the V1 pipeline are unaffected.

    /// <summary>Preferred supplier resolved at generation time (copied from <c>Product.PreferredSupplierId</c>).</summary>
    public Guid? PreferredSupplierId { get; private set; }

    /// <summary>Cached supplier name for display (denormalised — refreshed on each generation).</summary>
    public string? PreferredSupplierName { get; private set; }

    /// <summary>Quantity currently on order via open purchase orders (not yet received). Default 0.</summary>
    public decimal QuantityOnOrder { get; private set; }

    /// <summary>Effective stock used for the ROP test: <c>QuantityOnHand + QuantityOnOrder</c>.</summary>
    public decimal EffectiveQty { get; private set; }

    /// <summary>User-supplied override of <see cref="RecommendedQty"/> — wins over the algorithmic value at PO-prep time.</summary>
    public decimal? ManualQtyOverride { get; private set; }

    /// <summary>User-supplied override of <see cref="PreferredSupplierId"/> — wins over the default at PO-prep time.</summary>
    public Guid? ManualSupplierOverride { get; private set; }

    /// <summary>
    /// Estimated days of stock remaining at current consumption rate (<c>QuantityOnHand / DailyDemand</c>).
    /// Null when <c>DailyDemand &lt;= 0</c> (no consumption history). Cached for fast urgency rendering.
    /// </summary>
    public decimal? DaysOfStockRemaining { get; private set; }

    /// <summary>Free-form collaborative notes the user can attach to a recommendation (max 1000 chars).</summary>
    public string? UserNotes { get; private set; }

    private ReplenishmentRecommendation() { }

    public static ReplenishmentRecommendation Create(
        Guid productId,
        Guid warehouseId,
        decimal recommendedQty,
        decimal rop,
        decimal safetyStock,
        int leadTimeDays,
        decimal dailyDemand,
        string reasonCodesJson)
    {
        if (productId == Guid.Empty)
            throw new ArgumentException("ProductId is required", nameof(productId));
        if (warehouseId == Guid.Empty)
            throw new ArgumentException("WarehouseId is required", nameof(warehouseId));
        if (recommendedQty <= 0)
            throw new ArgumentException("RecommendedQty must be > 0", nameof(recommendedQty));
        if (leadTimeDays < 0)
            throw new ArgumentException("LeadTimeDays must be ≥ 0", nameof(leadTimeDays));
        // Defensive — the deterministic engine never produces negatives, but the contract should
        // not rely on that. These four invariants keep the domain self-consistent.
        if (rop < 0)
            throw new ArgumentException("Rop must be ≥ 0", nameof(rop));
        if (safetyStock < 0)
            throw new ArgumentException("SafetyStock must be ≥ 0", nameof(safetyStock));
        if (dailyDemand < 0)
            throw new ArgumentException("DailyDemand must be ≥ 0", nameof(dailyDemand));

        return new ReplenishmentRecommendation
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            GeneratedAt = DateTime.UtcNow,
            RecommendedQty = Math.Round(recommendedQty, 3),
            Rop = Math.Round(rop, 3),
            SafetyStock = Math.Round(safetyStock, 3),
            LeadTimeDays = leadTimeDays,
            DailyDemand = Math.Round(dailyDemand, 3),
            ReasonCodesJson = string.IsNullOrWhiteSpace(reasonCodesJson) ? "[]" : reasonCodesJson,
            Status = ReplenishmentStatus.Pending
        };
    }

    /// <summary>
    /// Approves the recommendation. Idempotent — re-approving an already-Approved recommendation is a no-op
    /// (fix F-M1: double-click no longer raises). Terminal states (Ordered/Dismissed/Superseded) still raise.
    /// </summary>
    public void Approve(string userId)
    {
        if (Status == ReplenishmentStatus.Approved)
            return; // idempotent — double-click safe
        if (Status != ReplenishmentStatus.Pending)
            throw new InvalidOperationException($"Cannot approve a recommendation in status {Status}");

        var from = Status;
        Status = ReplenishmentStatus.Approved;
        ProcessedAt = DateTime.UtcNow;
        ProcessedBy = userId;
        AddDomainEvent(new ReplenishmentApprovedEvent(Id, ProductId, WarehouseId, from, userId));
    }

    /// <summary>
    /// Dismisses the recommendation. Allowed from Pending and Approved (fix F-M2 — lets users undo
    /// an accidental Approve while still pending PO creation). Ordered/Superseded remain terminal.
    /// </summary>
    public void Dismiss(string userId, string? reason)
    {
        if (Status == ReplenishmentStatus.Dismissed)
            return; // idempotent
        if (Status is ReplenishmentStatus.Ordered or ReplenishmentStatus.Superseded)
            throw new InvalidOperationException($"Cannot dismiss a recommendation in status {Status}");

        var from = Status;
        Status = ReplenishmentStatus.Dismissed;
        DismissedReason = reason?.Trim();
        ProcessedAt = DateTime.UtcNow;
        ProcessedBy = userId;
        AddDomainEvent(new ReplenishmentDismissedEvent(Id, ProductId, WarehouseId, from, userId, DismissedReason));
    }

    /// <summary>
    /// Links this recommendation to a freshly-created purchase order and transitions to <see cref="ReplenishmentStatus.Ordered"/>.
    /// This method is the missing piece in V1 (fix F-C1): <see cref="LinkedPurchaseOrderId"/> was never set,
    /// so the "Ordered" filter was always empty.
    /// </summary>
    /// <remarks>
    /// Idempotent when re-linking the **same** PO (e.g. retry after a partial failure).
    /// Re-linking to a **different** PO is rejected — that would silently switch the user's
    /// audit trail and is almost always a bug in the caller; the caller must surface an
    /// explicit "supersede" decision instead.
    /// </remarks>
    public void LinkToPurchaseOrder(Guid purchaseOrderId, string userId)
    {
        if (purchaseOrderId == Guid.Empty)
            throw new ArgumentException("PurchaseOrderId is required", nameof(purchaseOrderId));
        if (Status == ReplenishmentStatus.Ordered)
        {
            if (LinkedPurchaseOrderId == purchaseOrderId)
                return; // idempotent — same PO re-linked
            throw new InvalidOperationException(
                $"Recommendation is already linked to a different purchase order ({LinkedPurchaseOrderId}); refusing to overwrite.");
        }

        var from = Status;
        LinkedPurchaseOrderId = purchaseOrderId;
        Status = ReplenishmentStatus.Ordered;
        ProcessedAt = DateTime.UtcNow;
        ProcessedBy = userId;
        AddDomainEvent(new ReplenishmentLinkedToPoEvent(Id, ProductId, WarehouseId, purchaseOrderId, from, userId));
    }

    /// <summary>
    /// Marks an outdated Pending recommendation as Superseded. Called by the generation pipeline
    /// before inserting fresh recommendations so users don't see two competing rows for the same
    /// (product, warehouse) pair. No-op when the recommendation is no longer Pending — terminal
    /// states (Approved / Dismissed / Ordered) are preserved untouched.
    /// </summary>
    public void MarkSuperseded()
    {
        if (Status == ReplenishmentStatus.Pending)
            Status = ReplenishmentStatus.Superseded;
    }

    // ─────────── Replenishment V2 mutators ───────────

    /// <summary>
    /// Populates the V2 enrichment fields (supplier hint, on-order qty, effective qty, days-of-stock).
    /// Called by the generation pipeline immediately after the recommendation is created.
    /// All arguments are validated; <see cref="EffectiveQty"/> is derived.
    /// </summary>
    /// <remarks>
    /// Restricted to <see cref="ReplenishmentStatus.Pending"/> (defense-in-depth — a stale call from
    /// a future bug could otherwise overwrite the supplier hint or days-of-stock on a recommendation
    /// that has already been approved / ordered).
    /// </remarks>
    public void SetV2Enrichment(
        Guid? preferredSupplierId,
        string? preferredSupplierName,
        decimal quantityOnHand,
        decimal quantityOnOrder,
        decimal? daysOfStockRemaining)
    {
        if (Status != ReplenishmentStatus.Pending)
            throw new InvalidOperationException($"Cannot enrich V2 fields on a recommendation in status {Status}");
        if (preferredSupplierId.HasValue && preferredSupplierId.Value == Guid.Empty)
            throw new ArgumentException("PreferredSupplierId must be a valid GUID or null", nameof(preferredSupplierId));
        if (quantityOnOrder < 0)
            throw new ArgumentException("QuantityOnOrder must be ≥ 0", nameof(quantityOnOrder));
        if (quantityOnHand < 0)
            throw new ArgumentException("QuantityOnHand must be ≥ 0", nameof(quantityOnHand));
        if (daysOfStockRemaining is < 0)
            throw new ArgumentException("DaysOfStockRemaining must be ≥ 0 or null", nameof(daysOfStockRemaining));
        if (!string.IsNullOrEmpty(preferredSupplierName) && preferredSupplierName.Length > 200)
            throw new ArgumentException("PreferredSupplierName must be ≤ 200 chars", nameof(preferredSupplierName));

        PreferredSupplierId = preferredSupplierId;
        PreferredSupplierName = preferredSupplierName?.Trim();
        QuantityOnOrder = Math.Round(quantityOnOrder, 3);
        EffectiveQty = Math.Round(quantityOnHand + quantityOnOrder, 3);
        DaysOfStockRemaining = daysOfStockRemaining.HasValue
            ? Math.Round(daysOfStockRemaining.Value, 2)
            : null;
    }

    /// <summary>
    /// Applies a user override on quantity and/or supplier before PO preparation.
    /// Either argument can be null to leave it untouched; passing both as null clears overrides.
    /// </summary>
    public void ApplyManualOverride(decimal? manualQty, Guid? manualSupplierId, string userId)
    {
        if (Status is ReplenishmentStatus.Ordered or ReplenishmentStatus.Superseded or ReplenishmentStatus.Dismissed)
            throw new InvalidOperationException($"Cannot override a recommendation in status {Status}");
        if (manualQty is <= 0)
            throw new ArgumentException("ManualQty must be > 0 (or null to clear)", nameof(manualQty));
        if (manualSupplierId.HasValue && manualSupplierId.Value == Guid.Empty)
            throw new ArgumentException("ManualSupplierId must be a valid GUID or null", nameof(manualSupplierId));

        ManualQtyOverride = manualQty.HasValue ? Math.Round(manualQty.Value, 3) : null;
        ManualSupplierOverride = manualSupplierId;
        ProcessedAt = DateTime.UtcNow;
        ProcessedBy = userId;
    }

    /// <summary>
    /// Attaches or updates the user notes. Pass null/empty to clear.
    /// </summary>
    public void AttachNotes(string? notes, string userId)
    {
        if (!string.IsNullOrEmpty(notes) && notes.Length > 1000)
            throw new ArgumentException("UserNotes must be ≤ 1000 chars", nameof(notes));
        UserNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        ProcessedAt = DateTime.UtcNow;
        ProcessedBy = userId;
    }

    /// <summary>
    /// Reverts the recommendation to <see cref="ReplenishmentStatus.Pending"/> after a non-terminal decision.
    /// Used by the "Undo" feature when the user wants to retract an Approve/Dismiss within the audit window.
    /// </summary>
    public void RevertToPending(string userId)
    {
        if (Status is ReplenishmentStatus.Ordered or ReplenishmentStatus.Superseded)
            throw new InvalidOperationException($"Cannot revert a recommendation in status {Status}");
        if (Status == ReplenishmentStatus.Pending)
            return; // idempotent

        Status = ReplenishmentStatus.Pending;
        DismissedReason = null;
        ProcessedAt = DateTime.UtcNow;
        ProcessedBy = userId;
    }

    /// <summary>
    /// Returns the effective quantity that should be sent to the PO line.
    /// Priority: <c>ManualQtyOverride</c> &gt; <c>RecommendedQty</c>.
    /// </summary>
    public decimal GetEffectiveOrderQty() => ManualQtyOverride ?? RecommendedQty;

    /// <summary>
    /// Returns the effective supplier id that should be used to create the PO.
    /// Priority: <c>ManualSupplierOverride</c> &gt; <c>PreferredSupplierId</c>.
    /// Returns null when no supplier is available — caller must surface the error.
    /// </summary>
    public Guid? GetEffectiveSupplierId() => ManualSupplierOverride ?? PreferredSupplierId;
}
