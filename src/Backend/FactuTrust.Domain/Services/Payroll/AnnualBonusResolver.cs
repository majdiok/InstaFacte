using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Services.Payroll;

/// <summary>
/// Résout les primes annuelles à verser sur un mois de paie donné.
/// </summary>
public static class AnnualBonusResolver
{
    public sealed record ResolvedLine(
        Guid EmployeeId,
        Guid RuleId,
        string Label,
        decimal Amount,
        bool Taxable,
        bool SubjectToCnss,
        VariableAllowanceSource Source);

    public static decimal ComputeAmount(
        AnnualBonusRule rule,
        EmployeeAnnualBonusRule? assignment,
        decimal baseSalary,
        int seniorityYears = 0)
    {
        ArgumentNullException.ThrowIfNull(rule);

        var formula = rule.Formula;
        var fixedAmount = assignment?.OverrideFixedAmount ?? rule.FixedAmount;
        var ratePercent = assignment?.OverrideRatePercent ?? rule.RatePercent;
        var monthsOfBase = assignment?.OverrideMonthsOfBase ?? rule.MonthsOfBase;

        return formula switch
        {
            AnnualBonusFormula.FixedAmount => R(fixedAmount),
            AnnualBonusFormula.PercentOfBase => R(baseSalary * ratePercent / 100m),
            AnnualBonusFormula.MonthsOfBase => R(baseSalary * monthsOfBase),
            _ => 0m
        };
    }

    public static IReadOnlyList<ResolvedLine> ResolveForMonth(
        int year,
        int month,
        IReadOnlyList<AnnualBonusRule> rules,
        IReadOnlyList<EmployeeAnnualBonusRule> assignments,
        IReadOnlyList<Employee> employees)
    {
        var activeRules = rules
            .Where(r => r.IsActive && r.PaymentMonth == month && (r.FiscalYear is null || r.FiscalYear == year))
            .ToList();
        if (activeRules.Count == 0)
            return Array.Empty<ResolvedLine>();

        var rulesById = activeRules.ToDictionary(r => r.Id);
        var lines = new List<ResolvedLine>();

        foreach (var employee in employees)
        {
            var contract = employee.GetActiveContract(new DateTime(year, month, 1).AddMonths(1).AddDays(-1))
                ?? employee.GetContractForPayrollMonth(year, month);
            if (contract is null)
                continue;

            var seniorityYears = TerminationIndemnityCalculator.CountFullMonths(
                employee.HireDate, new DateTime(year, month, 1)) / 12;

            var employeeAssignments = assignments
                .Where(a => a.EmployeeId == employee.Id && a.IsActive && rulesById.ContainsKey(a.AnnualBonusRuleId))
                .ToList();

            if (employeeAssignments.Count > 0)
            {
                foreach (var assignment in employeeAssignments)
                {
                    var rule = rulesById[assignment.AnnualBonusRuleId];
                    var amount = ComputeAmount(rule, assignment, contract.BaseSalary, seniorityYears);
                    if (amount <= 0m)
                        continue;
                    lines.Add(new ResolvedLine(
                        employee.Id, rule.Id, rule.Label, amount, rule.Taxable, rule.SubjectToCnss,
                        VariableAllowanceSource.AutoAnnualBonus));
                }
            }
            else
            {
                foreach (var rule in activeRules.Where(r => r.Kind == AnnualBonusKind.ThirteenthMonth))
                {
                    var amount = ComputeAmount(rule, null, contract.BaseSalary, seniorityYears);
                    if (amount <= 0m)
                        continue;
                    lines.Add(new ResolvedLine(
                        employee.Id, rule.Id, rule.Label, amount, rule.Taxable, rule.SubjectToCnss,
                        VariableAllowanceSource.AutoAnnualBonus));
                }
            }
        }

        return lines;
    }

    private static decimal R(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}
