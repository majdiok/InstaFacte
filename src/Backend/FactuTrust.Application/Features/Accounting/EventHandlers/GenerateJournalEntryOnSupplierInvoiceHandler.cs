using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.Accounting.Notifications;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Application.Features.Accounting.EventHandlers;

public sealed class GenerateJournalEntryOnSupplierInvoiceHandler
    : INotificationHandler<SupplierInvoiceCreatedForAccountingNotification>
{
    private readonly ISupplierInvoiceRepository _supplierInvoiceRepository;
    private readonly IAccountingService _accountingService;
    private readonly ILogger<GenerateJournalEntryOnSupplierInvoiceHandler> _logger;

    public GenerateJournalEntryOnSupplierInvoiceHandler(
        ISupplierInvoiceRepository supplierInvoiceRepository,
        IAccountingService accountingService,
        ILogger<GenerateJournalEntryOnSupplierInvoiceHandler> logger)
    {
        _supplierInvoiceRepository = supplierInvoiceRepository;
        _accountingService = accountingService;
        _logger = logger;
    }

    public async Task Handle(SupplierInvoiceCreatedForAccountingNotification notification, CancellationToken cancellationToken)
    {
        var invoice = await _supplierInvoiceRepository.GetByIdWithLinesAsync(notification.SupplierInvoiceId, cancellationToken);
        if (invoice is null)
        {
            _logger.LogWarning("Supplier invoice {Id} not found for accounting", notification.SupplierInvoiceId);
            return;
        }

        var result = await _accountingService.GenerateSupplierInvoiceEntryAsync(invoice, cancellationToken);
        if (result.IsFailure)
            _logger.LogWarning("Supplier invoice accounting failed for {Id}: {Error}",
                notification.SupplierInvoiceId, result.Error.Description);
    }
}
