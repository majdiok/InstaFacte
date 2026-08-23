using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.Accounting.Notifications;
using FactuTrust.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Application.Features.Accounting.EventHandlers;

/// <summary>
/// Génère le 2ᵉ volet comptable d'un effet client réglé à échéance : encaissement (532/413)
/// ou impayé (4111/413). L'issue est portée par le statut de l'effet positionné avant publication.
/// </summary>
public sealed class GenerateJournalEntryOnClientEffetSettledHandler : INotificationHandler<ClientEffetSettledNotification>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IAccountingService _accountingService;
    private readonly ILogger<GenerateJournalEntryOnClientEffetSettledHandler> _logger;

    public GenerateJournalEntryOnClientEffetSettledHandler(
        IPaymentRepository paymentRepository,
        IAccountingService accountingService,
        ILogger<GenerateJournalEntryOnClientEffetSettledHandler> logger)
    {
        _paymentRepository = paymentRepository;
        _accountingService = accountingService;
        _logger = logger;
    }

    public async Task Handle(ClientEffetSettledNotification notification, CancellationToken cancellationToken)
    {
        var payment = await _paymentRepository.GetByIdAsync(notification.PaymentId, cancellationToken);
        if (payment is null)
        {
            _logger.LogWarning("Payment {PaymentId} not found for effet settlement entry", notification.PaymentId);
            return;
        }

        if (payment.EffetStatus is not (EffetStatus.Encaisse or EffetStatus.Impaye))
        {
            _logger.LogWarning("Payment {PaymentId} is not a settled effet ({Status})", notification.PaymentId, payment.EffetStatus);
            return;
        }

        var result = await _accountingService.GenerateClientEffetSettlementEntryAsync(
            payment, payment.EffetStatus.Value, cancellationToken);
        if (result.IsFailure)
        {
            _logger.LogWarning("Effet settlement entry failed for payment {PaymentId}: {Error}",
                notification.PaymentId, result.Error.Description);
        }
    }
}
