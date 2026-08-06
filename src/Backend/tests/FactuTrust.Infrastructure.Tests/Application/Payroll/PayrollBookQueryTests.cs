using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Features.Payroll.Reports;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application.Payroll;

/// <summary>
/// Livre de paie simplifié : cumuls par salarié sur une plage de mois, lus depuis les
/// bulletins gelés (aucun recalcul).
/// </summary>
public sealed class PayrollBookQueryTests
{
    private static readonly Guid EmpAId = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
    private static readonly Guid EmpBId = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");

    private static void SetEntityId(object entity, Guid id) =>
        typeof(FactuTrust.Domain.Common.Entity)
            .GetProperty(nameof(FactuTrust.Domain.Common.Entity.Id))!
            .SetValue(entity, id);

    private static Payslip BuildPayslip(Guid runId, Guid employeeId, string name, string number, int month, decimal baseSalary)
    {
        var parameters = PayrollParameterDefaults.CreateDefaults(2026).Value;
        var input = new PayrollComputationInput
        {
            BaseSalary = baseSalary,
            Regime = SocialRegime.Rsna,
            WorkAccidentRate = 0.4m
        };
        var computation = PayrollCalculator.Compute(input, parameters);
        var (empRate, employerRate) = PayrollCalculator.ResolveCnssRates(input.Regime, parameters);

        return Payslip.FromComputation(
            runId, employeeId, name, number, "1234567890", 2026, month, computation, empRate, employerRate);
    }

    /// <summary>Cycle du mois demandé, amené jusqu'au statut voulu (Calculé par défaut).</summary>
    private static PayrollRun BuildRun(int month, PayrollRunStatus status, params (Guid Id, string Name, string Number, decimal Salary)[] employees)
    {
        var run = PayrollRun.Create(2026, month, 2026).Value;
        SetEntityId(run, Guid.NewGuid());

        var payslips = employees
            .Select(e => BuildPayslip(run.Id, e.Id, e.Name, e.Number, month, e.Salary))
            .ToList();

        run.SetPayslips(payslips);
        if (status is PayrollRunStatus.Validated or PayrollRunStatus.Closed)
        {
            run.Validate("tester");
            if (status == PayrollRunStatus.Closed)
                run.Close();
        }

        return run;
    }

    private static Employee BuildEmployee(Guid id, string number, string first, string last)
    {
        var employee = Employee.Create(number, first, last, new DateTime(2020, 1, 1)).Value;
        SetEntityId(employee, id);
        return employee;
    }

