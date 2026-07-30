using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Calcule l'encours d'un client (voir <see cref="IClientOutstandingService"/>).
///
/// <b>Deux composantes, volontairement distinctes.</b> Les factures non soldées sont une
/// créance ; les commandes confirmées non facturées n'en sont pas encore une, mais elles
/// engagent. Les additionner donne le risque réel ; les séparer permet à l'écran d'expliquer
/// d'où vient le dépassement, plutôt que d'afficher un total opaque.
///
/// ⚠️ Ce service n'interdit rien. Il informe.
/// </summary>
public sealed class ClientOutstandingService : IClientOutstandingService
{
    private readonly IClientRepository _clientRepository;
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly ISalesOrderRepository _salesOrderRepository;

    public ClientOutstandingService(
        IClientRepository clientRepository,
        IInvoiceRepository invoiceRepository,
        IPaymentRepository paymentRepository,
        ISalesOrderRepository salesOrderRepository)
    {
        _clientRepository = clientRepository;
        _invoiceRepository = invoiceRepository;
        _paymentRepository = paymentRepository;
        _salesOrderRepository = salesOrderRepository;
    }

    public async Task<Result<ClientOutstandingDto>> GetOutstandingAsync(
        Guid clientId, CancellationToken cancellationToken = default)
    {
        var client = await _clientRepository.GetByIdAsync(clientId, cancellationToken);
        if (client is null)
            return Result.Failure<ClientOutstandingDto>(Error.NotFound("Client", clientId));

        var invoices = await _invoiceRepository.GetByClientIdAsync(clientId, cancellationToken);

        // Seules les factures ÉMISES pèsent : un brouillon n'engage personne, un avoir vient en
        // déduction et une facture annulée n'existe plus.
        var receivables = invoices
            .Where(i => i.Status is InvoiceStatus.Validated or InvoiceStatus.Signed or InvoiceStatus.Paid)
            .Where(i => i.Type != InvoiceType.CreditNote)
            .ToList();

        var paidByInvoice = receivables.Count == 0
            ? new Dictionary<Guid, decimal>()
            : (await _paymentRepository.GetTotalPaidByInvoiceIdsAsync(
                receivables.Select(i => i.Id).ToList(), cancellationToken))
                .ToDictionary(kv => kv.Key, kv => kv.Value);

        var today = DateTime.UtcNow.Date;
        var unpaidTotal = 0m;
        var unpaidCount = 0;
        var overdue = 0m;

        foreach (var invoice in receivables)
        {
            var paid = paidByInvoice.TryGetValue(invoice.Id, out var p) ? p : 0m;
            var remaining = invoice.TotalAmount.Amount - paid;

            // Un trop-perçu ne vient pas en déduction des autres factures : il se traite au
            // lettrage, pas ici. On le neutralise plutôt que de minorer l'encours à tort.
            if (remaining <= 0)
                continue;

            unpaidTotal += remaining;
            unpaidCount++;

            // Échu depuis plus de 30 jours : c'est ce chiffre qui appelle une relance, bien
            // plus que l'encours global.
            if (invoice.DueDate is { } due && due.Date.AddDays(30) < today)
                overdue += remaining;
        }

        // Commandes confirmées ou partiellement livrées, part NON encore facturée.
        var openOrders = await _salesOrderRepository.GetOpenOrdersByClientAsync(clientId, cancellationToken);
        var ordersAmount = openOrders.Sum(PendingInvoiceValue);

        var total = decimal.Round(unpaidTotal + ordersAmount, 3);
        var limit = client.CreditLimit;

        return Result.Success(new ClientOutstandingDto
        {
            ClientId = client.Id,
            ClientName = client.Name,
            UnpaidInvoicesAmount = decimal.Round(unpaidTotal, 3),
            ConfirmedOrdersAmount = decimal.Round(ordersAmount, 3),
            TotalOutstanding = total,
            CreditLimit = limit,
            AvailableCredit = limit.HasValue ? decimal.Round(limit.Value - total, 3) : null,
            IsOverLimit = limit.HasValue && total > limit.Value,
            UnpaidInvoiceCount = unpaidCount,
            OverdueAmount = decimal.Round(overdue, 3)
        });
    }

    /// <summary>
    /// Valeur TTC restant à facturer sur une commande, au prorata des quantités non facturées.
    /// Le prorata évite de compter la commande entière alors qu'une partie est déjà facturée —
    /// ce qui compterait la même vente deux fois.
    /// </summary>
    private static decimal PendingInvoiceValue(SalesOrder order)
    {
        var orderedQty = order.Lines.Sum(l => l.Quantity);
        if (orderedQty <= 0)
            return 0m;

        var pendingQty = order.Lines.Sum(l => l.PendingInvoiceQuantity);
        if (pendingQty <= 0)
            return 0m;

        return decimal.Round(order.TotalAmount.Amount * pendingQty / orderedQty, 3);
    }
}
