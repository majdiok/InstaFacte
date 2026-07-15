using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.Accounting.Notifications;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Application.Features.Accounting.EventHandlers;

public sealed class GenerateJournalEntryOnSupplierInvoiceWithholdingHandler
    : INotificationHandler<SupplierInvoiceWithholdingAccountingNotification>
{
    private readonly ISupplierInvoiceRepository _invoiceRepository;
    private readonly IAccountingService _accountingService;
    private readonly ILogger<GenerateJournalEntryOnSupplierInvoiceWithholdingHandler> _logger;

    public GenerateJournalEntryOnSupplierInvoiceWithholdingHandler(
        ISupplierInvoiceRepository invoiceRepository,
        IAccountingService accountingService,
        ILogger<GenerateJournalEntryOnSupplierInvoiceWithholdingHandler> logger)
    {
        _invoiceRepository = invoiceRepository;
        _accountingService = accountingService;
        _logger = logger;
    }

    public async Task Handle(SupplierInvoiceWithholdingAccountingNotification notification, CancellationToken cancellationToken)
    {
        var invoice = await _invoiceRepository.GetByIdWithLinesAsync(notification.SupplierInvoiceId, cancellationToken);
        if (invoice is null)
        {
            _logger.LogWarning("Supplier invoice {Id} not found for withholding accounting", notification.SupplierInvoiceId);
            return;
        }

        var result = await _accountingService.GenerateSupplierInvoiceWithholdingEntryAsync(invoice, cancellationToken);
        if (result.IsFailure)
        {
            _logger.LogWarning("Supplier invoice withholding accounting failed for {Id}: {Error}",
                notification.SupplierInvoiceId, result.Error.Description);
        }
    }
}
