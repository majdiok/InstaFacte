using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Application.Features.Accounting.EventHandlers;

public sealed class GenerateJournalEntryOnInvoiceValidatedHandler : INotificationHandler<InvoiceValidatedEvent>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IAccountingService _accountingService;
    private readonly ILogger<GenerateJournalEntryOnInvoiceValidatedHandler> _logger;

    public GenerateJournalEntryOnInvoiceValidatedHandler(
        IInvoiceRepository invoiceRepository,
        IAccountingService accountingService,
        ILogger<GenerateJournalEntryOnInvoiceValidatedHandler> logger)
    {
        _invoiceRepository = invoiceRepository;
        _accountingService = accountingService;
        _logger = logger;
    }

    public async Task Handle(InvoiceValidatedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var invoice = await _invoiceRepository.GetByIdWithLinesAsync(notification.InvoiceId, cancellationToken);
            if (invoice is null)
            {
                _logger.LogWarning("Invoice {InvoiceId} not found for accounting entry", notification.InvoiceId);
                return;
            }

            var result = invoice.Type == InvoiceType.CreditNote
                ? await _accountingService.GenerateInvoiceCreditNoteEntryAsync(invoice, cancellationToken)
                : await _accountingService.GenerateInvoiceSaleEntryAsync(invoice, cancellationToken);

            if (result.IsFailure)
                _logger.LogWarning("Accounting entry failed for invoice {InvoiceNumber}: {Error}",
                    notification.InvoiceNumber, result.Error.Description);
        }
        catch (Exception ex)
        {
            // Side-effects must never fail the main operation. The invoice is already validated in DB.
            _logger.LogError(ex,
                "Unexpected error generating accounting entry for invoice {InvoiceNumber}. " +
                "Invoice was validated — only the accounting side-effect is missing.",
                notification.InvoiceNumber);
        }
    }
}
