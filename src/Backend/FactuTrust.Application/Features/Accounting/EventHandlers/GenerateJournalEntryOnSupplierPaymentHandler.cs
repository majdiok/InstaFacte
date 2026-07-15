using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.Accounting.Notifications;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Application.Features.Accounting.EventHandlers;

public sealed class GenerateJournalEntryOnSupplierPaymentHandler
    : INotificationHandler<SupplierPaymentRecordedForAccountingNotification>
{
    private readonly ISupplierPaymentRepository _supplierPaymentRepository;
    private readonly IAccountingService _accountingService;
    private readonly ILetteringService _letteringService;
    private readonly ILogger<GenerateJournalEntryOnSupplierPaymentHandler> _logger;

    public GenerateJournalEntryOnSupplierPaymentHandler(
        ISupplierPaymentRepository supplierPaymentRepository,
        IAccountingService accountingService,
        ILetteringService letteringService,
        ILogger<GenerateJournalEntryOnSupplierPaymentHandler> logger)
    {
        _supplierPaymentRepository = supplierPaymentRepository;
        _accountingService = accountingService;
        _letteringService = letteringService;
        _logger = logger;
    }

    public async Task Handle(SupplierPaymentRecordedForAccountingNotification notification, CancellationToken cancellationToken)
    {
        var payment = await _supplierPaymentRepository.GetByIdAsync(notification.SupplierPaymentId, cancellationToken);
        if (payment is null)
        {
            _logger.LogWarning("Supplier payment {Id} not found for accounting", notification.SupplierPaymentId);
            return;
        }

        var result = await _accountingService.GenerateSupplierPaymentEntryAsync(payment, cancellationToken);
        if (result.IsFailure)
        {
            _logger.LogWarning("Supplier payment accounting failed for {Id}: {Error}",
                notification.SupplierPaymentId, result.Error.Description);
            return;
        }

        // Attempt automatic lettering on supplier account 4011
        await _letteringService.AutoLetterPaymentAsync(
            "SupplierPayment", payment.Id,
            "SupplierInvoice", payment.SupplierInvoiceId,
            cancellationToken);
    }
}
