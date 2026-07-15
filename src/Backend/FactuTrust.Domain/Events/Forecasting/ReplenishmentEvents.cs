using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Events.Forecasting;

/// <summary>
/// Raised when a replenishment recommendation is approved (state transition Pending → Approved).
/// Consumers may persist the audit row, notify the buyer, or refresh KPI tiles.
/// </summary>
public sealed class ReplenishmentApprovedEvent : DomainEvent
{
    public Guid RecommendationId { get; }
    public Guid ProductId { get; }
    public Guid WarehouseId { get; }
    public ReplenishmentStatus FromStatus { get; }
    public string ActorUserId { get; }

    public ReplenishmentApprovedEvent(
        Guid recommendationId,
        Guid productId,
        Guid warehouseId,
        ReplenishmentStatus fromStatus,
        string actorUserId)
    {
        RecommendationId = recommendationId;
        ProductId = productId;
        WarehouseId = warehouseId;
        FromStatus = fromStatus;
        ActorUserId = actorUserId;
    }
}

/// <summary>
/// Raised when a replenishment recommendation is dismissed (rejected with optional reason).
/// </summary>
public sealed class ReplenishmentDismissedEvent : DomainEvent
{
    public Guid RecommendationId { get; }
    public Guid ProductId { get; }
    public Guid WarehouseId { get; }
    public ReplenishmentStatus FromStatus { get; }
    public string ActorUserId { get; }
    public string? Reason { get; }

    public ReplenishmentDismissedEvent(
        Guid recommendationId,
        Guid productId,
        Guid warehouseId,
        ReplenishmentStatus fromStatus,
        string actorUserId,
        string? reason)
    {
        RecommendationId = recommendationId;
        ProductId = productId;
        WarehouseId = warehouseId;
        FromStatus = fromStatus;
        ActorUserId = actorUserId;
        Reason = reason;
    }
}

/// <summary>
/// Raised when a recommendation is linked to a freshly-created purchase order (fix F-C1).
/// Consumers update KPIs and may trigger supplier notifications.
/// </summary>
public sealed class ReplenishmentLinkedToPoEvent : DomainEvent
{
    public Guid RecommendationId { get; }
    public Guid ProductId { get; }
    public Guid WarehouseId { get; }
    public Guid PurchaseOrderId { get; }
    public ReplenishmentStatus FromStatus { get; }
    public string ActorUserId { get; }

    public ReplenishmentLinkedToPoEvent(
        Guid recommendationId,
        Guid productId,
        Guid warehouseId,
        Guid purchaseOrderId,
        ReplenishmentStatus fromStatus,
        string actorUserId)
    {
        RecommendationId = recommendationId;
        ProductId = productId;
        WarehouseId = warehouseId;
        PurchaseOrderId = purchaseOrderId;
        FromStatus = fromStatus;
        ActorUserId = actorUserId;
    }
}
