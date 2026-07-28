using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Notifications;
using FactuTrust.Application.Features.Invoices.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Invoices.Commands;

/// <summary>
/// Command to record a payment on a client invoice.
/// Supports multiple partial payments per invoice.
/// </summary>
public sealed record RecordInvoicePaymentCommand(Guid InvoiceId, RecordInvoicePaymentRequest Request) : IRequest<Result>;

/// <summary>
/// Handler for RecordInvoicePaymentCommand.
/// </summary>
public sealed class RecordInvoicePaymentCommandHandler : IRequestHandler<RecordInvoicePaymentCommand, Result>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;
    private readonly IPublisher _publisher;
    private readonly AccountingSettings _accountingSettings;

    public RecordInvoicePaymentCommandHandler(
        IInvoiceRepository invoiceRepository,
        IPaymentRepository paymentRepository,
        ICurrentUser currentUser,
        IAuditService auditService,
        IPublisher publisher,
        IOptions<AccountingSettings> accountingSettings)
    {
        _invoiceRepository = invoiceRepository;
        _paymentRepository = paymentRepository;
        _currentUser = currentUser;
        _auditService = auditService;
        _publisher = publisher;
        _accountingSettings = accountingSettings.Value;
    }

    public async Task<Result> Handle(RecordInvoicePaymentCommand request, CancellationToken cancellationToken)
    {
        var invoice = await _invoiceRepository.GetByIdWithLinesAsync(request.InvoiceId, cancellationToken);

        if (invoice is null)
            return Result.Failure(Error.NotFound("Facture", request.InvoiceId));

        if (!invoice.Status.CanBePaymentRecorded())
            return Result.Failure(Error.Validation("Status", "Cette facture ne peut pas recevoir de paiement dans son état actuel"));

        var existingPayments = await _paymentRepository.GetByInvoiceIdAsync(request.InvoiceId, cancellationToken);
        var totalPaid = existingPayments
            .Where(p => !p.IsRefunded)
            .Sum(p => p.GetTotalAppliedTowardInvoice());

        var withholding = request.Request.ClientWithholdingAmount ?? 0m;

        var resolved = InvoicePaymentCalculator.Resolve(
            invoice, totalPaid, request.Request.Amount, withholding);

        if (resolved.IsFailure)
            return Result.Failure(resolved.Error);

        var (netReceived, appliedTowardInvoice) = resolved.Value;

        var money = Money.Create(netReceived, invoice.TotalAmount.Currency);
        var paymentDate = request.Request.PaymentDate;
        var method = request.Request.Method ?? PaymentMethod.Other;

        if (method == PaymentMethod.Traite && !_accountingSettings.EffetDeCommerceEnabled)
            return Result.Failure(Error.Validation("Method", "Le paiement par traite n'est pas activé"));

        var paymentResult = Payment.Create(
            invoice,
            money,
            paymentDate,
            method,
            request.Request.Reference,
            request.Request.Notes,
            withholding > 0 ? withholding : null,
            request.Request.EffetDueDate);

        if (paymentResult.IsFailure)
            return paymentResult;

        var payment = paymentResult.Value;
        payment.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system", isUpdate: false);

        await _paymentRepository.AddAsync(payment, cancellationToken);

        var newTotalPaid = totalPaid + appliedTowardInvoice;
        var allPaymentDates = existingPayments.Select(p => p.PaymentDate).Append(paymentDate);
        var lastPaymentDate = allPaymentDates.Max();

        invoice.ReconcilePaymentStatus(newTotalPaid, lastPaymentDate);
        invoice.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system", isUpdate: true);

        await _invoiceRepository.UpdateAsync(invoice, cancellationToken);

        await _auditService.LogAsync(
            AuditActions.Invoice.Paid,
            "Invoice",
            invoice.Id,
            newValues: new { invoice.Number.Value, netReceived, withholding, paymentDate, totalPaid = newTotalPaid },
            cancellationToken: cancellationToken);

        await _publisher.Publish(new InvoicePaymentRecordedNotification(payment.Id), cancellationToken);

        return Result.Success();
    }
}
