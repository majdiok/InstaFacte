using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain.Payroll;

public sealed class PayrollRegimeAndFamilyTests
{
    [Fact]
    public void OvertimeLine_Create_FortyHoursRegime_ComputesWith173_33Divisor()
    {
        var result = PayrollOvertimeLine.Create(
            Guid.NewGuid(), 2026, 7, hours: 10m, ratePercent: 125m, baseSalary: 1000m,
            weeklyRegime: WeeklyWorkRegime.FortyHours);

        Assert.True(result.IsSuccess);
        // R-37 : diviseur 40 h exact 520/3 + arrondi final uniquement : 1000/(520/3)×10×1,25 = 72,115
        Assert.Equal(72.115m, result.Value.ComputedAmount);
    }

    [Fact]
    public void OvertimeLine_Create_FortyEightHoursRegime_Accepts175WithoutExtendedFlag()
    {
        var result = PayrollOvertimeLine.Create(
            Guid.NewGuid(), 2026, 7, hours: 2m, ratePercent: 175m, baseSalary: 2080m,
            weeklyRegime: WeeklyWorkRegime.FortyEightHours);

        Assert.True(result.IsSuccess);
        Assert.Equal(35m, result.Value.ComputedAmount); // 2080/208 = 10 ; × 2 × 1,75
    }

    [Fact]
    public void OvertimeLine_Update_KeepsHistoricalRateValid()
    {
        // Une ligne historique à 125 % reste éditable quel que soit le régime.
        var line = PayrollOvertimeLine.Create(
            Guid.NewGuid(), 2026, 7, 2m, 125m, 2080m,
            weeklyRegime: WeeklyWorkRegime.FortyEightHours).Value;

        var update = line.Update(3m, 125m, 2080m, weeklyRegime: WeeklyWorkRegime.FortyEightHours);

        Assert.True(update.IsSuccess);
        Assert.Equal(37.5m, line.ComputedAmount); // 10 × 3 × 1,25
    }

    [Fact]
    public void Contract_Create_DefaultsToFortyEightHoursRegime()
    {
        var contract = EmploymentContract.CreatePublic(
            Guid.NewGuid(), ContractType.Cdi, SocialRegime.Rsna,
            new DateTime(2026, 1, 1), 1000m, 0.4m);

        Assert.True(contract.IsSuccess);
        Assert.Equal(WeeklyWorkRegime.FortyEightHours, contract.Value.WeeklyRegime);
    }

    [Fact]
    public void Employee_Create_RejectsStudentPlusDisabledExceedingDependentChildren()
    {
        var result = Employee.Create(
            "EMP-001", "Sami", "Samou", new DateTime(2021, 4, 14),
            dependentChildren: 2, studentChildren: 2, disabledChildren: 1);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Employee_Create_RejectsMoreThanTwoDependentParents()
    {
        var result = Employee.Create(
            "EMP-002", "Sami", "Samou", new DateTime(2021, 4, 14),
            dependentParents: 3);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Employee_Create_AcceptsValidFamilyCounts()
    {
        var result = Employee.Create(
            "EMP-003", "Sami", "Samou", new DateTime(2021, 4, 14),
            dependentChildren: 4, studentChildren: 1, disabledChildren: 1, dependentParents: 2);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.StudentChildren);
        Assert.Equal(1, result.Value.DisabledChildren);
        Assert.Equal(2, result.Value.DependentParents);
    }
}
