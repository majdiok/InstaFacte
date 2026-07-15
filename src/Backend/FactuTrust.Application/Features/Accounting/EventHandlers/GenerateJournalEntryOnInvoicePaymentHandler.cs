using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.Accounting.Notifications;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Application.Features.Accounting.EventHandlers;

public sealed class GenerateJournalEntryOnInvoicePaymentHandler : INotificationHandler<InvoicePaymentRecordedNotification>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IAccountingService _accountingService;
    private readonly ILetteringService _letteringService;
    private readonly ILogger<GenerateJournalEntryOnInvoicePaymentHandler> _logger;

    public GenerateJournalEntryOnInvoicePaymentHandler(
        IPaymentRepository paymentRepository,
        IAccountingService accountingService,
        ILetteringService letteringService,
        ILogger<GenerateJournalEntryOnInvoicePaymentHandler> logger)
    {
        _paymentRepository = paymentRepository;
        _accountingService = accountingService;
        _letteringService = letteringService;
        _logger = logger;
    }

    public async Task Handle(InvoicePaymentRecordedNotification notification, CancellationToken cancellationToken)
    {
        var payment = await _paymentRepository.GetByIdAsync(notification.PaymentId, cancellationToken);
        if (payment is null)
        {
            _logger.LogWarning("Payment {PaymentId} not found for accounting entry", notification.PaymentId);
            return;
        }

        var result = await _accountingService.GenerateClientPaymentEntryAsync(payment, cancellationToken);
        if (result.IsFailure)
        {
            _logger.LogWarning("Accounting payment entry failed for payment {PaymentId}: {Error}",
                notification.PaymentId, result.Error.Description);
            return;
        }

        // Attempt automatic lettering of the payment against the invoice on account 4111
        await _letteringService.AutoLetterPaymentAsync(
            "Payment", payment.Id,
            "Invoice", payment.InvoiceId,
            cancellationToken);
    }
}
