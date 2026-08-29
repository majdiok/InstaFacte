using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;
using MediatR;

namespace FactuTrust.Application.Features.Accounting.Commands;

/// <summary>
/// Répare une écriture d'à-nouveaux (« JAN ») contaminée pour l'exercice clôturé
/// <paramref name="FiscalYear"/> : extourne interne de l'écriture en écart puis régénération
/// (<c>IAccountingService.RepairOpeningEntriesAsync</c>). Idempotente : aucun écart détecté →
/// succès sans effet. Refuse si l'exercice est verrouillé.
/// </summary>
public sealed record RepairOpeningEntriesCommand(int FiscalYear) : IRequest<Result>;

public sealed class RepairOpeningEntriesCommandHandler : IRequestHandler<RepairOpeningEntriesCommand, Result>
{
    private readonly IAccountingService _accountingService;
    private readonly IFiscalYearLockService _lockService;
    private readonly ITenantUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;

    public RepairOpeningEntriesCommandHandler(
        IAccountingService accountingService,
        IFiscalYearLockService lockService,
        ITenantUnitOfWork unitOfWork,
        IAuditService auditService)
    {
        _accountingService = accountingService;
        _lockService = lockService;
        _unitOfWork = unitOfWork;
        _auditService = auditService;
    }

    public async Task<Result> Handle(RepairOpeningEntriesCommand request, CancellationToken cancellationToken)
    {
        var locks = await _lockService.GetLocksAsync(cancellationToken);
        if (locks.IsSuccess && locks.Value.Any(l => l.FiscalYear == request.FiscalYear))
        {
            return Result.Failure(Error.Conflict(
                $"L'exercice {request.FiscalYear} est verrouillé : l'à-nouveau ne peut pas être réparé."));
        }

        var result = await _unitOfWork.ExecuteAsync(
            ct => _accountingService.RepairOpeningEntriesAsync(request.FiscalYear, ct),
            cancellationToken);

        if (result.IsSuccess)
        {
            await _auditService.LogAsync(
                AuditActions.Accounting.FiscalYearClosed,
                "JournalEntry",
                null,
                newValues: new { request.FiscalYear, Action = "OpeningEntriesRepaired" },
                cancellationToken: cancellationToken);
        }

        return result;
    }
}
