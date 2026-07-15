using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Application.Features.Accounting.EventHandlers;

public sealed class GenerateJournalEntryOnPayrollRunValidatedHandler : INotificationHandler<PayrollRunValidatedEvent>
{
    private readonly IPayrollRunRepository _payrollRuns;
    private readonly IAccountingService _accountingService;
    private readonly ILogger<GenerateJournalEntryOnPayrollRunValidatedHandler> _logger;

    public GenerateJournalEntryOnPayrollRunValidatedHandler(
        IPayrollRunRepository payrollRuns,
        IAccountingService accountingService,
        ILogger<GenerateJournalEntryOnPayrollRunValidatedHandler> logger)
    {
        _payrollRuns = payrollRuns;
        _accountingService = accountingService;
        _logger = logger;
    }

    public async Task Handle(PayrollRunValidatedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var run = await _payrollRuns.GetByIdWithPayslipsAsync(notification.PayrollRunId, cancellationToken);
            if (run is null)
            {
                _logger.LogWarning("Payroll run {PayrollRunId} not found for accounting entry", notification.PayrollRunId);
                return;
            }

            var result = await _accountingService.GeneratePayrollRunEntryAsync(run, cancellationToken);
            if (result.IsFailure)
                _logger.LogWarning("Accounting entry failed for payroll {Year}/{Month}: {Error}",
                    notification.Year, notification.Month, result.Error.Description);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error generating accounting entry for payroll {Year}/{Month}. " +
                "Payroll run was validated — only the accounting side-effect is missing.",
                notification.Year, notification.Month);
        }
    }
}
