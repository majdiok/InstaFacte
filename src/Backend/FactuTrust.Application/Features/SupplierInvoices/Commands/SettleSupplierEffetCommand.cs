using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Notifications;
using FactuTrust.Domain.Common;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.SupplierInvoices.Commands;

/// <summary>
/// Règle (paie) un effet de commerce fournisseur à échéance : génère l'écriture 403/532.
/// </summary>
public sealed record SettleSupplierEffetCommand(Guid PaymentId, SettleSupplierEffetRequest Request) : IRequest<Result>;

public sealed class SettleSupplierEffetCommandHandler : IRequestHandler<SettleSupplierEffetCommand, Result>
{
    private readonly ISupplierInvoiceRepository _invoiceRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;
    private readonly IPublisher _publisher;
    private readonly AccountingSettings _accountingSettings;

    public SettleSupplierEffetCommandHandler(
        ISupplierInvoiceRepository invoiceRepository,
        ICurrentUser currentUser,
        IAuditService auditService,
        IPublisher publisher,
        IOptions<AccountingSettings> accountingSettings)
    {
        _invoiceRepository = invoiceRepository;
        _currentUser = currentUser;
        _auditService = auditService;
        _publisher = publisher;
        _accountingSettings = accountingSettings.Value;
    }

    public async Task<Result> Handle(SettleSupplierEffetCommand request, CancellationToken cancellationToken)
    {
        if (!_accountingSettings.EffetDeCommerceEnabled)
            return Result.Failure(Error.Validation("Effet", "Le paiement par traite n'est pas activé"));

        var userId = _currentUser.UserId?.ToString() ?? "system";

        var result = await _invoiceRepository.SettleEffetAsync(
            request.PaymentId,
            request.Request.SettlementDate,
            userId,
            cancellationToken);

        if (result.IsFailure)
            return Result.Failure(result.Error);

        var (paymentId, invoiceNumber) = result.Value;

        await _auditService.LogAsync(
            AuditActions.SupplierInvoice.EffetSettled,
            "SupplierPayment",
            paymentId,
            newValues: new { invoiceNumber, request.Request.SettlementDate },
            cancellationToken: cancellationToken);

        await _publisher.Publish(new SupplierEffetSettledNotification(paymentId), cancellationToken);

        return Result.Success();
    }
}
