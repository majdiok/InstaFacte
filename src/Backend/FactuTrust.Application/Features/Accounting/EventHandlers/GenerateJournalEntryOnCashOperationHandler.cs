using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.Accounting.Notifications;
using FactuTrust.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Application.Features.Accounting.EventHandlers;

public sealed class GenerateJournalEntryOnCashOperationHandler
    : INotificationHandler<CashOperationCreatedForAccountingNotification>
{
    private readonly ICashOperationRepository _cashOperationRepository;
    private readonly IAccountingService _accountingService;
    private readonly ILogger<GenerateJournalEntryOnCashOperationHandler> _logger;

    public GenerateJournalEntryOnCashOperationHandler(
        ICashOperationRepository cashOperationRepository,
        IAccountingService accountingService,
        ILogger<GenerateJournalEntryOnCashOperationHandler> logger)
    {
        _cashOperationRepository = cashOperationRepository;
        _accountingService = accountingService;
        _logger = logger;
    }

    public async Task Handle(CashOperationCreatedForAccountingNotification notification, CancellationToken cancellationToken)
    {
        var op = await _cashOperationRepository.GetByIdAsync(notification.CashOperationId, cancellationToken);
        if (op is null)
        {
            _logger.LogWarning("Cash operation {Id} not found for accounting", notification.CashOperationId);
            return;
        }

        if (op.Origin == CashOperationOrigin.InvoicePayment)
            return;

        if (op.Origin == CashOperationOrigin.SupplierPayment)
            return;

        if (op.Category == CashExpenseCategory.BankDeposit)
            return;

        var result = await _accountingService.GenerateCashOperationEntryAsync(op, cancellationToken);
        if (result.IsFailure)
            _logger.LogWarning("Cash operation accounting failed for {Id}: {Error}",
                notification.CashOperationId, result.Error.Description);
    }
}
