using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Payroll.Services;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using Microsoft.Extensions.Options;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application.Payroll;

public sealed class PayrollProrataIntegrationTests
{
    private static PayrollYearParameters ParamsWithProrata(bool enabled)
    {
        var created = PayrollParameterDefaults.CreateDefaults(2026);
        Assert.True(created.IsSuccess);
        var parameters = created.Value;
        if (!enabled)
            return parameters;

        var update = parameters.UpdateRates(
            parameters.CnssEmployeeRate,
            parameters.CnssEmployerRate,
            parameters.CssRate,
            parameters.CssAnnualExemptionThreshold,
            parameters.ProfessionalExpensesRate,
            parameters.ProfessionalExpensesAnnualCap,
            parameters.HeadOfFamilyAnnualDeduction,
            parameters.ChildAnnualDeduction,
            parameters.MaxDeductibleChildren,
            parameters.TfpRateIndustry,
            parameters.TfpRateOther,
            parameters.FoprolosRate,
            parameters.MonthlySmig,
            parameters.CnssEmployeeRateRsa,
            parameters.CnssEmployerRateRsa,
            parameters.EnforceSmigOnContracts,
            parameters.EnableExtendedOvertimeRates,
            parameters.EnableAllowanceQuadrantMatrix,
            enableIrppRegularization: parameters.EnableIrppRegularization,
            enableAutomaticProrata: true);
        Assert.True(update.IsSuccess);
        return parameters;
    }

    [Fact]
    public void Build_ProrataDisabled_HasZeroProrataDeduction()
    {
        var (employee, contract) = CreateEmployeeWithContract(new DateTime(2026, 3, 16));
        var builder = new PayrollInputBuilder(Options.Create(new AccountingSettings()));

        var input = builder.Build(
            employee,
            contract,
            Array.Empty<LeaveRequest>(),
            0m,
            [],
            [],
            ParamsWithProrata(false),
            new PayrollInputBuilder.MonthBatchData(),
            2026,
            3);

        Assert.Equal(0m, input.ProrataDeductionAmount);
    }

    [Fact]
    public void Build_MidMonthHire_AppliesProrataDeduction()
    {
        var (employee, contract) = CreateEmployeeWithContract(new DateTime(2026, 3, 16));
        var builder = new PayrollInputBuilder(Options.Create(new AccountingSettings()));

        var input = builder.Build(
            employee,
            contract,
            Array.Empty<LeaveRequest>(),
            0m,
            [],
            [],
            ParamsWithProrata(true),
            new PayrollInputBuilder.MonthBatchData(),
            2026,
            3);

        Assert.True(input.ProrataDeductionAmount > 0);
        Assert.True(input.ProrataNonWorkedDays > 0);
    }

    [Fact]
    public void Compute_WithProrata_ProducesDistinctPayslipLine()
    {
        var input = new PayrollComputationInput
        {
            BaseSalary = 2600m,
            ProrataDeductionAmount = 500m,
            Regime = SocialRegime.Rsna,
            WorkAccidentRate = 0.4m
        };

        var computation = PayrollCalculator.Compute(input, ParamsWithProrata(false));
        Assert.Contains(computation.Lines, l => l.Label.Contains("Prorata embauche", StringComparison.Ordinal));
    }

    private static (Employee Employee, EmploymentContract Contract) CreateEmployeeWithContract(DateTime hireDate)
    {
        var employee = Employee.Create("EMP-PRORATA", "Dupont", "Jean", hireDate).Value;
        var add = employee.AddContract(ContractType.Cdi, SocialRegime.Rsna, hireDate, 2600m, 0.4m);
        Assert.True(add.IsSuccess);
        return (employee, add.Value);
    }
}
