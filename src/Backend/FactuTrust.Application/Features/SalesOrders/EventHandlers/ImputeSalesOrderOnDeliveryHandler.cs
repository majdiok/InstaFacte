using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Application.Features.SalesOrders.EventHandlers;

/// <summary>
/// Impute une livraison sur la commande dont elle provient, et libère la réservation
/// correspondante.
///
/// C'est ce handler qui fait vivre le reliquat : sans lui, la commande ignorerait ce qui a
/// été livré et son « reste à livrer » ne décroîtrait jamais. Le rapprochement se fait par
/// produit, la ligne de bon de livraison ne portant pas d'identifiant de ligne de commande.
/// </summary>
public sealed class ImputeSalesOrderOnDeliveryHandler : INotificationHandler<DeliveryNoteDeliveredEvent>
{
    private readonly IDeliveryNoteRepository _deliveryNoteRepository;
    private readonly ISalesOrderRepository _salesOrderRepository;
    private readonly ILogger<ImputeSalesOrderOnDeliveryHandler> _logger;

    public ImputeSalesOrderOnDeliveryHandler(
        IDeliveryNoteRepository deliveryNoteRepository,
        ISalesOrderRepository salesOrderRepository,
        ILogger<ImputeSalesOrderOnDeliveryHandler> logger)
    {
        _deliveryNoteRepository = deliveryNoteRepository;
        _salesOrderRepository = salesOrderRepository;
        _logger = logger;
    }

    public async Task Handle(DeliveryNoteDeliveredEvent notification, CancellationToken cancellationToken)
    {
        var deliveryNote = await _deliveryNoteRepository.GetByIdWithLinesAsync(
            notification.DeliveryNoteId, cancellationToken);

        if (deliveryNote?.SourceSalesOrderId is not { } salesOrderId)
            return; // Bon de livraison hors commande : rien à imputer.

        var order = await _salesOrderRepository.GetByIdWithLinesAsync(salesOrderId, cancellationToken);
        if (order is null)
        {
            _logger.LogWarning(
                "Commande {SalesOrderId} introuvable pour l'imputation du BL {Number}",
                salesOrderId, notification.DeliveryNoteNumber);
            return;
        }

        // Rapprochement par produit. Une même ligne de commande peut être servie par
        // plusieurs lignes de bon ; on cumule par produit avant d'imputer.
        var deliveredByProduct = deliveryNote.Lines
            .Where(l => l.DeliveredQuantity > 0)
            .GroupBy(l => l.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.DeliveredQuantity));

        var imputations = new List<(Guid LineId, decimal Quantity)>();

        foreach (var (productId, delivered) in deliveredByProduct)
        {
            var remaining = delivered;

            // Plusieurs lignes de commande peuvent porter le même produit : on impute dans
            // l'ordre des lignes, en s'arrêtant au reste à livrer de chacune.
            foreach (var line in order.Lines
                         .Where(l => l.ProductId == productId && l.PendingDeliveryQuantity > 0)
                         .OrderBy(l => l.LineNumber))
            {
                if (remaining <= 0)
                    break;

                var take = Math.Min(remaining, line.PendingDeliveryQuantity);
                imputations.Add((line.Id, take));
                remaining -= take;
            }

            if (remaining > 0)
            {
                // Livré plus que commandé pour ce produit : on impute ce qui peut l'être et on
                // trace l'excédent, plutôt que de refuser une livraison déjà physiquement faite.
                _logger.LogWarning(
                    "BL {Number} : {Excess} unité(s) du produit {ProductId} livrées au-delà de la commande {OrderNumber}",
                    notification.DeliveryNoteNumber, remaining, productId, order.Number.Value);
            }
        }

        if (imputations.Count == 0)
            return;

        var result = order.RecordDeliveries(imputations);
        if (result.IsFailure)
        {
            _logger.LogWarning(
                "Imputation impossible sur la commande {OrderNumber} depuis le BL {Number} : {Error}",
                order.Number.Value, notification.DeliveryNoteNumber, result.Error.Description);
            return;
        }

        await _salesOrderRepository.UpdateAsync(order, cancellationToken);

        _logger.LogInformation(
            "BL {Number} imputé sur la commande {OrderNumber} — reste à livrer : {Pending}",
            notification.DeliveryNoteNumber, order.Number.Value, order.TotalPendingDeliveryQuantity);
    }
}
