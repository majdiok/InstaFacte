using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Commands;

public sealed record CalculatePayrollRunCommand(Guid RunId, CalculatePayrollRunDto Dto) : IRequest<Result>;

public sealed class CalculatePayrollRunCommandHandler : IRequestHandler<CalculatePayrollRunCommand, Result>
{
    /// <summary>Base de jours ouvrables mensuels pour le calcul du taux journalier (convention tunisienne).</summary>
    private const decimal MonthlyWorkingDays = 26m;

    private readonly IPayrollRunRepository _runs;
    private readonly IEmployeeRepository _employees;
    private readonly IPayrollParametersRepository _parameters;
    private readonly ILeaveRequestRepository _leaves;
    private readonly IEmployeeAdvanceRepository _advances;
    private readonly IPayrollOvertimeRepository _overtime;

    public CalculatePayrollRunCommandHandler(
        IPayrollRunRepository runs,
        IEmployeeRepository employees,
        IPayrollParametersRepository parameters,
        ILeaveRequestRepository leaves,
        IEmployeeAdvanceRepository advances,
        IPayrollOvertimeRepository overtime)
    {
        _runs = runs;
        _employees = employees;
        _parameters = parameters;
        _leaves = leaves;
        _advances = advances;
        _overtime = overtime;
    }

    public async Task<Result> Handle(CalculatePayrollRunCommand request, CancellationToken cancellationToken)
    {
        var run = await _runs.GetByIdAsync(request.RunId, cancellationToken);
        if (run is null)
            return Result.Failure(Error.NotFound("PayrollRun", request.RunId));

        if (!run.Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Un cycle validé ou clôturé ne peut plus être recalculé."));

        var parameters = await _parameters.GetOrCreateForYearAsync(run.ParametersFiscalYear, cancellationToken);

        var employees = await _employees.GetActiveWithContractsAsync(cancellationToken);
        var leaves = await _leaves.ListForMonthAsync(run.Year, run.Month, cancellationToken);
        var overtimeLines = await _overtime.ListForMonthAsync(run.Year, run.Month, cancellationToken);
        var overtimeByEmployee = overtimeLines
            .GroupBy(l => l.EmployeeId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var referenceDate = new DateTime(run.Year, run.Month, 1).AddMonths(1).AddDays(-1);

        var payslips = new List<Payslip>();
        foreach (var employee in employees)
        {
            var contract = employee.GetActiveContract(referenceDate);
            if (contract is null)
                continue;

            // Outstanding advances are previewed as a net deduction; they are settled on validation.
            decimal advanceTotal = 0m;
            if (request.Dto.SettleOutstandingAdvances)
            {
                var outstanding = await _advances.ListOutstandingAsync(employee.Id, cancellationToken);
                advanceTotal = outstanding.Sum(a => a.Amount);
            }

            var input = BuildInput(
                employee,
                contract,
                leaves,
                advanceTotal,
                overtimeByEmployee.GetValueOrDefault(employee.Id, []),
                parameters);

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
                appliedEmployerRate);

            payslips.Add(payslip);
        }

        if (payslips.Count == 0)
            return Result.Failure(Error.Validation("Payslips", "Aucun salarié actif avec un contrat en cours pour ce mois."));

        var setResult = run.SetPayslips(payslips);
        if (setResult.IsFailure)
            return setResult;

        await _runs.PersistCalculationAsync(run, cancellationToken);
        return Result.Success();
    }

    private static PayrollComputationInput BuildInput(
        Employee employee,
        EmploymentContract contract,
        IReadOnlyList<LeaveRequest> monthLeaves,
        decimal otherDeductions,
        IReadOnlyList<PayrollOvertimeLine> overtimeLines,
        PayrollYearParameters parameters)
    {
        decimal taxableCnssable = 0m;
        decimal taxableOnly = 0m;
        decimal cnssOnly = 0m;
        decimal nonTaxable = 0m;

        if (parameters.EnableAllowanceQuadrantMatrix)
        {
            foreach (var allowance in contract.Allowances)
            {
                switch (allowance.Taxable, allowance.SubjectToCnss)
                {
                    case (true, true):
                        taxableCnssable += allowance.Amount;
                        break;
                    case (true, false):
                        taxableOnly += allowance.Amount;
                        break;
                    case (false, true):
                        cnssOnly += allowance.Amount;
                        break;
                    default:
                        nonTaxable += allowance.Amount;
                        break;
                }
            }
        }
        else
        {
            // Two-bucket allowance model (v1): a recurring allowance is treated as taxable + CNSS-able
            // only when it is both taxable and subject to CNSS; otherwise it is added to the net (non
            // taxable, non CNSS-able). Mixed cases are rare in Tunisian recurring primes.
            foreach (var allowance in contract.Allowances)
            {
                if (allowance.Taxable && allowance.SubjectToCnss)
                    taxableCnssable += allowance.Amount;
                else
                    nonTaxable += allowance.Amount;
            }
        }

        var unpaidDays = monthLeaves
            .Where(l => l.EmployeeId == employee.Id && l.Type.ReducesGross())
            .Sum(l => l.Days);
        var dailyRate = contract.BaseSalary / MonthlyWorkingDays;
        var unpaidAbsenceAmount = Math.Round(dailyRate * unpaidDays, 3, MidpointRounding.AwayFromZero);
        var overtimeAmount = overtimeLines.Sum(l => l.EffectiveAmount);

        return new PayrollComputationInput
        {
            BaseSalary = contract.BaseSalary,
            TaxableCnssableAllowances = taxableCnssable,
            TaxableOnlyAllowances = taxableOnly,
            CnssOnlyAllowances = cnssOnly,
            NonTaxableAllowances = nonTaxable,
            OvertimeAmount = overtimeAmount,
            UnpaidAbsenceAmount = unpaidAbsenceAmount,
            OtherDeductions = otherDeductions,
            Regime = contract.Regime,
            WorkAccidentRate = contract.WorkAccidentRate,
            // Le secteur (TFP 1 % industrie / 2 % autres) est un paramètre d'exercice persisté ;
            // le flag du DTO de calcul est déprécié et ignoré.
            IsIndustrialSector = parameters.IsIndustrialSector,
            IsHeadOfFamily = employee.IsHeadOfFamily,
            DependentChildren = employee.DependentChildren,
            StudentChildren = employee.StudentChildren,
            DisabledChildren = employee.DisabledChildren,
            DependentParents = employee.DependentParents
        };
    }
}
