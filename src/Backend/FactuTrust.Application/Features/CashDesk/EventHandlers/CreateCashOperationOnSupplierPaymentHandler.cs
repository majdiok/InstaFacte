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

/// <summary>
/// Creates a cash desk debit when a supplier invoice is paid in cash, mirroring the client invoice credit flow.
/// Supplier payment journal entries are generated separately; this handler only updates the cash desk ledger.
/// </summary>
public sealed class CreateCashOperationOnSupplierPaymentHandler
    : INotificationHandler<SupplierPaymentRecordedForAccountingNotification>
{
    private readonly ISupplierPaymentRepository _supplierPaymentRepository;
    private readonly ICashOperationRepository _cashOperationRepository;
    private readonly ICashOperationNumberGenerator _numberGenerator;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<CreateCashOperationOnSupplierPaymentHandler> _logger;
    private readonly IOptions<CashDeskFeaturesOptions> _features;

    public CreateCashOperationOnSupplierPaymentHandler(
        ISupplierPaymentRepository supplierPaymentRepository,
        ICashOperationRepository cashOperationRepository,
        ICashOperationNumberGenerator numberGenerator,
        ICurrentUser currentUser,
        ILogger<CreateCashOperationOnSupplierPaymentHandler> logger,
        IOptions<CashDeskFeaturesOptions> features)
    {
        _supplierPaymentRepository = supplierPaymentRepository;
        _cashOperationRepository = cashOperationRepository;
        _numberGenerator = numberGenerator;
        _currentUser = currentUser;
        _logger = logger;
        _features = features;
    }

    public async Task Handle(SupplierPaymentRecordedForAccountingNotification notification, CancellationToken cancellationToken)
    {
        if (!_features.Value.AutoCashFromSupplierPayment)
            return;

        var payment = await _supplierPaymentRepository.GetByIdAsync(notification.SupplierPaymentId, cancellationToken);
        if (payment?.SupplierInvoice is null)
            return;

        if (payment.Method != PaymentMethod.Cash || payment.Amount.Amount <= 0)
            return;

        if (await _cashOperationRepository.ExistsBySourceAsync("SupplierPayment", payment.Id, cancellationToken))
            return;

        if (_currentUser.TenantId is null)
        {
            _logger.LogWarning("Auto supplier cash operation skipped: missing TenantId for supplier payment {PaymentId}", payment.Id);
            return;
        }

        var fiscalYear = payment.PaymentDate.Year;
        var number = await _numberGenerator.ReserveNextNumberAsync(
            _currentUser.TenantId.Value,
            fiscalYear,
            CashOperationType.Debit,
            cancellationToken);

        var invoiceNumber = payment.SupplierInvoice.InvoiceNumber;

        var createResult = Domain.Entities.CashOperation.CreateFromSupplierPayment(
            number: number,
            supplierPaymentId: payment.Id,
            supplierInvoiceNumber: invoiceNumber,
            amount: payment.Amount,
            paymentDate: payment.PaymentDate,
            reference: payment.Reference,
            notes: payment.Notes);

        if (createResult.IsFailure)
        {
            _logger.LogWarning(
                "Auto cash operation creation failed for supplier payment {PaymentId}: {Error}",
                payment.Id,
                createResult.Error.Description);
            return;
        }

        var operation = createResult.Value;
        operation.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system", isUpdate: false);
        var saved = await _cashOperationRepository.AddAsync(operation, cancellationToken);

        _logger.LogInformation(
            "Auto cash operation {CashOperationId} created from supplier payment {PaymentId} for supplier invoice {InvoiceId}",
            saved.Id,
            payment.Id,
            payment.SupplierInvoiceId);
    }
}