    private static (GeneratePayrollBookQueryHandler Handler, Mock<IPayrollRunRepository> Runs) CreateHandler(
        IReadOnlyList<PayrollRun> runs,
        IReadOnlyDictionary<Guid, Employee>? employees = null)
    {
        var runsRepo = new Mock<IPayrollRunRepository>();
        runsRepo
            .Setup(r => r.ListByMonthRangeWithPayslipsAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(runs);

        var employeesRepo = new Mock<IEmployeeRepository>();
        employeesRepo
            .Setup(e => e.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(employees ?? new Dictionary<Guid, Employee>());

        return (new GeneratePayrollBookQueryHandler(runsRepo.Object, employeesRepo.Object), runsRepo);
    }

    [Theory]
    [InlineData(1999, 1, 3)]
    [InlineData(2101, 1, 3)]
    [InlineData(2026, 0, 3)]
    [InlineData(2026, 1, 13)]
    [InlineData(2026, 5, 2)]
    public async Task InvalidPeriod_Fails(int year, int fromMonth, int toMonth)
    {
        var (handler, _) = CreateHandler(Array.Empty<PayrollRun>());

        var result = await handler.Handle(new GeneratePayrollBookQuery(year, fromMonth, toMonth), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.StartsWith("Validation.", result.Error.Code);
    }

    [Fact]
    public async Task CumulatesPayslipsAcrossMonths_PerEmployee()
    {
        var runs = new[]
        {
            BuildRun(1, PayrollRunStatus.Validated, (EmpAId, "Ahmed Ben Ali", "EMP001", 2000m), (EmpBId, "Sonia Trabelsi", "EMP002", 1500m)),
            BuildRun(2, PayrollRunStatus.Validated, (EmpAId, "Ahmed Ben Ali", "EMP001", 2000m)),
            BuildRun(3, PayrollRunStatus.Closed, (EmpAId, "Ahmed Ben Ali", "EMP001", 2000m), (EmpBId, "Sonia Trabelsi", "EMP002", 1500m))
        };
        var (handler, _) = CreateHandler(runs);

        var result = await handler.Handle(new GeneratePayrollBookQuery(2026, 1, 3), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = result.Value;

        Assert.Equal(2, dto.EmployeeCount);
        Assert.Equal(new[] { 1, 2, 3 }, dto.IncludedMonths);
        Assert.Empty(dto.MissingMonths);
        Assert.False(dto.IsProvisional);

        // Tri alphabétique sur le nom figé du bulletin.
        Assert.Equal("Ahmed Ben Ali", dto.Lines[0].EmployeeName);
        Assert.Equal("Sonia Trabelsi", dto.Lines[1].EmployeeName);

        Assert.Equal(3, dto.Lines[0].MonthsCount);
        Assert.Equal(2, dto.Lines[1].MonthsCount);

        // Le cumul d'un salarié est la somme de ses bulletins.
        var expectedGrossA = runs
            .SelectMany(r => r.Payslips)
            .Where(p => p.EmployeeId == EmpAId)
            .Sum(p => p.GrossSalary);
        Assert.Equal(expectedGrossA, dto.Lines[0].GrossSalary);

        // Le total de l'état est la somme des lignes, et recoupe les totaux des cycles.
        Assert.Equal(dto.Lines.Sum(l => l.GrossSalary), dto.TotalGross);
        Assert.Equal(runs.Sum(r => r.TotalGross), dto.TotalGross);
        Assert.Equal(runs.Sum(r => r.TotalNet), dto.TotalNetSalary);
        Assert.Equal(runs.Sum(r => r.TotalCnssEmployee), dto.TotalCnssEmployee);
    }

    [Fact]
    public async Task EmployerChargesAndCost_AreCumulated()
    {
        var runs = new[] { BuildRun(4, PayrollRunStatus.Validated, (EmpAId, "Ahmed Ben Ali", "EMP001", 2000m)) };
        var (handler, _) = CreateHandler(runs);

        var result = await handler.Handle(new GeneratePayrollBookQuery(2026, 4, 4), CancellationToken.None);

        var dto = result.Value;
        var run = runs[0];

        Assert.Equal(run.TotalCnssEmployer, dto.TotalCnssEmployer);
        Assert.Equal(run.TotalWorkAccident, dto.TotalWorkAccident);
        Assert.Equal(run.TotalTfp, dto.TotalTfp);
        Assert.Equal(run.TotalFoprolos, dto.TotalFoprolos);
        Assert.Equal(run.TotalCssEmployer, dto.TotalCssEmployer);
        Assert.Equal(
            run.TotalCnssEmployer + run.TotalWorkAccident + run.TotalTfp + run.TotalFoprolos + run.TotalCssEmployer,
            dto.TotalEmployerCharges);
        Assert.Equal(dto.TotalGross + dto.TotalEmployerCharges, dto.TotalEmployerCost);
    }

    [Fact]
    public async Task MissingMonths_AreReported()
    {
        var runs = new[] { BuildRun(1, PayrollRunStatus.Validated, (EmpAId, "Ahmed Ben Ali", "EMP001", 2000m)) };
        var (handler, _) = CreateHandler(runs);

        var result = await handler.Handle(new GeneratePayrollBookQuery(2026, 1, 3), CancellationToken.None);

        Assert.Equal(new[] { 1 }, result.Value.IncludedMonths);
        Assert.Equal(new[] { 2, 3 }, result.Value.MissingMonths);
    }

    [Fact]
    public async Task CalculatedRun_MarksPeriodAsProvisional()
    {
        var runs = new[]
        {
            BuildRun(1, PayrollRunStatus.Validated, (EmpAId, "Ahmed Ben Ali", "EMP001", 2000m)),
            BuildRun(2, PayrollRunStatus.Calculated, (EmpAId, "Ahmed Ben Ali", "EMP001", 2000m))
        };
        var (handler, _) = CreateHandler(runs);

        var result = await handler.Handle(new GeneratePayrollBookQuery(2026, 1, 2, IncludeCalculated: true), CancellationToken.None);

        Assert.True(result.Value.IsProvisional);
        Assert.Equal(new[] { 2 }, result.Value.ProvisionalMonths);
        Assert.True(result.Value.IncludeCalculated);
    }

    [Fact]
    public async Task IncludeCalculatedFlag_IsForwardedToRepository()
    {
        var (handler, runsRepo) = CreateHandler(Array.Empty<PayrollRun>());

        await handler.Handle(new GeneratePayrollBookQuery(2026, 1, 6, IncludeCalculated: true), CancellationToken.None);

        runsRepo.Verify(
            r => r.ListByMonthRangeWithPayslipsAsync(2026, 1, 6, true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EmployeeIdentity_IsEnrichedFromEmployeeRecord()
    {
        var runs = new[] { BuildRun(1, PayrollRunStatus.Validated, (EmpAId, "Ahmed Ben Ali", "EMP001", 2000m)) };
        var employee = BuildEmployee(EmpAId, "EMP001", "Ahmed", "Ben Ali");
        var (handler, _) = CreateHandler(runs, new Dictionary<Guid, Employee> { [EmpAId] = employee });

        var result = await handler.Handle(new GeneratePayrollBookQuery(2026, 1, 1), CancellationToken.None);

        Assert.Equal(new DateTime(2020, 1, 1), result.Value.Lines[0].HireDate);
    }

    [Fact]
    public async Task DeletedEmployee_KeepsLineWithFrozenIdentity()
    {
        var runs = new[] { BuildRun(1, PayrollRunStatus.Validated, (EmpAId, "Ahmed Ben Ali", "EMP001", 2000m)) };
        var (handler, _) = CreateHandler(runs); // aucune fiche salarié retournée

        var result = await handler.Handle(new GeneratePayrollBookQuery(2026, 1, 1), CancellationToken.None);

        var line = Assert.Single(result.Value.Lines);
        Assert.Equal("Ahmed Ben Ali", line.EmployeeName);
        Assert.Equal("EMP001", line.EmployeeNumber);
        Assert.Null(line.HireDate);
        Assert.Null(line.Cin);
    }

    [Fact]
    public async Task NoEligibleRun_ReturnsEmptyBook()
    {
        var (handler, _) = CreateHandler(Array.Empty<PayrollRun>());

        var result = await handler.Handle(new GeneratePayrollBookQuery(2026, 1, 12), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Lines);
        Assert.Equal(0, result.Value.EmployeeCount);
        Assert.Equal(0m, result.Value.TotalGross);
        Assert.Equal(Enumerable.Range(1, 12), result.Value.MissingMonths);
    }

    [Fact]
    public async Task PeriodLabel_UsesFrenchMonths()
    {
        var (handler, _) = CreateHandler(Array.Empty<PayrollRun>());

        var range = await handler.Handle(new GeneratePayrollBookQuery(2026, 1, 3), CancellationToken.None);
        var single = await handler.Handle(new GeneratePayrollBookQuery(2026, 3, 3), CancellationToken.None);

        Assert.Equal("Janvier à Mars 2026", range.Value.PeriodLabel);
        Assert.Equal("Mars 2026", single.Value.PeriodLabel);
    }
}
