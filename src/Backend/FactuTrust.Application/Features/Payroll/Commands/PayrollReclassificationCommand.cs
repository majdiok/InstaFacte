using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Commands;

// ── Reclassement SCE (plan §5.2.1 / WS-5) ──
// Outil firm-only : génère l'OD de reclassement d'un cycle paie (TFP/FOPROLOS, taxes, indemnités,
// compensation AN) dans UNE transaction. Idempotente côté AccountingService (refus du double).

/// <summary>
/// Génère l'OD de reclassement SCE d'un cycle paie validé/clôturé (outil de remédiation historique,
/// réservé firm). Retourne l'id de l'écriture créée.
/// </summary>
public sealed record GeneratePayrollReclassificationCommand(Guid RunId) : IRequest<Result<Guid>>;

public sealed class GeneratePayrollReclassificationCommandHandler
    : IRequestHandler<GeneratePayrollReclassificationCommand, Result<Guid>>
{
    private readonly IPayrollRunRepository _runs;
    private readonly IAccountingService _accountingService;
    private readonly ITenantUnitOfWork _unitOfWork;

    public GeneratePayrollReclassificationCommandHandler(
        IPayrollRunRepository runs,
        IAccountingService accountingService,
        ITenantUnitOfWork unitOfWork)
    {
        _runs = runs;
        _accountingService = accountingService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(GeneratePayrollReclassificationCommand request, CancellationToken cancellationToken)
    {
        return await _unitOfWork.ExecuteAsync(async ct =>
        {
            // M1 : le reclassement dérive la ventilation des indemnités (rupture vs ordinaires) et la
            // part de compensation d'avantage en nature des lignes figées des bulletins — payslips requis.
            var run = await _runs.GetByIdWithPayslipsAsync(request.RunId, ct);
            if (run is null)
                return Result.Failure<Guid>(Error.NotFound("PayrollRun", request.RunId));

            return await _accountingService.GeneratePayrollReclassificationEntryAsync(run, ct);
        }, cancellationToken);
    }
}
