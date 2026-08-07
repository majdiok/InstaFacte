using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain.Payroll;

public sealed class AnnualBonusResolverTests
{
    [Fact]
    public void ComputeAmount_MonthsOfBase_ReturnsOneMonthSalary()
    {
        var rule = AnnualBonusRule.Create(
            "P13", "13e mois", AnnualBonusKind.ThirteenthMonth,
            AnnualBonusFormula.MonthsOfBase, 12, monthsOfBase: 1m).Value;

        var amount = AnnualBonusResolver.ComputeAmount(rule, null, 1500m);
        Assert.Equal(1500m, amount);
    }

    [Fact]
    public void ComputeAmount_PercentOfBase_AppliesRate()
    {
        var rule = AnnualBonusRule.Create(
            "ANC", "Ancienneté", AnnualBonusKind.Seniority,
            AnnualBonusFormula.PercentOfBase, 6, ratePercent: 5m).Value;

        var amount = AnnualBonusResolver.ComputeAmount(rule, null, 2000m);
        Assert.Equal(100m, amount);
    }

    [Fact]
    public void ResolveForMonth_PaymentMonthMatch_GeneratesLine()
    {
        var rule = AnnualBonusRule.Create(
            "P13", "13e mois", AnnualBonusKind.ThirteenthMonth,
            AnnualBonusFormula.MonthsOfBase, 12, monthsOfBase: 1m).Value;

        var employee = Employee.Create(
            "E001", "Ali", "Ben", new DateTime(2020, 1, 1), MaritalStatus.Single).Value;
        employee.AddContract(ContractType.Cdi, SocialRegime.Rsna, new DateTime(2020, 1, 1), 1800m, 0.4m);

        var lines = AnnualBonusResolver.ResolveForMonth(
            2026, 12, [rule], [], [employee]);

        Assert.Single(lines);
        Assert.Equal(1800m, lines[0].Amount);
        Assert.Equal(VariableAllowanceSource.AutoAnnualBonus, lines[0].Source);
    }
}
