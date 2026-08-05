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
    private readonly IEmployeeLoanRepository _loans;
    private readonly IEmployeeGarnishmentRepository _garnishments;
    private readonly IPayrollParametersRepository _parameters;
    private readonly IAccountingService _accountingService;
    private readonly ITenantUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public ValidatePayrollRunCommandHandler(
        IPayrollRunRepository runs,
        IEmployeeAdvanceRepository advances,
        ILeaveRequestRepository leaves,
        ILeaveBalanceAccrualRepository accruals,
        IEmployeeLoanRepository loans,
        IEmployeeGarnishmentRepository garnishments,
        IPayrollParametersRepository parameters,
        IAccountingService accountingService,
        ITenantUnitOfWork unitOfWork,
        ICurrentUser currentUser)
    {
        _runs = runs;
        _advances = advances;
        _leaves = leaves;
        _accruals = accruals;
        _loans = loans;
        _garnishments = garnishments;
        _parameters = parameters;
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

            var loansDue = await _loans.ListWithDueInstallmentsForMonthAsync(run.Year, run.Month, ct);
            foreach (var loan in loansDue)
            {
                foreach (var installment in loan.Installments
                    .Where(i => i.Year == run.Year && i.Month == run.Month && !i.IsSettled))
                    loan.MarkInstallmentSettled(installment.Id, run.Id);
            }
            await _loans.UpdateRangeAsync(loansDue, ct);

            var referenceDate = new DateTime(run.Year, run.Month, 1).AddMonths(1).AddDays(-1);
            var parameters = await _parameters.GetOrCreateForYearAsync(run.ParametersFiscalYear, ct);
            var activeGarnishments = await _garnishments.ListActiveForEmployeesAsync(employeeIds, referenceDate, ct);
            var garnishmentsToUpdate = new List<EmployeeGarnishment>();

            foreach (var payslip in run.Payslips)
            {
                var postTaxTotal = payslip.Lines
                    .Where(l => l.Kind == PayslipLineKind.Deduction
                        && l.DeductionKind is DeductionKind.Garnishment or DeductionKind.Alimony)
                    .Sum(l => l.Amount);
                if (postTaxTotal <= 0)
                    continue;

                var netBeforeGarnishments = payslip.NetSalary + postTaxTotal;
                var employeeGarnishments = activeGarnishments.Where(g => g.EmployeeId == payslip.EmployeeId).ToList();
                if (employeeGarnishments.Count == 0)
                    continue;

                var hasAlimony = employeeGarnishments.Any(g => g.Type == GarnishmentType.Alimony);
                var available = GarnishmentCalculator.ComputeAvailableSeizable(
                    netBeforeGarnishments,
                    parameters.GarnishmentBrackets.ToList(),
                    hasAlimony);

                var requests = employeeGarnishments.Select(g => new GarnishmentCalculator.GarnishmentRequest(
                    g.Id,
                    g.Type,
                    g.BeneficiaryName,
                    g.Priority,
                    g.IssuedAt,
                    g.ComputeRequestedAmount(netBeforeGarnishments),
                    g.BeneficiaryRib)).ToList();

                var allocations = GarnishmentCalculator.Allocate(available, requests);
                foreach (var allocation in allocations.Where(a => a.AppliedAmount > 0))
                {
                    var garnishment = employeeGarnishments.First(g => g.Id == allocation.GarnishmentId);
                    garnishment.RecordInstallment(
                        run.Year,
                        run.Month,
                        run.Id,
                        allocation.RequestedAmount,
                        allocation.AppliedAmount,
                        allocation.CarriedOverAmount);
                    if (!garnishmentsToUpdate.Contains(garnishment))
                        garnishmentsToUpdate.Add(garnishment);
                }
            }

            await _garnishments.UpdateRangeAsync(garnishmentsToUpdate, ct);

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
    private readonly IEmployeeLoanRepository _loans;
    private readonly IEmployeeGarnishmentRepository _garnishments;
    private readonly ITenantUnitOfWork _unitOfWork;

    public ReopenPayrollRunCommandHandler(
        IPayrollRunRepository runs,
        IEmployeeAdvanceRepository advances,
        ILeaveBalanceAccrualRepository accruals,
        IEmployeeLoanRepository loans,
        IEmployeeGarnishmentRepository garnishments,
        ITenantUnitOfWork unitOfWork)
    {
        _runs = runs;
        _advances = advances;
        _accruals = accruals;
        _loans = loans;
        _garnishments = garnishments;
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

            var settledLoans = await _loans.ListWithSettledInstallmentsForRunAsync(run.Id, ct);
            foreach (var loan in settledLoans)
                loan.UnsettleInstallmentsForRun(run.Id);
            await _loans.UpdateRangeAsync(settledLoans, ct);

            var garnishmentsWithInstallments = await _garnishments.ListWithInstallmentsForRunAsync(run.Id, ct);
            foreach (var garnishment in garnishmentsWithInstallments)
                garnishment.RemoveInstallmentsForRun(run.Id);
            await _garnishments.UpdateRangeAsync(garnishmentsWithInstallments, ct);

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
