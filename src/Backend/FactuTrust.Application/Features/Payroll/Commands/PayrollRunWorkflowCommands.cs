using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Commands;

// ── Validate ──
public sealed record ValidatePayrollRunCommand(Guid RunId) : IRequest<Result>;

/// <summary>
/// Validation atomique : statut du cycle, avances soldées, acquisitions de congés et
/// écriture comptable de paie sont commités dans UNE transaction (unité de travail
/// ambiante). Un échec au milieu (y compris comptable) annule tout — plus d'états partiels.
/// </summary>
public sealed class ValidatePayrollRunCommandHandler : IRequestHandler<ValidatePayrollRunCommand, Result>
{
    private readonly IPayrollRunRepository _runs;
    private readonly IEmployeeAdvanceRepository _advances;
    private readonly ILeaveRequestRepository _leaves;
    private readonly ILeaveBalanceAccrualRepository _accruals;
    private readonly IAccountingService _accountingService;
    private readonly ITenantUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public ValidatePayrollRunCommandHandler(
        IPayrollRunRepository runs,
        IEmployeeAdvanceRepository advances,
        ILeaveRequestRepository leaves,
        ILeaveBalanceAccrualRepository accruals,
        IAccountingService accountingService,
        ITenantUnitOfWork unitOfWork,
        ICurrentUser currentUser)
    {
        _runs = runs;
        _advances = advances;
        _leaves = leaves;
        _accruals = accruals;
        _accountingService = accountingService;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(ValidatePayrollRunCommand request, CancellationToken cancellationToken)
    {
        return await _unitOfWork.ExecuteAsync(async ct =>
        {
            var run = await _runs.GetByIdWithPayslipsAsync(request.RunId, ct);
            if (run is null)
                return Result.Failure(Error.NotFound("PayrollRun", request.RunId));

            var employeeIds = run.Payslips.Select(p => p.EmployeeId).Distinct().ToList();
            var monthLeaves = await _leaves.ListForMonthAsync(run.Year, run.Month, ct);

            var validatedBy = _currentUser.Email ?? _currentUser.UserId?.ToString() ?? "system";
            var validateResult = run.Validate(validatedBy);
            if (validateResult.IsFailure)
                return validateResult;

            await _runs.UpdateScalarAsync(run, ct);

            // Requêtes batch (une par table au lieu d'une par salarié/avance).
            var outstanding = await _advances.ListOutstandingByEmployeeIdsAsync(employeeIds, ct);
            foreach (var advance in outstanding)
                advance.Settle(run.Id);
            await _advances.UpdateRangeAsync(outstanding, ct);

            var employeesWithAccrual = (await _accruals.GetByEmployeePeriodsAsync(employeeIds, run.Year, run.Month, ct))
                .Select(a => a.EmployeeId)
                .ToHashSet();

            var newAccruals = new List<LeaveBalanceAccrual>();
            foreach (var employeeId in employeeIds)
            {
                if (employeesWithAccrual.Contains(employeeId))
                    continue;

                var unpaidDays = monthLeaves
                    .Where(l => l.EmployeeId == employeeId && l.Type.ReducesGross())
                    .Sum(l => l.Days);
                var workedDays = LeaveBalanceService.ComputeWorkedDays(unpaidDays);

                var accrualResult = LeaveBalanceAccrual.Create(employeeId, run.Year, run.Month, workedDays, run.Id);
                if (accrualResult.IsFailure)
                    return accrualResult;

                newAccruals.Add(accrualResult.Value);
            }

            await _accruals.AddRangeAsync(newAccruals, ct);

            // Transaction stricte : l'écriture comptable de paie fait partie de la validation.
            // (Skip-succès si le plan comptable n'est pas initialisé — comportement préservé.)
            var entryResult = await _accountingService.GeneratePayrollRunEntryAsync(run, ct);
            if (entryResult.IsFailure)
                return entryResult;

            return Result.Success();
        }, cancellationToken);
    }
}

// ── Reopen ──
public sealed record ReopenPayrollRunCommand(Guid RunId) : IRequest<Result>;

/// <summary>
/// Réouverture atomique : statut, dé-solde des avances et suppression des acquisitions
/// sont commités dans UNE transaction.
/// </summary>
public sealed class ReopenPayrollRunCommandHandler : IRequestHandler<ReopenPayrollRunCommand, Result>
{
    private readonly IPayrollRunRepository _runs;
    private readonly IEmployeeAdvanceRepository _advances;
    private readonly ILeaveBalanceAccrualRepository _accruals;
    private readonly ITenantUnitOfWork _unitOfWork;

    public ReopenPayrollRunCommandHandler(
        IPayrollRunRepository runs,
        IEmployeeAdvanceRepository advances,
        ILeaveBalanceAccrualRepository accruals,
        ITenantUnitOfWork unitOfWork)
    {
        _runs = runs;
        _advances = advances;
        _accruals = accruals;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(ReopenPayrollRunCommand request, CancellationToken cancellationToken)
    {
        return await _unitOfWork.ExecuteAsync(async ct =>
        {
            var run = await _runs.GetByIdAsync(request.RunId, ct);
            if (run is null)
                return Result.Failure(Error.NotFound("PayrollRun", request.RunId));

            var reopenResult = run.Reopen();
            if (reopenResult.IsFailure)
                return reopenResult;

            await _runs.UpdateScalarAsync(run, ct);

            // Requête ciblée (avant : chargement de TOUTE la table des avances + filtre en mémoire).
            var settled = await _advances.ListSettledByPayrollRunIdAsync(run.Id, ct);
            foreach (var advance in settled)
                advance.Unsettle();
            await _advances.UpdateRangeAsync(settled, ct);

            await _accruals.DeleteByPayrollRunIdAsync(run.Id, ct);

            return Result.Success();
        }, cancellationToken);
    }
}

// ── Close ──
public sealed record ClosePayrollRunCommand(Guid RunId) : IRequest<Result>;

public sealed class ClosePayrollRunCommandHandler : IRequestHandler<ClosePayrollRunCommand, Result>
{
    private readonly IPayrollRunRepository _runs;

    public ClosePayrollRunCommandHandler(IPayrollRunRepository runs)
    {
        _runs = runs;
    }

    public async Task<Result> Handle(ClosePayrollRunCommand request, CancellationToken cancellationToken)
    {
        var run = await _runs.GetByIdAsync(request.RunId, cancellationToken);
        if (run is null)
            return Result.Failure(Error.NotFound("PayrollRun", request.RunId));

        var closeResult = run.Close();
        if (closeResult.IsFailure)
            return closeResult;

        await _runs.UpdateScalarAsync(run, cancellationToken);
        return Result.Success();
    }
}
