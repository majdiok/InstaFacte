using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Application.Features.PurchaseOrders.EventHandlers;

/// <summary>
/// Handles PurchaseOrderConfirmedEvent.
/// Logs the confirmation for traceability.
/// </summary>
public sealed class LogPurchaseOrderConfirmedHandler : INotificationHandler<PurchaseOrderConfirmedEvent>
{
    private readonly IAuditService _auditService;
    private readonly ILogger<LogPurchaseOrderConfirmedHandler> _logger;

    public LogPurchaseOrderConfirmedHandler(
        IAuditService auditService,
        ILogger<LogPurchaseOrderConfirmedHandler> logger)
    {
        _auditService = auditService;
        _logger = logger;
    }

    public async Task Handle(PurchaseOrderConfirmedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Purchase order {PurchaseOrderId} ({Number}) confirmed for supplier {SupplierId}",
            notification.PurchaseOrderId,
            notification.Number,
            notification.SupplierId);

        await Task.CompletedTask;
    }
}

/// <summary>
/// Handles GoodsReceivedEvent.
/// Logs reception details for traceability.
/// </summary>
public sealed class LogGoodsReceivedHandler : INotificationHandler<GoodsReceivedEvent>
{
    private readonly ILogger<LogGoodsReceivedHandler> _logger;

    public LogGoodsReceivedHandler(ILogger<LogGoodsReceivedHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(GoodsReceivedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Goods received for purchase order {PurchaseOrderId} ({Number}): {LinesReceived} lines received. Fully received: {IsFullyReceived}",
            notification.PurchaseOrderId,
            notification.Number,
            notification.LinesReceived,
            notification.IsFullyReceived);

        return Task.CompletedTask;
    }
}

/// <summary>
/// Handles PurchaseOrderCancelledEvent.
/// Logs cancellation for traceability.
/// </summary>
public sealed class LogPurchaseOrderCancelledHandler : INotificationHandler<PurchaseOrderCancelledEvent>
{
    private readonly ILogger<LogPurchaseOrderCancelledHandler> _logger;

    public LogPurchaseOrderCancelledHandler(ILogger<LogPurchaseOrderCancelledHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(PurchaseOrderCancelledEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Purchase order {PurchaseOrderId} ({Number}) cancelled. Reason: {Reason}",
            notification.PurchaseOrderId,
            notification.Number,
            notification.Reason);

        return Task.CompletedTask;
    }
}
