using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Features.Payroll.BankTransfer;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application.Payroll;

public sealed class PayrollBankTransferExportTests
{
    private static readonly Guid RunId = Guid.Parse("dddddddd-4444-4444-4444-444444444444");
    private static readonly Guid EmpId = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");

    private static PayrollYearParameters Params() => PayrollParameterDefaults.CreateDefaults(2026).Value;

    private static void SetEntityId(object entity, Guid id) =>
        typeof(FactuTrust.Domain.Common.Entity)
            .GetProperty(nameof(FactuTrust.Domain.Common.Entity.Id))!
            .SetValue(entity, id);

    private static PayrollRun BuildRun(PayrollRunStatus targetStatus, decimal baseSalary = 2000m, string? rib = "20001234567890123456")
    {
        var run = PayrollRun.Create(2026, 8, 2026).Value;
        SetEntityId(run, RunId);

        var emp = Employee.Create("EMP001", "Ahmed", "Ben Ali", new DateTime(2020, 1, 1), rib: rib).Value;
        SetEntityId(emp, EmpId);

        var input = new PayrollComputationInput
        {
            BaseSalary = baseSalary,
            Regime = SocialRegime.Rsna,
            WorkAccidentRate = 0.4m
        };
        var parameters = Params();
        var computation = PayrollCalculator.Compute(input, parameters);
        var (empRate, employerRate) = PayrollCalculator.ResolveCnssRates(input.Regime, parameters);
        var payslip = Payslip.FromComputation(
            run.Id, EmpId, emp.FullName, emp.EmployeeNumber, "1234567890",
            2026, 8, computation, empRate, employerRate);

        if (targetStatus != PayrollRunStatus.Draft)
        {
            run.SetPayslips([payslip]);
            if (targetStatus is PayrollRunStatus.Validated or PayrollRunStatus.Closed)
            {
                run.Validate("tester");
                if (targetStatus == PayrollRunStatus.Closed)
                    run.Close();
            }
        }

        return run;
    }

    private static Employee BuildEmployee(string? rib = "20001234567890123456")
    {
        var emp = Employee.Create("EMP001", "Ahmed", "Ben Ali", new DateTime(2020, 1, 1), rib: rib).Value;
        SetEntityId(emp, EmpId);
        return emp;
    }

    private static (GeneratePayrollBankTransferQueryHandler Preview, ExportPayrollBankTransferQueryHandler Export)
        CreateHandlers(PayrollRun? run, Employee? employee)
    {
        var runs = new Mock<IPayrollRunRepository>();
        runs.Setup(r => r.GetByIdWithPayslipsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(run);

        var employees = new Mock<IEmployeeRepository>();
        var dict = employee is null
            ? new Dictionary<Guid, Employee>()
            : new Dictionary<Guid, Employee> { [employee.Id] = employee };
        employees.Setup(e => e.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dict);

        var companies = new Mock<ICompanyRepository>();
        companies.Setup(c => c.GetDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((Company?)null);

        var banks = new Mock<IBankAccountRepository>();
        var garnishments = new Mock<IEmployeeGarnishmentRepository>();
        garnishments.Setup(g => g.ListWithInstallmentsForRunAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<EmployeeGarnishment>());

        return (
            new GeneratePayrollBankTransferQueryHandler(runs.Object, employees.Object, companies.Object, banks.Object, garnishments.Object),
            new ExportPayrollBankTransferQueryHandler(runs.Object, employees.Object, companies.Object, banks.Object, garnishments.Object));
    }

    [Fact]
    public async Task Preview_Draft_Fails()
    {
        var run = BuildRun(PayrollRunStatus.Draft);
        var (preview, _) = CreateHandlers(run, BuildEmployee());
        var result = await preview.Handle(new GeneratePayrollBankTransferQuery(RunId), CancellationToken.None);
        Assert.True(result.IsFailure);
        Assert.Contains("validation", result.Error.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Preview_Calculated_Fails()
    {
        var run = BuildRun(PayrollRunStatus.Calculated);
        var (preview, _) = CreateHandlers(run, BuildEmployee());
        var result = await preview.Handle(new GeneratePayrollBankTransferQuery(RunId), CancellationToken.None);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Preview_Validated_Succeeds()
    {
        var run = BuildRun(PayrollRunStatus.Validated);
        var emp = BuildEmployee();
        var (preview, _) = CreateHandlers(run, emp);
        var result = await preview.Handle(new GeneratePayrollBankTransferQuery(RunId), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.EligibleCount);
        Assert.True(result.Value.TotalAmount > 0);
    }

    [Fact]
    public async Task Export_ValidatedWithoutEligible_Fails()
    {
        var run = BuildRun(PayrollRunStatus.Validated, rib: null);
        var emp = BuildEmployee(rib: null);
        var (_, export) = CreateHandlers(run, emp);
        var result = await export.Handle(new ExportPayrollBankTransferQuery(RunId), CancellationToken.None);
        Assert.True(result.IsFailure);
        Assert.Contains("éligible", result.Error.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Export_Validated_ReturnsCsvBytes()
    {
        var run = BuildRun(PayrollRunStatus.Validated);
        var emp = BuildEmployee();
        var (_, export) = CreateHandlers(run, emp);
        var result = await export.Handle(new ExportPayrollBankTransferQuery(RunId), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal("virement_paie_2026_08.csv", result.Value.FileName);
        Assert.True(result.Value.Content.Length > 3);
        Assert.Equal(0xEF, result.Value.Content[0]);
    }

    [Fact]
    public async Task Export_NotFound_Fails()
    {
        var (_, export) = CreateHandlers(null, null);
        var result = await export.Handle(new ExportPayrollBankTransferQuery(Guid.NewGuid()), CancellationToken.None);
        Assert.True(result.IsFailure);
        Assert.Contains("NotFound", result.Error.Code);
    }
}
