using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Application.Features.SalesOrders.EventHandlers;

/// <summary>
/// Impute une facture sur la commande dont elle provient, au moment de sa VALIDATION.
///
/// L'imputation est faite à la validation et non à la création : une facture restée
/// brouillon puis abandonnée ferait sinon apparaître la commande comme facturée à tort.
/// Symétrique de l'imputation des livraisons, qui se fait à la confirmation du bon.
///
/// Un avoir déduit au lieu d'ajouter : il rouvre le reste à facturer.
/// </summary>
public sealed class ImputeSalesOrderOnInvoiceHandler : INotificationHandler<InvoiceValidatedEvent>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly ISalesOrderRepository _salesOrderRepository;
    private readonly ILogger<ImputeSalesOrderOnInvoiceHandler> _logger;

    public ImputeSalesOrderOnInvoiceHandler(
        IInvoiceRepository invoiceRepository,
        ISalesOrderRepository salesOrderRepository,
        ILogger<ImputeSalesOrderOnInvoiceHandler> logger)
    {
        _invoiceRepository = invoiceRepository;
        _salesOrderRepository = salesOrderRepository;
        _logger = logger;
    }

    public async Task Handle(InvoiceValidatedEvent notification, CancellationToken cancellationToken)
    {
        var invoice = await _invoiceRepository.GetByIdWithLinesAsync(notification.InvoiceId, cancellationToken);

        if (invoice?.SourceSalesOrderId is not { } salesOrderId)
            return; // Facture hors commande : rien à imputer.

        var order = await _salesOrderRepository.GetByIdWithLinesAsync(salesOrderId, cancellationToken);
        if (order is null)
        {
            _logger.LogWarning(
                "Commande {SalesOrderId} introuvable pour l'imputation de la facture {Number}",
                salesOrderId, notification.InvoiceNumber);
            return;
        }

        var isCreditNote = notification.IsCreditNote;

        // Rapprochement par produit : la ligne de facture ne porte pas d'identifiant de ligne
        // de commande. Les lignes libres (ProductId vide) sont ignorées.
        var byProduct = invoice.Lines
            .Where(l => l.ProductId != Guid.Empty && l.Quantity > 0)
            .GroupBy(l => l.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

        var imputations = new List<(Guid LineId, decimal Quantity)>();

        foreach (var (productId, quantity) in byProduct)
        {
            var remaining = quantity;

            var candidates = order.Lines
                .Where(l => l.ProductId == productId)
                .Where(l => isCreditNote ? l.InvoicedQuantity > 0 : l.PendingInvoiceQuantity > 0)
                .OrderBy(l => l.LineNumber);

            foreach (var line in candidates)
            {
                if (remaining <= 0)
                    break;

                var capacity = isCreditNote ? line.InvoicedQuantity : line.PendingInvoiceQuantity;
                var take = Math.Min(remaining, capacity);
                imputations.Add((line.Id, take));
                remaining -= take;
            }

            if (remaining > 0)
            {
                _logger.LogWarning(
                    "Facture {Number} : {Excess} unité(s) du produit {ProductId} hors capacité de la commande {OrderNumber}",
                    notification.InvoiceNumber, remaining, productId, order.Number.Value);
            }
        }

        if (imputations.Count == 0)
            return;

        var result = isCreditNote
            ? order.ReverseInvoiced(imputations)
            : order.RecordInvoiced(imputations);

        if (result.IsFailure)
        {
            _logger.LogWarning(
                "Imputation impossible sur la commande {OrderNumber} depuis la facture {Number} : {Error}",
                order.Number.Value, notification.InvoiceNumber, result.Error.Description);
            return;
        }

        await _salesOrderRepository.UpdateAsync(order, cancellationToken);

        _logger.LogInformation(
            "Facture {Number} imputée sur la commande {OrderNumber} — reste à facturer : {Pending}",
            notification.InvoiceNumber, order.Number.Value, order.TotalPendingInvoiceQuantity);
    }
}
