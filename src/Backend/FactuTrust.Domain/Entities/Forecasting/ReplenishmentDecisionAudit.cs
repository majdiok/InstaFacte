using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Forecasting;

/// <summary>
/// Immutable audit trail of every state-changing action applied to a <see cref="ReplenishmentRecommendation"/>.
/// One row per decision (Approve / Dismiss / Override / LinkToPurchaseOrder / Revert / AttachNotes).
/// Lives in the tenant database alongside the recommendation it refers to.
/// </summary>
/// <remarks>
/// V2 module (gated by <c>Features:Forecasting:ReplenishmentV2:Enabled</c>) writes one entry on every action,
/// making the "decision history" feature self-contained (no dependency on the global AuditLog table).
/// V1 does not write here — its audit story remains unchanged.
/// </remarks>
public sealed class ReplenishmentDecisionAudit : Entity
{
    /// <summary>FK to the recommendation this audit row belongs to.</summary>
    public Guid RecommendationId { get; private set; }

    /// <summary>Status before the action was applied.</summary>
    public ReplenishmentStatus FromStatus { get; private set; }

    /// <summary>Status after the action was applied (equal to FromStatus when the action did not change status, e.g. AttachNotes).</summary>
    public ReplenishmentStatus ToStatus { get; private set; }

    /// <summary>
    /// Action label: "Approve" | "Dismiss" | "Override" | "LinkPO" | "Revert" | "AttachNotes" | "Generate".
    /// Stored as a short string for readability and forward-compatibility.
    /// </summary>
    public string ActionType { get; private set; } = string.Empty;

    /// <summary>Optional reason — typed for Dismiss, free-form for other actions.</summary>
    public string? Reason { get; private set; }

    /// <summary>UserId of the actor (UI user or "system" for background jobs).</summary>
    public string ActorUserId { get; private set; } = string.Empty;

    /// <summary>Timestamp of the action (UTC).</summary>
    public DateTime ActedAt { get; private set; }

    /// <summary>
    /// Optional JSON payload describing the change in detail
    /// (e.g. <c>{"oldQty":50,"newQty":150,"oldSupplier":null,"newSupplier":"abc-…"}</c>).
    /// Max 4000 chars.
    /// </summary>
    public string? PayloadJson { get; private set; }

    private ReplenishmentDecisionAudit() { }

    public static ReplenishmentDecisionAudit Create(
        Guid recommendationId,
        ReplenishmentStatus fromStatus,
        ReplenishmentStatus toStatus,
        string actionType,
        string actorUserId,
        string? reason = null,
        string? payloadJson = null)
    {
        if (recommendationId == Guid.Empty)
            throw new ArgumentException("RecommendationId is required", nameof(recommendationId));
        if (string.IsNullOrWhiteSpace(actionType))
            throw new ArgumentException("ActionType is required", nameof(actionType));
        if (actionType.Length > 32)
            throw new ArgumentException("ActionType must be ≤ 32 chars", nameof(actionType));
        if (string.IsNullOrWhiteSpace(actorUserId))
            throw new ArgumentException("ActorUserId is required", nameof(actorUserId));
        if (actorUserId.Length > 450)
            throw new ArgumentException("ActorUserId must be ≤ 450 chars", nameof(actorUserId));
        if (reason is { Length: > 500 })
            throw new ArgumentException("Reason must be ≤ 500 chars", nameof(reason));
        if (payloadJson is { Length: > 4000 })
            throw new ArgumentException("PayloadJson must be ≤ 4000 chars", nameof(payloadJson));

        return new ReplenishmentDecisionAudit
        {
            RecommendationId = recommendationId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            ActionType = actionType.Trim(),
            Reason = reason?.Trim(),
            ActorUserId = actorUserId.Trim(),
            ActedAt = DateTime.UtcNow,
            PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? null : payloadJson
        };
    }
}
