using FactuTrust.Application.Common;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Notifications;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.SupplierInvoices.Commands;

/// <summary>
/// Command to record a payment (tranche) on a supplier invoice.
/// Supports multiple partial payments per invoice.
/// </summary>
public sealed record RecordSupplierPaymentCommand(Guid SupplierInvoiceId, RecordSupplierPaymentRequest Request) : IRequest<Result>;

/// <summary>
/// Handler for RecordSupplierPaymentCommand.
/// </summary>
public sealed class RecordSupplierPaymentCommandHandler : IRequestHandler<RecordSupplierPaymentCommand, Result>
{
    private readonly ISupplierInvoiceRepository _invoiceRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;
    private readonly IPublisher _publisher;
    private readonly IWithholdingTaxRepository _withholdingTaxRepository;
    private readonly IWithholdingTaxService _withholdingTaxService;
    private readonly IWithholdingFiscalYearParameterRepository _fiscalYearParameters;
    private readonly AccountingSettings _accountingSettings;

    public RecordSupplierPaymentCommandHandler(
        ISupplierInvoiceRepository invoiceRepository,
        ICurrentUser currentUser,
        IAuditService auditService,
        IPublisher publisher,
        IWithholdingTaxRepository withholdingTaxRepository,
        IWithholdingTaxService withholdingTaxService,
        IWithholdingFiscalYearParameterRepository fiscalYearParameters,
        IOptions<AccountingSettings> accountingSettings)
    {
        _invoiceRepository = invoiceRepository;
        _currentUser = currentUser;
        _auditService = auditService;
        _publisher = publisher;
        _withholdingTaxRepository = withholdingTaxRepository;
        _withholdingTaxService = withholdingTaxService;
        _fiscalYearParameters = fiscalYearParameters;
        _accountingSettings = accountingSettings.Value;
    }

    public async Task<Result> Handle(RecordSupplierPaymentCommand request, CancellationToken cancellationToken)
    {
        var paymentDate = request.Request.PaymentDate;
        var method = request.Request.Method ?? PaymentMethod.Other;

        if (method == PaymentMethod.Traite && !_accountingSettings.EffetDeCommerceEnabled)
            return Result.Failure(Error.Validation("Method", "Le paiement par traite n'est pas activé"));

        var result = await _invoiceRepository.RecordPaymentAsync(
            request.SupplierInvoiceId,
            request.Request.Amount,
            paymentDate,
            method,
            request.Request.Reference,
            request.Request.Notes,
            request.Request.EffetDueDate,
            _currentUser.UserId?.ToString() ?? "system",
            cancellationToken);

        if (result.IsFailure)
            return result;

        var auditData = result.Value;

        if (auditData.IsInvoiceNowFullyPaid)
        {
            var invoice = await _invoiceRepository.GetByIdWithLinesAsync(auditData.InvoiceId, cancellationToken);
            if (invoice is not null)
            {
                await SupplierInvoiceWithholdingComputation.ApplyWithholdingPreviewAsync(
                    invoice,
                    _withholdingTaxRepository,
                    _withholdingTaxService,
                    _fiscalYearParameters,
                    cancellationToken);
                await _invoiceRepository.UpdateAsync(invoice, cancellationToken);
            }
        }

        await _auditService.LogAsync(
            AuditActions.SupplierInvoice.Paid,
            "SupplierInvoice",
            auditData.InvoiceId,
            newValues: new
            {
                auditData.InvoiceNumber,
                amount = auditData.Amount,
                paymentDate = auditData.PaymentDate,
                totalPaid = auditData.NewTotalPaid
            },
            cancellationToken: cancellationToken);

        await _publisher.Publish(new SupplierPaymentRecordedForAccountingNotification(auditData.PaymentId), cancellationToken);

        if (auditData.IsInvoiceNowFullyPaid)
        {
            await _publisher.Publish(
                new SupplierInvoiceWithholdingAccountingNotification(auditData.InvoiceId),
                cancellationToken);
        }

        return Result.Success();
    }
}
