using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Application.Features.Accounting.EventHandlers;

public sealed class ReverseJournalEntryOnInvoiceCancelledHandler : INotificationHandler<InvoiceCancelledEvent>
{
    private readonly IAccountingService _accountingService;
    private readonly ILogger<ReverseJournalEntryOnInvoiceCancelledHandler> _logger;

    public ReverseJournalEntryOnInvoiceCancelledHandler(
        IAccountingService accountingService,
        ILogger<ReverseJournalEntryOnInvoiceCancelledHandler> logger)
    {
        _accountingService = accountingService;
        _logger = logger;
    }

    public async Task Handle(InvoiceCancelledEvent notification, CancellationToken cancellationToken)
    {
        var result = await _accountingService.ReverseInvoiceSaleEntryAsync(
            notification.InvoiceId,
            notification.InvoiceNumber,
            cancellationToken);

        if (result.IsFailure)
            _logger.LogWarning("Accounting reversal failed for invoice {InvoiceNumber}: {Error}",
                notification.InvoiceNumber, result.Error.Description);
    }
}
