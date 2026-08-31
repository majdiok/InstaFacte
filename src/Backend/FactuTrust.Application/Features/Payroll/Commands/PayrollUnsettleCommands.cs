using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Commands;

// ── Unsettle review (plan §5.3 / WS-5) ──
// Outils firm-only : dé-solde ciblé d'une avance ou d'une échéance de prêt marquée « réglée » sans
// ligne de retenue figée correspondante (R-06). Chaque commande est gardée, auditée et transactionnelle.

/// <summary>
/// Dé-solde une avance salariée marquée réglée (outil firm-only de remédiation R-06).
/// </summary>
public sealed record UnsettleAdvanceCommand(Guid AdvanceId) : IRequest<Result>;

public sealed class UnsettleAdvanceCommandHandler : IRequestHandler<UnsettleAdvanceCommand, Result>
{
    private readonly IEmployeeAdvanceRepository _advances;
    private readonly ITenantUnitOfWork _unitOfWork;

    public UnsettleAdvanceCommandHandler(
        IEmployeeAdvanceRepository advances,
        ITenantUnitOfWork unitOfWork)
    {
        _advances = advances;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(UnsettleAdvanceCommand request, CancellationToken cancellationToken)
    {
        return await _unitOfWork.ExecuteAsync(async ct =>
        {
            var advance = await _advances.GetByIdAsync(request.AdvanceId, ct);
            if (advance is null)
                return Result.Failure(Error.NotFound("EmployeeAdvance", request.AdvanceId));

            if (!advance.IsSettled)
                return Result.Failure(Error.Validation(
                    "EmployeeAdvance",
                    "L'avance n'est pas marquée réglée — rien à dé-solder."));

            advance.Unsettle();
            await _advances.UpdateAsync(advance, ct);
            return Result.Success();
        }, cancellationToken);
    }
}

/// <summary>
/// Dé-solde une échéance de prêt salarié marquée réglée (outil firm-only de remédiation R-06).
/// </summary>
public sealed record UnsettleLoanInstallmentCommand(Guid InstallmentId) : IRequest<Result>;

public sealed class UnsettleLoanInstallmentCommandHandler : IRequestHandler<UnsettleLoanInstallmentCommand, Result>
{
    private readonly IEmployeeLoanRepository _loans;
    private readonly ITenantUnitOfWork _unitOfWork;

    public UnsettleLoanInstallmentCommandHandler(
        IEmployeeLoanRepository loans,
        ITenantUnitOfWork unitOfWork)
    {
        _loans = loans;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(UnsettleLoanInstallmentCommand request, CancellationToken cancellationToken)
    {
        return await _unitOfWork.ExecuteAsync(async ct =>
        {
            var loan = await _loans.GetByInstallmentIdAsync(request.InstallmentId, ct);
            if (loan is null)
                return Result.Failure(Error.NotFound("EmployeeLoanInstallment", request.InstallmentId));

            var result = loan.UnsettleInstallment(request.InstallmentId);
            if (result.IsFailure)
                return result;

            await _loans.UpdateAsync(loan, ct);
            return Result.Success();
        }, cancellationToken);
    }
}
