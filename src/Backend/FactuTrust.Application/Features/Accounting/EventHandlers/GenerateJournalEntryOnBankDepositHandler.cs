using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.Accounting.Notifications;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Application.Features.Accounting.EventHandlers;

public sealed class GenerateJournalEntryOnBankDepositHandler
    : INotificationHandler<BankDepositCreatedForAccountingNotification>
{
    private readonly IBankDepositRepository _bankDepositRepository;
    private readonly IAccountingService _accountingService;
    private readonly ILogger<GenerateJournalEntryOnBankDepositHandler> _logger;

    public GenerateJournalEntryOnBankDepositHandler(
        IBankDepositRepository bankDepositRepository,
        IAccountingService accountingService,
        ILogger<GenerateJournalEntryOnBankDepositHandler> logger)
    {
        _bankDepositRepository = bankDepositRepository;
        _accountingService = accountingService;
        _logger = logger;
    }

    public async Task Handle(BankDepositCreatedForAccountingNotification notification, CancellationToken cancellationToken)
    {
        var deposit = await _bankDepositRepository.GetByIdAsync(notification.BankDepositId, cancellationToken);
        if (deposit is null)
        {
            _logger.LogWarning("Bank deposit {Id} not found for accounting", notification.BankDepositId);
            return;
        }

        var result = await _accountingService.GenerateBankDepositEntryAsync(deposit, cancellationToken);
        if (result.IsFailure)
            _logger.LogWarning("Bank deposit accounting failed for {Id}: {Error}",
                notification.BankDepositId, result.Error.Description);
    }
}
