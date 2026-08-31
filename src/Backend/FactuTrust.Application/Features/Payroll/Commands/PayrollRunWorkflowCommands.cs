using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    private readonly IFirmCollaboratorCostSyncService _collaboratorCostSync;
    private readonly FirmGovernanceOptions _firmGovernanceOptions;
    private readonly AccountingSettings _settings;
    private readonly ILogger<ValidatePayrollRunCommandHandler> _logger;

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
        ICurrentUser currentUser,
        IFirmCollaboratorCostSyncService collaboratorCostSync,
        IOptions<FirmGovernanceOptions> firmGovernanceOptions,
        IOptions<AccountingSettings> settings,
        ILogger<ValidatePayrollRunCommandHandler> logger)
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
        _collaboratorCostSync = collaboratorCostSync;
        _firmGovernanceOptions = firmGovernanceOptions.Value;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<Result> Handle(ValidatePayrollRunCommand request, CancellationToken cancellationToken)
    {
        var payrollYear = 0;
        var result = await _unitOfWork.ExecuteAsync(async ct =>
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

            // R-15 : fige le compte auxiliaire 425 de chaque bulletin à la validation (compte
            // déterministe et traçable ; refus si un matricule ne contient aucun chiffre).
            var freezeResult = run.FreezeEmployeeAuxiliaryAccounts();
            if (freezeResult.IsFailure)
                return freezeResult;

            await _runs.UpdateScalarAsync(run, ct);
            if (freezeResult.Value.Count > 0)
                await _runs.UpdatePayslipsAsync(freezeResult.Value, ct);

            // ── R-06 : solde figé par les lignes de déduction du bulletin ──
            // Business rule : une avance/échéance/saisie n'est soldée que si une ligne de
            // déduction figée du bulletin la référence (plus de solde forfaitaire tenant-wide).
            var strictSettlement = _settings.PayrollStrictSettlementEnabled;
            var calculatedAt = run.CalculatedAt ?? DateTime.UtcNow;
            var referenceDate = new DateTime(run.Year, run.Month, 1).AddMonths(1).AddDays(-1);

            // Snapshot des retenues figées sur les bulletins.
            var advanceWithheldByEmployee = run.Payslips
                .SelectMany(p => p.Lines
                    .Where(l => l.Kind == PayslipLineKind.Deduction && l.DeductionKind == DeductionKind.Advance)
                    .Select(l => (EmployeeId: p.EmployeeId, Amount: l.Amount)))
                .GroupBy(x => x.EmployeeId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

            var referencedInstallmentIds = run.Payslips
                .SelectMany(p => p.Lines
                    .Where(l => l.Kind == PayslipLineKind.Deduction
                        && l.DeductionKind == DeductionKind.Loan
                        && l.SourceEntityId.HasValue)
                    .Select(l => l.SourceEntityId!.Value))
                .ToHashSet();

            var hasLegacyGarnishmentLines = run.Payslips
                .SelectMany(p => p.Lines)
                .Any(l => l.Kind == PayslipLineKind.Deduction
                    && (l.DeductionKind == DeductionKind.Garnishment || l.DeductionKind == DeductionKind.Alimony)
                    && !l.SourceEntityId.HasValue);

            var frozenGarnishmentLines = run.Payslips
                .SelectMany(p => p.Lines
                    .Where(l => l.Kind == PayslipLineKind.Deduction
                        && (l.DeductionKind == DeductionKind.Garnishment || l.DeductionKind == DeductionKind.Alimony)))
                .ToList();

            // Requêtes batch (une par table) — réutilisées pour le garde-fou et le solde.
            var outstanding = await _advances.ListOutstandingByEmployeeIdsAsync(employeeIds, ct);
            var loansDue = await _loans.ListWithDueInstallmentsForMonthAsync(run.Year, run.Month, ct);
            var activeGarnishments = await _garnishments.ListActiveForEmployeesAsync(employeeIds, referenceDate, ct);

            // Garde-fou anti-staleness : une retenue créée après le calcul invalide le cycle
            // (fenêtre calculate→validate). CreatedAt est immuable — pas de faux positif sur les
            // réouvertures de cycles antérieurs (qui ne bumpent que UpdatedAt).
            if (strictSettlement &&
                (outstanding.Any(a => a.CreatedAt > calculatedAt)
                 || loansDue.Any(l => l.Installments.Any(i =>
                     i.Year == run.Year && i.Month == run.Month && !i.IsSettled && i.CreatedAt > calculatedAt))
                 || activeGarnishments.Any(g => g.CreatedAt > calculatedAt)))
            {
                return Result.Failure(Error.Validation(
                    "StaleDeductions",
                    "Des avances/prêts/saisies ont été créés ou modifiés après le calcul — recalculez le cycle."));
            }

            // ── Avances : la ligne agrégée « Avance sur salaire » ne porte pas de SourceEntityId,
            // on solde oldest-first à concurrence du montant retenu figé par salarié. ──
            var advancesToSettle = new List<EmployeeAdvance>();
            if (strictSettlement)
            {
                foreach (var empGroup in outstanding.GroupBy(a => a.EmployeeId))
                {
                    if (!advanceWithheldByEmployee.TryGetValue(empGroup.Key, out var withheld) || withheld <= 0)
                        continue; // rien retenu ce cycle → on ne solde rien (R-06)

                    var remaining = withheld;
                    foreach (var advance in empGroup.OrderBy(a => a.Date))
                    {
                        if (remaining <= 0)
                            break;
                        // M2 : le seuil de solde intégral se compare au reliquat (RemainingAmount), non au
                        // montant initial — une avance déjà partiellement soldée n'exige plus sa totalité
                        // pour être soldée, et ne doit pas avaler le budget disponible au détriment de
                        // l'avance suivante (qui se retrouverait affamée).
                        var reliquat = advance.RemainingAmount;
                        if (remaining >= reliquat - 0.001m)
                        {
                            // La retenue figée couvre intégralement le reliquat de cette avance. On capture
                            // le reliquat avant Settle — Settle() remet SettledAmount à Amount, ce qui
                            // annulerait RemainingAmount (et fausserait le budget de l'avance suivante).
                            advance.Settle(run.Id);
                            remaining -= reliquat;
                        }
                        else
                        {
                            // R-22 : le net disponible n'a permis de retenir qu'une partie de cette
                            // avance. On solde partiellement à concurrence du montant retenu ; le
                            // reliquat (RemainingAmount) reste dû et sera repris sur un cycle ultérieur.
                            var partialResult = advance.SettlePartial(run.Id, remaining);
                            if (partialResult.IsFailure)
                                return partialResult;
                            remaining = 0m;
                        }
                        advancesToSettle.Add(advance);
                    }

                    if (remaining > 0)
                        _logger.LogWarning(
                            "Avances du salarié {EmployeeId} soldées par correspondance de montant (ligne agrégée) — écart {Gap}",
                            empGroup.Key, remaining);
                }
            }
            else
            {
                // Repli incident : solde forfaitaire historique.
                foreach (var advance in outstanding)
                {
                    advance.Settle(run.Id);
                    advancesToSettle.Add(advance);
                }
            }
            await _advances.UpdateRangeAsync(advancesToSettle, ct);

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

            // ── Prêts : solde uniquement les échéances référencées par SourceEntityId (lignes typées).
            // H1 : plus de solde forfaitaire tenant-wide. Un cycle sans aucune ligne de prêt ne solde
            // rien — le bug historique soldait toutes les échéances dues du mois (tous salariés
            // confondus) dès qu'aucune ligne ne portait de SourceEntityId. Repli legacy (lignes de prêt
            // sans SourceEntityId, bulletins antérieurs au typage) : correspondance salarié + montant. ──
            var hasLoanDeductionLines = run.Payslips
                .SelectMany(p => p.Lines)
                .Any(l => l.Kind == PayslipLineKind.Deduction && l.DeductionKind == DeductionKind.Loan);

            var loanLineAmountsByEmployee = run.Payslips
                .SelectMany(p => p.Lines
                    .Where(l => l.Kind == PayslipLineKind.Deduction && l.DeductionKind == DeductionKind.Loan)
                    .Select(l => (EmployeeId: p.EmployeeId, l.Amount)))
                .ToList();

            // Repli legacy : lignes de prêt présentes mais sans SourceEntityId (vrais bulletins antérieurs).
            var legacyLoanFallback = strictSettlement && hasLoanDeductionLines && referencedInstallmentIds.Count == 0;
            var settleAllLoans = !strictSettlement || legacyLoanFallback;

            var loansToSettle = new List<EmployeeLoan>();
            foreach (var loan in loansDue)
            {
                var settledAny = false;
                foreach (var installment in loan.Installments
                    .Where(i => i.Year == run.Year && i.Month == run.Month && !i.IsSettled))
                {
                    if (settleAllLoans)
                    {
                        // Repli incident (!strictSettlement) : solde forfaitaire historique.
                        // Repli legacy : on ne solde que les échéances correspondant à une ligne figée du
                        // même salarié et de même montant — jamais toutes les échéances dues du tenant.
                        if (legacyLoanFallback && !loanLineAmountsByEmployee.Any(x =>
                                x.EmployeeId == loan.EmployeeId && Math.Abs(x.Amount - installment.Amount) <= 0.001m))
                            continue;
                    }
                    else if (!referencedInstallmentIds.Contains(installment.Id))
                    {
                        continue; // échéance non référencée par une ligne typée → non soldée (R-06)
                    }

                    loan.MarkInstallmentSettled(installment.Id, run.Id);
                    settledAny = true;
                }
                if (settledAny)
                    loansToSettle.Add(loan);
            }
            await _loans.UpdateRangeAsync(loansToSettle, ct);

            // ── Saisies : RecordInstallment depuis les montants figés au calcul (plus de recalcul
            // contre données courantes). Repli legacy (lignes sans SourceEntityId) : recalcul Allocate. ──
            var garnishmentsToUpdate = new List<EmployeeGarnishment>();
            if (strictSettlement && !hasLegacyGarnishmentLines)
            {
                var garnishmentsById = activeGarnishments.ToDictionary(g => g.Id);
                foreach (var line in frozenGarnishmentLines)
                {
                    if (!line.SourceEntityId.HasValue
                        || !garnishmentsById.TryGetValue(line.SourceEntityId.Value, out var garnishment))
                        continue;

                    garnishment.RecordInstallment(
                        run.Year,
                        run.Month,
                        run.Id,
                        line.RequestedAmount ?? line.Amount,
                        line.Amount,
                        line.CarriedOverAmount ?? 0m);
                    if (!garnishmentsToUpdate.Contains(garnishment))
                        garnishmentsToUpdate.Add(garnishment);
                }
            }
            else
            {
                var parameters = await _parameters.GetOrCreateForYearAsync(run.ParametersFiscalYear, ct);
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
            }

            await _garnishments.UpdateRangeAsync(garnishmentsToUpdate, ct);

            // Transaction stricte : l'écriture comptable de paie fait partie de la validation.
            // (Skip-succès si le plan comptable n'est pas initialisé — comportement préservé.)
            var entryResult = await _accountingService.GeneratePayrollRunEntryAsync(run, ct);
            if (entryResult.IsFailure)
                return entryResult;

            payrollYear = run.Year;
            return Result.Success();
        }, cancellationToken);

        if (result.IsSuccess && payrollYear > 0)
            await TrySyncCollaboratorCostsAfterPayrollValidateAsync(payrollYear, cancellationToken);

        return result;
    }

    private async Task TrySyncCollaboratorCostsAfterPayrollValidateAsync(int year, CancellationToken cancellationToken)
    {
        if (!FirmPayrollValidateCostSyncPolicy.ShouldSyncAfterValidate(_firmGovernanceOptions, _currentUser))
            return;

        var tenantId = _currentUser.TenantId!.Value;

        try
        {
            await _collaboratorCostSync.EnsureFreshAsync(
                tenantId,
                year,
                FirmCostSyncTrigger.PayrollValidate,
                forceImport: false,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Import automatique des coûts collaborateurs ignoré après validation paie (tenant {TenantId}, {Year})",
                tenantId,
                year);
        }
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
    private readonly ICnssContributionPaymentRepository _cnssPayments;
    private readonly IPayrollPaymentRepository _payments;
    private readonly IAccountingService _accountingService;
    private readonly ITenantUnitOfWork _unitOfWork;

    public ReopenPayrollRunCommandHandler(
        IPayrollRunRepository runs,
        IEmployeeAdvanceRepository advances,
        ILeaveBalanceAccrualRepository accruals,
        IEmployeeLoanRepository loans,
        IEmployeeGarnishmentRepository garnishments,
        ICnssContributionPaymentRepository cnssPayments,
        IPayrollPaymentRepository payments,
        IAccountingService accountingService,
        ITenantUnitOfWork unitOfWork)
    {
        _runs = runs;
        _advances = advances;
        _accruals = accruals;
        _loans = loans;
        _garnishments = garnishments;
        _cnssPayments = cnssPayments;
        _payments = payments;
        _accountingService = accountingService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(ReopenPayrollRunCommand request, CancellationToken cancellationToken)
    {
        return await _unitOfWork.ExecuteAsync(async ct =>
        {
            // R-39 : on charge les bulletins pour rendre vivant le garde-fou HasPayments de
            // PayrollRun.Reopen() (les paiements au niveau bulletin sont sinon invisibles).
            var run = await _runs.GetByIdWithPayslipsAsync(request.RunId, ct);
            if (run is null)
                return Result.Failure(Error.NotFound("PayrollRun", request.RunId));

            if (await _cnssPayments.HasActivePaymentForRunAsync(run.Id, ct))
            {
                return Result.Failure(Error.Validation(
                    "CnssPayment",
                    "Impossible de rouvrir : un versement CNSS actif existe pour ce cycle. Annulez-le d'abord."));
            }

            var activePayments = await _payments.ListByPayrollRunAsync(run.Id, includeCancelled: false, ct);
            if (activePayments.Count > 0)
            {
                return Result.Failure(Error.Validation(
                    "PayrollPayment",
                    "Impossible de rouvrir : des paiements de salaires actifs existent pour ce cycle. Annulez-les d'abord."));
            }

            var reopenResult = run.Reopen();
            if (reopenResult.IsFailure)
                return reopenResult;

            await _runs.UpdateScalarAsync(run, ct);

            // L'écriture OD doit être invalidée ici : sa génération est idempotente par source, donc
            // sans extourne la revalidation la laisserait figée sur les anciens montants.
            var reverseResult = await _accountingService.ReversePayrollRunEntryAsync(
                run.Id, $"Réouverture du cycle {run.Month:D2}/{run.Year}", ct);
            if (reverseResult.IsFailure)
                return reverseResult;

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
    private readonly IAccountingService _accountingService;
    private readonly ITenantUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly AccountingSettings _settings;

    public ClosePayrollRunCommandHandler(
        IPayrollRunRepository runs,
        IAccountingService accountingService,
        ITenantUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IOptions<AccountingSettings> settings)
    {
        _runs = runs;
        _accountingService = accountingService;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _settings = settings.Value;
    }

    public async Task<Result> Handle(ClosePayrollRunCommand request, CancellationToken cancellationToken)
    {
        // R-33 : la validation de l'OD brouillon fait partie de la clôture — atomique avec le
        // passage du statut (un échec de postage annule la clôture, plus d'OD orpheline).
        return await _unitOfWork.ExecuteAsync(async ct =>
        {
            var run = await _runs.GetByIdAsync(request.RunId, ct);
            if (run is null)
                return Result.Failure(Error.NotFound("PayrollRun", request.RunId));

            var closeResult = run.Close();
            if (closeResult.IsFailure)
                return closeResult;

            await _runs.UpdateScalarAsync(run, ct);

            // Mode Brouillard : l'OD de paie a été générée au brouillon à la validation. La
            // clôture est le signal métier de fin de mois — on valide l'OD dans la même transaction.
            if (_settings.BrouillardEnabled)
            {
                var validatedBy = _currentUser.Email ?? _currentUser.UserId?.ToString() ?? "system";
                var postResult = await _accountingService.EnsurePayrollRunEntryPostedAsync(run.Id, validatedBy, ct);
                if (postResult.IsFailure)
                    return postResult;
            }

            return Result.Success();
        }, cancellationToken);
    }
}
