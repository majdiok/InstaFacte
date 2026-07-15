using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Notifications;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Invoices.Commands;

/// <summary>
/// Règle un effet de commerce client à échéance : encaissement (532/412) ou impayé (4111/412).
/// Sur impayé, l'effet est marqué remboursé (exclu des totaux) et la facture est réouverte.
/// </summary>
public sealed record SettleClientEffetCommand(Guid PaymentId, SettleEffetRequest Request) : IRequest<Result>;

public sealed class SettleClientEffetCommandHandler : IRequestHandler<SettleClientEffetCommand, Result>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;
    private readonly IPublisher _publisher;
    private readonly AccountingSettings _accountingSettings;

    public SettleClientEffetCommandHandler(
        IPaymentRepository paymentRepository,
        IInvoiceRepository invoiceRepository,
        ICurrentUser currentUser,
        IAuditService auditService,
        IPublisher publisher,
        IOptions<AccountingSettings> accountingSettings)
    {
        _paymentRepository = paymentRepository;
        _invoiceRepository = invoiceRepository;
        _currentUser = currentUser;
        _auditService = auditService;
        _publisher = publisher;
        _accountingSettings = accountingSettings.Value;
    }

    public async Task<Result> Handle(SettleClientEffetCommand request, CancellationToken cancellationToken)
    {
        if (!_accountingSettings.EffetDeCommerceEnabled)
            return Result.Failure(Error.Validation("Effet", "Le paiement par traite n'est pas activé"));

        var payment = await _paymentRepository.GetByIdAsync(request.PaymentId, cancellationToken);
        if (payment is null)
            return Result.Failure(Error.NotFound("Paiement", request.PaymentId));

        if (payment.Method != PaymentMethod.Traite)
            return Result.Failure(Error.Validation("Effet", "Ce paiement n'est pas une traite"));

        var outcome = request.Request.Outcome switch
        {
            1 => EffetStatus.Encaisse,
            2 => EffetStatus.Impaye,
            _ => (EffetStatus?)null
        };
        if (outcome is null)
            return Result.Failure(Error.Validation("Outcome", "L'issue de règlement de l'effet est invalide"));

        var settled = payment.MarkEffetSettled(request.Request.SettlementDate, outcome.Value);
        if (settled.IsFailure)
            return settled;

        if (outcome == EffetStatus.Impaye)
        {
            var refund = payment.Refund("Effet impayé");
            if (refund.IsFailure)
                return refund;
        }

        var userId = _currentUser.UserId?.ToString() ?? "system";
        payment.SetAuditInfo(userId, isUpdate: true);
        await _paymentRepository.UpdateAsync(payment, cancellationToken);

        if (outcome == EffetStatus.Impaye)
        {
            // La créance client est réouverte : recalcul du total réellement encaissé (l'effet impayé,
            // marqué remboursé, est désormais exclu de tous les calculs de « restant dû »).
            var allPayments = await _paymentRepository.GetByInvoiceIdAsync(payment.InvoiceId, cancellationToken);
            var active = allPayments.Where(p => !p.IsRefunded).ToList();
            var totalPaid = active.Sum(p => p.GetTotalAppliedTowardInvoice());
            DateTime? lastPaymentDate = active.Count > 0 ? active.Max(p => p.PaymentDate) : null;

            var invoice = await _invoiceRepository.GetByIdWithLinesAsync(payment.InvoiceId, cancellationToken);
            if (invoice is not null)
            {
                invoice.ReopenAfterEffetUnpaid(totalPaid, lastPaymentDate);
                invoice.SetAuditInfo(userId, isUpdate: true);
                await _invoiceRepository.UpdateAsync(invoice, cancellationToken);
            }
        }

        await _auditService.LogAsync(
            AuditActions.Invoice.EffetSettled,
            "Payment",
            payment.Id,
            newValues: new { payment.InvoiceId, outcome = outcome.Value.ToString(), request.Request.SettlementDate },
            cancellationToken: cancellationToken);

        await _publisher.Publish(new ClientEffetSettledNotification(payment.Id), cancellationToken);

        return Result.Success();
    }
}
