using FactuTrust.Domain.Entities.Forecasting;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Events.Forecasting;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Forecasting;

/// <summary>
/// Unit tests covering the V2 enhancements of <see cref="ReplenishmentRecommendation"/>:
///   • Idempotent <c>Approve</c> / <c>Dismiss</c> (fix F-M1).
///   • Dismiss now accepts Approved status (fix F-M2).
///   • <c>LinkToPurchaseOrder</c> emits <see cref="ReplenishmentLinkedToPoEvent"/> (fix F-C1 root cause).
///   • <c>SetV2Enrichment</c>, <c>ApplyManualOverride</c>, <c>AttachNotes</c>, <c>RevertToPending</c>.
/// </summary>
public sealed class ReplenishmentRecommendationTests
{
    private static ReplenishmentRecommendation NewRec() =>
        ReplenishmentRecommendation.Create(
            productId: Guid.NewGuid(),
            warehouseId: Guid.NewGuid(),
            recommendedQty: 100m,
            rop: 50m,
            safetyStock: 20m,
            leadTimeDays: 7,
            dailyDemand: 5m,
            reasonCodesJson: "[\"BelowSafetyStock\"]");

    // ─────────── Create() — defensive validations (Phase 1 review M5) ───────────

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(0, -1, 0)]
    [InlineData(0, 0, -1)]
    public void Create_WithNegativeRopOrSafetyStockOrDailyDemand_Throws(decimal rop, decimal ss, decimal dd)
    {
        Assert.Throws<ArgumentException>(() =>
            ReplenishmentRecommendation.Create(
                productId: Guid.NewGuid(),
                warehouseId: Guid.NewGuid(),
                recommendedQty: 10m,
                rop: rop,
                safetyStock: ss,
                leadTimeDays: 7,
                dailyDemand: dd,
                reasonCodesJson: "[]"));
    }

    [Fact]
    public void Create_WithAllZeroNonNegativeFields_Succeeds()
    {
        var rec = ReplenishmentRecommendation.Create(
            productId: Guid.NewGuid(),
            warehouseId: Guid.NewGuid(),
            recommendedQty: 1m, // strictly > 0
            rop: 0m, safetyStock: 0m, leadTimeDays: 0, dailyDemand: 0m,
            reasonCodesJson: "[]");

        Assert.Equal(0m, rec.Rop);
        Assert.Equal(0m, rec.SafetyStock);
        Assert.Equal(0m, rec.DailyDemand);
    }

    // ─────────── Defaults ───────────

    [Fact]
    public void NewRecommendation_V2Fields_HaveSafeDefaults()
    {
        var rec = NewRec();

        Assert.Null(rec.PreferredSupplierId);
        Assert.Null(rec.PreferredSupplierName);
        Assert.Equal(0m, rec.QuantityOnOrder);
        Assert.Equal(0m, rec.EffectiveQty);
        Assert.Null(rec.ManualQtyOverride);
        Assert.Null(rec.ManualSupplierOverride);
        Assert.Null(rec.DaysOfStockRemaining);
        Assert.Null(rec.UserNotes);
        Assert.Equal(ReplenishmentStatus.Pending, rec.Status);
    }

    // ─────────── Approve idempotence (fix F-M1) ───────────

    [Fact]
    public void Approve_FromPending_TransitionsToApproved_AndRaisesEvent()
    {
        var rec = NewRec();

        rec.Approve("user-1");

        Assert.Equal(ReplenishmentStatus.Approved, rec.Status);
        Assert.Equal("user-1", rec.ProcessedBy);
        Assert.NotNull(rec.ProcessedAt);
        Assert.Single(rec.DomainEvents);
        Assert.IsType<ReplenishmentApprovedEvent>(rec.DomainEvents.First());
    }

    [Fact]
    public void Approve_WhenAlreadyApproved_IsIdempotent_NoExceptionNoNewEvent()
    {
        var rec = NewRec();
        rec.Approve("user-1");
        rec.ClearDomainEvents();

        rec.Approve("user-2"); // second call — must be no-op (fix F-M1)

        Assert.Equal(ReplenishmentStatus.Approved, rec.Status);
        Assert.Equal("user-1", rec.ProcessedBy); // unchanged
        Assert.Empty(rec.DomainEvents);
    }

    [Fact]
    public void Approve_FromOrdered_Throws()
    {
        var rec = NewRec();
        rec.LinkToPurchaseOrder(Guid.NewGuid(), "user-1");
        Assert.Throws<InvalidOperationException>(() => rec.Approve("user-2"));
    }

    [Fact]
    public void Approve_FromDismissed_Throws()
    {
        var rec = NewRec();
        rec.Dismiss("user-1", "obsolete");
        Assert.Throws<InvalidOperationException>(() => rec.Approve("user-2"));
    }

    // ─────────── Dismiss now allows Approved → Dismissed (fix F-M2) ───────────

    [Fact]
    public void Dismiss_FromApproved_NowAllowed_TransitionsAndRaisesEvent()
    {
        var rec = NewRec();
        rec.Approve("user-1");
        rec.ClearDomainEvents();

        rec.Dismiss("user-2", "budget cancelled");

        Assert.Equal(ReplenishmentStatus.Dismissed, rec.Status);
        Assert.Equal("budget cancelled", rec.DismissedReason);
        Assert.Single(rec.DomainEvents);
        Assert.IsType<ReplenishmentDismissedEvent>(rec.DomainEvents.First());
    }

    [Fact]
    public void Dismiss_FromApproved_EmittedEventCarriesPreviousStatus_AsApproved()
    {
        // Phase 1 review M3: pin the contract — the event's FromStatus must reflect Approved,
        // not Pending, so downstream consumers (audit log, KPI refresh) can reason on the actual
        // transition path. This guards against accidentally re-introducing the V1 strict check.
        var rec = NewRec();
        rec.Approve("user-1");
        rec.ClearDomainEvents();

        rec.Dismiss("user-2", "budget cancelled");

        var evt = Assert.IsType<ReplenishmentDismissedEvent>(rec.DomainEvents.Single());
        Assert.Equal(ReplenishmentStatus.Approved, evt.FromStatus);
        Assert.Equal("user-2", evt.ActorUserId);
        Assert.Equal("budget cancelled", evt.Reason);
    }

    [Fact]
    public void Dismiss_FromOrdered_Throws()
    {
        var rec = NewRec();
        rec.LinkToPurchaseOrder(Guid.NewGuid(), "user-1");
        Assert.Throws<InvalidOperationException>(() => rec.Dismiss("user-2", "any"));
    }

    [Fact]
    public void Dismiss_IsIdempotent()
    {
        var rec = NewRec();
        rec.Dismiss("user-1", "reason");
        rec.ClearDomainEvents();

        rec.Dismiss("user-2", "different");

        Assert.Equal(ReplenishmentStatus.Dismissed, rec.Status);
        Assert.Equal("reason", rec.DismissedReason); // first reason kept
        Assert.Empty(rec.DomainEvents);
    }

    // ─────────── LinkToPurchaseOrder emits event (fix F-C1 root cause) ───────────

    [Fact]
    public void LinkToPurchaseOrder_SetsLinkedIdAndTransitionsToOrdered()
    {
        var rec = NewRec();
        var poId = Guid.NewGuid();

        rec.LinkToPurchaseOrder(poId, "user-1");

        Assert.Equal(poId, rec.LinkedPurchaseOrderId);
        Assert.Equal(ReplenishmentStatus.Ordered, rec.Status);
        Assert.NotNull(rec.ProcessedAt);
        Assert.Single(rec.DomainEvents);
        var evt = Assert.IsType<ReplenishmentLinkedToPoEvent>(rec.DomainEvents.First());
        Assert.Equal(poId, evt.PurchaseOrderId);
    }

    [Fact]
    public void LinkToPurchaseOrder_WithEmptyGuid_Throws()
    {
        var rec = NewRec();
        Assert.Throws<ArgumentException>(() => rec.LinkToPurchaseOrder(Guid.Empty, "user-1"));
    }

    [Fact]
    public void LinkToPurchaseOrder_SamePoTwice_IsIdempotent()
    {
        var rec = NewRec();
        var poId = Guid.NewGuid();
        rec.LinkToPurchaseOrder(poId, "user-1");
        rec.ClearDomainEvents();

        rec.LinkToPurchaseOrder(poId, "user-2"); // same PO — no-op

        Assert.Equal(poId, rec.LinkedPurchaseOrderId);
        Assert.Empty(rec.DomainEvents);
    }

    [Fact]
    public void LinkToPurchaseOrder_DifferentPoOnOrderedRec_Throws()
    {
        // Phase 1 review M4: linking to a different PO would silently rewrite the audit trail.
        // The domain refuses; caller must explicitly supersede first.
        var rec = NewRec();
        var firstPo = Guid.NewGuid();
        var secondPo = Guid.NewGuid();
        rec.LinkToPurchaseOrder(firstPo, "user-1");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            rec.LinkToPurchaseOrder(secondPo, "user-2"));

        Assert.Contains("already linked", ex.Message);
        Assert.Equal(firstPo, rec.LinkedPurchaseOrderId);   // original link preserved
    }

    // ─────────── SetV2Enrichment ───────────

    [Fact]
    public void SetV2Enrichment_WithValidValues_PopulatesAllFields()
    {
        var rec = NewRec();
        var supplierId = Guid.NewGuid();

        rec.SetV2Enrichment(
            preferredSupplierId: supplierId,
            preferredSupplierName: "Fournisseur X",
            quantityOnHand: 30m,
            quantityOnOrder: 20m,
            daysOfStockRemaining: 6m);

        Assert.Equal(supplierId, rec.PreferredSupplierId);
        Assert.Equal("Fournisseur X", rec.PreferredSupplierName);
        Assert.Equal(20m, rec.QuantityOnOrder);
        Assert.Equal(50m, rec.EffectiveQty); // 30 + 20
        Assert.Equal(6m, rec.DaysOfStockRemaining);
    }

    [Fact]
    public void SetV2Enrichment_WithNullSupplier_AllowsClear()
    {
        var rec = NewRec();
        rec.SetV2Enrichment(null, null, 30m, 0m, null);

        Assert.Null(rec.PreferredSupplierId);
        Assert.Null(rec.PreferredSupplierName);
        Assert.Equal(30m, rec.EffectiveQty);
        Assert.Null(rec.DaysOfStockRemaining);
    }

    [Fact]
    public void SetV2Enrichment_WithNegativeOnOrder_Throws()
    {
        var rec = NewRec();
        Assert.Throws<ArgumentException>(() =>
            rec.SetV2Enrichment(null, null, 30m, -1m, null));
    }

    [Fact]
    public void SetV2Enrichment_WithEmptySupplierId_Throws()
    {
        var rec = NewRec();
        Assert.Throws<ArgumentException>(() =>
            rec.SetV2Enrichment(Guid.Empty, "name", 30m, 0m, null));
    }

    [Theory]
    [InlineData(ReplenishmentStatus.Approved)]
    [InlineData(ReplenishmentStatus.Dismissed)]
    [InlineData(ReplenishmentStatus.Ordered)]
    [InlineData(ReplenishmentStatus.Superseded)]
    public void SetV2Enrichment_OnNonPending_Throws(ReplenishmentStatus terminal)
    {
        // Phase 1 review M1: defense-in-depth. A stale call from a future bug would otherwise
        // overwrite supplier hints / days-of-stock on an already-decided recommendation.
        var rec = NewRec();
        // Move the recommendation into the requested terminal state.
        switch (terminal)
        {
            case ReplenishmentStatus.Approved:    rec.Approve("u"); break;
            case ReplenishmentStatus.Dismissed:   rec.Dismiss("u", "reason"); break;
            case ReplenishmentStatus.Ordered:     rec.LinkToPurchaseOrder(Guid.NewGuid(), "u"); break;
            case ReplenishmentStatus.Superseded:  rec.MarkSuperseded(); break;
        }

        Assert.Throws<InvalidOperationException>(() =>
            rec.SetV2Enrichment(null, null, 10m, 0m, null));
    }

    // ─────────── ApplyManualOverride ───────────

    [Fact]
    public void ApplyManualOverride_OnPending_StoresValues()
    {
        var rec = NewRec();
        var supplierId = Guid.NewGuid();

        rec.ApplyManualOverride(150m, supplierId, "user-1");

        Assert.Equal(150m, rec.ManualQtyOverride);
        Assert.Equal(supplierId, rec.ManualSupplierOverride);
        Assert.Equal("user-1", rec.ProcessedBy);
    }

    [Fact]
    public void ApplyManualOverride_WithBothNull_ClearsOverrides()
    {
        var rec = NewRec();
        rec.ApplyManualOverride(150m, Guid.NewGuid(), "user-1");

        rec.ApplyManualOverride(null, null, "user-1");

        Assert.Null(rec.ManualQtyOverride);
        Assert.Null(rec.ManualSupplierOverride);
    }

    [Fact]
    public void ApplyManualOverride_OnOrdered_Throws()
    {
        var rec = NewRec();
        rec.LinkToPurchaseOrder(Guid.NewGuid(), "user-1");
        Assert.Throws<InvalidOperationException>(() =>
            rec.ApplyManualOverride(150m, null, "user-1"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ApplyManualOverride_WithNonPositiveQty_Throws(decimal qty)
    {
        var rec = NewRec();
        Assert.Throws<ArgumentException>(() =>
            rec.ApplyManualOverride(qty, null, "user-1"));
    }

    // ─────────── AttachNotes ───────────

    [Fact]
    public void AttachNotes_WithValidString_Stores()
    {
        var rec = NewRec();
        rec.AttachNotes("Urgent — supplier confirmed delivery this week.", "user-1");
        Assert.Equal("Urgent — supplier confirmed delivery this week.", rec.UserNotes);
    }

    [Fact]
    public void AttachNotes_WithEmptyOrWhitespace_ClearsNotes()
    {
        var rec = NewRec();
        rec.AttachNotes("first", "user-1");

        rec.AttachNotes("   ", "user-1");

        Assert.Null(rec.UserNotes);
    }

    [Fact]
    public void AttachNotes_Over1000Chars_Throws()
    {
        var rec = NewRec();
        var huge = new string('x', 1001);
        Assert.Throws<ArgumentException>(() => rec.AttachNotes(huge, "user-1"));
    }

    // ─────────── RevertToPending ───────────

    [Fact]
    public void RevertToPending_FromApproved_GoesBackToPending()
    {
        var rec = NewRec();
        rec.Approve("user-1");

        rec.RevertToPending("user-2");

        Assert.Equal(ReplenishmentStatus.Pending, rec.Status);
    }

    [Fact]
    public void RevertToPending_FromDismissed_ClearsReasonAndGoesBack()
    {
        var rec = NewRec();
        rec.Dismiss("user-1", "wrong product");

        rec.RevertToPending("user-2");

        Assert.Equal(ReplenishmentStatus.Pending, rec.Status);
        Assert.Null(rec.DismissedReason);
    }

    [Fact]
    public void RevertToPending_FromOrdered_Throws()
    {
        var rec = NewRec();
        rec.LinkToPurchaseOrder(Guid.NewGuid(), "user-1");
        Assert.Throws<InvalidOperationException>(() => rec.RevertToPending("user-2"));
    }

    [Fact]
    public void RevertToPending_FromPending_IsNoOp()
    {
        var rec = NewRec();
        rec.RevertToPending("user-1");
        Assert.Equal(ReplenishmentStatus.Pending, rec.Status);
    }

    // ─────────── GetEffectiveOrderQty / GetEffectiveSupplierId ───────────

    [Fact]
    public void GetEffectiveOrderQty_PrefersManualOverride()
    {
        var rec = NewRec();
        rec.ApplyManualOverride(250m, null, "user-1");
        Assert.Equal(250m, rec.GetEffectiveOrderQty());
    }

    [Fact]
    public void GetEffectiveOrderQty_FallsBackToRecommendedQty()
    {
        var rec = NewRec();
        Assert.Equal(100m, rec.GetEffectiveOrderQty());
    }

    [Fact]
    public void GetEffectiveSupplierId_PrefersManualOverride()
    {
        var rec = NewRec();
        var preferred = Guid.NewGuid();
        var manual = Guid.NewGuid();
        rec.SetV2Enrichment(preferred, "x", 0, 0, null);
        rec.ApplyManualOverride(null, manual, "user-1");

        Assert.Equal(manual, rec.GetEffectiveSupplierId());
    }

    [Fact]
    public void GetEffectiveSupplierId_FallsBackToPreferred()
    {
        var rec = NewRec();
        var preferred = Guid.NewGuid();
        rec.SetV2Enrichment(preferred, "x", 0, 0, null);

        Assert.Equal(preferred, rec.GetEffectiveSupplierId());
    }

    [Fact]
    public void GetEffectiveSupplierId_NoSupplier_ReturnsNull()
    {
        var rec = NewRec();
        Assert.Null(rec.GetEffectiveSupplierId());
    }
}
