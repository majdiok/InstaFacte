using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Accounting.Notifications;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.CashDesk.EventHandlers;

public sealed class CreateCashOperationOnInvoicePaymentHandler
    : INotificationHandler<InvoicePaymentRecordedNotification>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly ICashOperationRepository _cashOperationRepository;
    private readonly ICashOperationNumberGenerator _numberGenerator;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<CreateCashOperationOnInvoicePaymentHandler> _logger;
    private readonly IOptions<CashDeskFeaturesOptions> _features;

    public CreateCashOperationOnInvoicePaymentHandler(
        IPaymentRepository paymentRepository,
        ICashOperationRepository cashOperationRepository,
        ICashOperationNumberGenerator numberGenerator,
        ICurrentUser currentUser,
        ILogger<CreateCashOperationOnInvoicePaymentHandler> logger,
        IOptions<CashDeskFeaturesOptions> features)
    {
        _paymentRepository = paymentRepository;
        _cashOperationRepository = cashOperationRepository;
        _numberGenerator = numberGenerator;
        _currentUser = currentUser;
        _logger = logger;
        _features = features;
    }

    public async Task Handle(InvoicePaymentRecordedNotification notification, CancellationToken cancellationToken)
    {
        if (!_features.Value.AutoCashFromInvoicePayment)
            return;

        var payment = await _paymentRepository.GetByIdAsync(notification.PaymentId, cancellationToken);
        if (payment?.Invoice is null)
            return;

        if (payment.Method != PaymentMethod.Cash || payment.IsRefunded || payment.Amount.Amount <= 0)
            return;

        if (await _cashOperationRepository.ExistsBySourceAsync("Payment", payment.Id, cancellationToken))
            return;

        if (_currentUser.TenantId is null)
        {
            _logger.LogWarning("Auto cash operation skipped: missing TenantId for payment {PaymentId}", payment.Id);
            return;
        }

        var fiscalYear = payment.PaymentDate.Year;
        // For a credit note (AVO), paying it out means reimbursing the client — cash leaves the desk (Debit).
        // For a regular invoice (FAC), the client pays us — cash enters the desk (Credit).
        var isRefund = payment.Invoice.IsCreditNote;
        var operationType = isRefund ? CashOperationType.Debit : CashOperationType.Credit;
        var number = await _numberGenerator.ReserveNextNumberAsync(
            _currentUser.TenantId.Value,
            fiscalYear,
            operationType,
            cancellationToken);

        var createResult = isRefund
            ? Domain.Entities.CashOperation.CreateFromInvoiceRefund(
                number: number,
                paymentId: payment.Id,
                invoiceNumber: payment.Invoice.Number.Value,
                amount: payment.Amount,
                paymentDate: payment.PaymentDate,
                reference: payment.Reference,
                notes: payment.Notes)
            : Domain.Entities.CashOperation.CreateFromInvoicePayment(
                number: number,
                paymentId: payment.Id,
                invoiceNumber: payment.Invoice.Number.Value,
                amount: payment.Amount,
                paymentDate: payment.PaymentDate,
                reference: payment.Reference,
                notes: payment.Notes);

        if (createResult.IsFailure)
        {
            _logger.LogWarning(
                "Auto cash operation creation failed for payment {PaymentId}: {Error}",
                payment.Id,
                createResult.Error.Description);
            return;
        }

        var operation = createResult.Value;
        if (payment.CashRegisterSessionId is { } sessionId)
            operation.AssignCashRegisterSession(sessionId);
        operation.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system", isUpdate: false);
        var saved = await _cashOperationRepository.AddAsync(operation, cancellationToken);

        _logger.LogInformation(
            "Auto cash operation {CashOperationId} created from payment {PaymentId} for invoice {InvoiceId}",
            saved.Id,
            payment.Id,
            payment.InvoiceId);
    }
}
