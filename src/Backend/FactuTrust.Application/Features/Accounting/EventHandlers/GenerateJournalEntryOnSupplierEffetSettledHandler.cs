using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.Accounting.Notifications;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Application.Features.Accounting.EventHandlers;

/// <summary>
/// Génère le 2ᵉ volet comptable d'un effet fournisseur payé à échéance : 403/532.
/// </summary>
public sealed class GenerateJournalEntryOnSupplierEffetSettledHandler : INotificationHandler<SupplierEffetSettledNotification>
{
    private readonly ISupplierInvoiceRepository _supplierInvoiceRepository;
    private readonly IAccountingService _accountingService;
    private readonly ILogger<GenerateJournalEntryOnSupplierEffetSettledHandler> _logger;

    public GenerateJournalEntryOnSupplierEffetSettledHandler(
        ISupplierInvoiceRepository supplierInvoiceRepository,
        IAccountingService accountingService,
        ILogger<GenerateJournalEntryOnSupplierEffetSettledHandler> logger)
    {
        _supplierInvoiceRepository = supplierInvoiceRepository;
        _accountingService = accountingService;
        _logger = logger;
    }

    public async Task Handle(SupplierEffetSettledNotification notification, CancellationToken cancellationToken)
    {
        var payment = await _supplierInvoiceRepository.GetPaymentByIdWithInvoiceAsync(notification.SupplierPaymentId, cancellationToken);
        if (payment is null)
        {
            _logger.LogWarning("Supplier payment {PaymentId} not found for effet settlement entry", notification.SupplierPaymentId);
            return;
        }

        var result = await _accountingService.GenerateSupplierEffetSettlementEntryAsync(payment, cancellationToken);
        if (result.IsFailure)
        {
            _logger.LogWarning("Supplier effet settlement entry failed for payment {PaymentId}: {Error}",
                notification.SupplierPaymentId, result.Error.Description);
        }
    }
}
