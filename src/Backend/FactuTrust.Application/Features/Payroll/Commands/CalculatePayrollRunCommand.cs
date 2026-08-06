using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using FactuTrust.Application.Features.Payroll.Services;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Commands;

public sealed record CalculatePayrollRunCommand(Guid RunId, CalculatePayrollRunDto Dto)
    : IRequest<Result<CalculatePayrollRunResultDto>>;

public sealed class CalculatePayrollRunCommandHandler
    : IRequestHandler<CalculatePayrollRunCommand, Result<CalculatePayrollRunResultDto>>
{
    private readonly IPayrollRunRepository _runs;
    private readonly IEmployeeRepository _employees;
    private readonly IPayrollParametersRepository _parameters;
    private readonly ILeaveRequestRepository _leaves;
    private readonly IEmployeeAdvanceRepository _advances;
    private readonly IPayrollOvertimeRepository _overtime;
    private readonly IPayrollVariableAllowanceRepository _variableAllowances;
    private readonly IEmployeeSocialFundEnrollmentRepository _socialFundEnrollments;
    private readonly ISocialFundSchemeRepository _socialFundSchemes;
    private readonly IPayrollMealVoucherLineRepository _mealVouchers;
    private readonly IEmployeeInKindBenefitRepository _inKindBenefits;
    private readonly IEmployeeLoanRepository _loans;
    private readonly IEmployeeGarnishmentRepository _garnishments;
    private readonly IPayrollIrppRegularizationRepository _irppRegularizations;
    private readonly IEmployeePayrollSuspensionRepository _suspensions;
    private readonly IEmployeeDependentParentRepository _dependentParents;
    private readonly PayrollInputBuilder _inputBuilder;

    public CalculatePayrollRunCommandHandler(
        IPayrollRunRepository runs,
        IEmployeeRepository employees,
        IPayrollParametersRepository parameters,
        ILeaveRequestRepository leaves,
        IEmployeeAdvanceRepository advances,
        IPayrollOvertimeRepository overtime,
        IPayrollVariableAllowanceRepository variableAllowances,
        IEmployeeSocialFundEnrollmentRepository socialFundEnrollments,
        ISocialFundSchemeRepository socialFundSchemes,
        IPayrollMealVoucherLineRepository mealVouchers,
        IEmployeeInKindBenefitRepository inKindBenefits,
        IEmployeeLoanRepository loans,
        IEmployeeGarnishmentRepository garnishments,
        IPayrollIrppRegularizationRepository irppRegularizations,
        IEmployeePayrollSuspensionRepository suspensions,
        IEmployeeDependentParentRepository dependentParents,
        PayrollInputBuilder inputBuilder)
    {
        _runs = runs;
        _employees = employees;
        _parameters = parameters;
        _leaves = leaves;
        _advances = advances;
        _overtime = overtime;
        _variableAllowances = variableAllowances;
        _socialFundEnrollments = socialFundEnrollments;
        _socialFundSchemes = socialFundSchemes;
        _mealVouchers = mealVouchers;
        _inKindBenefits = inKindBenefits;
        _loans = loans;
        _garnishments = garnishments;
        _irppRegularizations = irppRegularizations;
        _suspensions = suspensions;
        _dependentParents = dependentParents;
        _inputBuilder = inputBuilder;
    }

    public async Task<Result<CalculatePayrollRunResultDto>> Handle(
        CalculatePayrollRunCommand request,
        CancellationToken cancellationToken)
    {
        var run = await _runs.GetByIdAsync(request.RunId, cancellationToken);
        if (run is null)
            return Result.Failure<CalculatePayrollRunResultDto>(Error.NotFound("PayrollRun", request.RunId));

        if (!run.Status.CanBeEdited())
            return Result.Failure<CalculatePayrollRunResultDto>(
                Error.Validation("Status", "Un cycle validé ou clôturé ne peut plus être recalculé."));

        var parameters = await _parameters.GetOrCreateForYearAsync(run.ParametersFiscalYear, cancellationToken);

        var employees = parameters.EnableAutomaticProrata
            ? await _employees.GetEligibleForPayrollMonthAsync(run.Year, run.Month, cancellationToken)
            : await _employees.GetActiveWithContractsAsync(cancellationToken);
        var employeeIds = employees.Select(e => e.Id).ToList();
        var referenceDate = new DateTime(run.Year, run.Month, 1).AddMonths(1).AddDays(-1);

        // Preflight non-cumul parents à charge (blocage strict si conflit CIN).
        var allActiveParentClaims = await _dependentParents.ListAllActiveAsync(cancellationToken);
        var employeeNames = employees.ToDictionary(e => e.Id, e => e.FullName);
        var conflictCheck = ParentDeductionEligibilityResolver.ValidateNoConflicts(allActiveParentClaims, employeeNames);
        if (conflictCheck.IsFailure)
            return Result.Failure<CalculatePayrollRunResultDto>(conflictCheck.Error);

        var claimsByEmployee = allActiveParentClaims
            .GroupBy(c => c.EmployeeId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<EmployeeDependentParent>)g.ToList());
        var cinIndex = allActiveParentClaims
            .GroupBy(c => c.ParentCin, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().EmployeeId, StringComparer.Ordinal);

        var warnings = new List<PayrollCalculationWarningDto>();

        var leaves = await _leaves.ListForMonthAsync(run.Year, run.Month, cancellationToken);
        var overtimeLines = await _overtime.ListForMonthAsync(run.Year, run.Month, cancellationToken);
        var variableAllowanceLines = await _variableAllowances.ListForMonthAsync(run.Year, run.Month, cancellationToken);
        var enrollments = await _socialFundEnrollments.ListActiveForEmployeesAsync(employeeIds, referenceDate, cancellationToken);
        var schemes = await _socialFundSchemes.ListAsync(cancellationToken: cancellationToken);
        var mealVoucherLines = await _mealVouchers.ListForMonthAsync(run.Year, run.Month, cancellationToken);
        var inKindBenefitLines = await _inKindBenefits.ListActiveForEmployeesAsync(employeeIds, referenceDate, cancellationToken);
        var loansDue = await _loans.ListWithDueInstallmentsForMonthAsync(run.Year, run.Month, cancellationToken);
        var activeGarnishments = await _garnishments.ListActiveForEmployeesAsync(employeeIds, referenceDate, cancellationToken);

        var regularizations = parameters.EnableIrppRegularization
            ? await _irppRegularizations.ListForMonthAsync(run.Year, run.Month, cancellationToken)
            : Array.Empty<PayrollIrppRegularization>();

        var monthSuspensions = parameters.EnableAutomaticProrata
            ? await _suspensions.ListForMonthAsync(run.Year, run.Month, cancellationToken)
            : Array.Empty<EmployeePayrollSuspension>();

        var batch = new PayrollInputBuilder.MonthBatchData
        {
            IrppRegularizations = regularizations,
            Suspensions = monthSuspensions,
            Enrollments = enrollments,
            Schemes = schemes.ToDictionary(s => s.Id),
            MealVouchers = mealVoucherLines,
            InKindBenefits = inKindBenefitLines,
            LoansWithDueInstallments = loansDue,
            ActiveGarnishments = activeGarnishments
        };

        var overtimeByEmployee = overtimeLines.GroupBy(l => l.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());
        var variableAllowancesByEmployee = variableAllowanceLines.GroupBy(l => l.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());

        var payslips = new List<Payslip>();
        foreach (var employee in employees)
        {
            var contract = parameters.EnableAutomaticProrata
                ? employee.GetContractForPayrollMonth(run.Year, run.Month)
                : employee.GetActiveContract(referenceDate);
            if (contract is null)
                continue;

            var employeeClaims = claimsByEmployee.GetValueOrDefault(employee.Id)
                ?? Array.Empty<EmployeeDependentParent>();
            var eligibility = ParentDeductionEligibilityResolver.ResolveForEmployee(
                employee.DependentParents,
                employeeClaims,
                cinIndex,
                employee.Id);

            if (eligibility.Status == ParentClaimsStatus.Incomplete && eligibility.WarningMessage is not null)
            {
                warnings.Add(new PayrollCalculationWarningDto
                {
                    Code = eligibility.WarningCode ?? ParentDeductionEligibilityResolver.IncompleteWarningCode,
                    Message = $"{employee.FullName} : {eligibility.WarningMessage}",
                    EmployeeId = employee.Id,
                    EmployeeName = employee.FullName
                });
            }

            decimal advanceTotal = 0m;
            if (request.Dto.SettleOutstandingAdvances)
            {
                var outstanding = await _advances.ListOutstandingAsync(employee.Id, cancellationToken);
                advanceTotal = outstanding.Sum(a => a.Amount);
            }

            var input = _inputBuilder.Build(
                employee,
                contract,
                leaves,
                advanceTotal,
                overtimeByEmployee.GetValueOrDefault(employee.Id, []),
                variableAllowancesByEmployee.GetValueOrDefault(employee.Id, []),
                parameters,
                batch,
                run.Year,
                run.Month,
                eligibility.EffectiveDependentParents);

            var (appliedEmployeeRate, appliedEmployerRate) = PayrollCalculator.ResolveCnssRates(input.Regime, parameters);
            var computation = PayrollCalculator.Compute(input, parameters);

            var payslip = Payslip.FromComputation(
                run.Id,
                employee.Id,
                employee.FullName,
                employee.EmployeeNumber,
                employee.CnssNumber,
                run.Year,
                run.Month,
                computation,
                appliedEmployeeRate,
                appliedEmployerRate,
                input.ProrataWorkedDays,
                input.ProrataNonWorkedDays,
                input.ProrataDeductionAmount);

            payslips.Add(payslip);
        }

        if (payslips.Count == 0)
            return Result.Failure<CalculatePayrollRunResultDto>(
                Error.Validation("Payslips", "Aucun salarié actif avec un contrat en cours pour ce mois."));

        var setResult = run.SetPayslips(payslips);
        if (setResult.IsFailure)
            return Result.Failure<CalculatePayrollRunResultDto>(setResult.Error);

        await _runs.PersistCalculationAsync(run, cancellationToken);

        return Result.Success(new CalculatePayrollRunResultDto
        {
            PayslipCount = payslips.Count,
            Warnings = warnings
        });
    }
}
