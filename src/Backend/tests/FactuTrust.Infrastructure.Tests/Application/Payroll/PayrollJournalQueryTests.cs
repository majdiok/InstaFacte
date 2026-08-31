using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Features.Payroll.Reports;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application.Payroll;

/// <summary>
/// Journal de paie : détail par salarié et ventilation comptable OD (écriture réelle si le
/// cycle est comptabilisé, simulation à partir des totaux figés sinon).
/// </summary>
public sealed class PayrollJournalQueryTests
{
    private static readonly Guid EmpAId = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
    private static readonly Guid EmpBId = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");

    private static void SetEntityId(object entity, Guid id) =>
        typeof(FactuTrust.Domain.Common.Entity)
            .GetProperty(nameof(FactuTrust.Domain.Common.Entity.Id))!
            .SetValue(entity, id);

    private static PayrollRun BuildRun(PayrollRunStatus status, params (Guid Id, string Name, string Number, decimal Salary)[] employees)
    {
        var run = PayrollRun.Create(2026, 3, 2026).Value;
        SetEntityId(run, Guid.Parse("dddddddd-4444-4444-4444-444444444444"));

        if (status == PayrollRunStatus.Draft)
            return run;

        var parameters = PayrollParameterDefaults.CreateDefaults(2026).Value;
        var payslips = employees.Select(e =>
        {
            var input = new PayrollComputationInput
            {
                BaseSalary = e.Salary,
                Regime = SocialRegime.Rsna,
                WorkAccidentRate = 0.4m
            };
            var computation = PayrollCalculator.Compute(input, parameters);
            var (empRate, employerRate) = PayrollCalculator.ResolveCnssRates(input.Regime, parameters);
            return Payslip.FromComputation(run.Id, e.Id, e.Name, e.Number, "1234567890", 2026, 3, computation, empRate, employerRate);
        }).ToList();

        run.SetPayslips(payslips);
        if (status is PayrollRunStatus.Validated or PayrollRunStatus.Closed)
        {
            run.Validate("tester");
            if (status == PayrollRunStatus.Closed)
                run.Close();
        }

        return run;
    }

    private static GeneratePayrollJournalQueryHandler CreateHandler(PayrollRun? run, JournalEntry? postedEntry = null)
    {
        var runs = new Mock<IPayrollRunRepository>();
        runs.Setup(r => r.GetByPeriodWithPayslipsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(run);

        var entries = new Mock<IJournalEntryRepository>();
        // R-27 : la requête lit désormais l'écriture active (filtre les extournées).
        entries.Setup(e => e.GetActiveBySourceAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(postedEntry);

        var settings = Microsoft.Extensions.Options.Options.Create(new FactuTrust.Application.Configuration.AccountingSettings());

        return new GeneratePayrollJournalQueryHandler(runs.Object, entries.Object, settings);
    }

    private static JournalEntry BuildPostedEntry(PayrollRun run)
    {
        var lines = PayrollJournalEntryBuilder.BuildLines(
            run.TotalGross,
            run.TotalNet,
            run.TotalCnssEmployee,
            run.TotalCnssEmployer,
            run.TotalIrpp,
            run.TotalCss,
            run.TotalTfp,
            run.TotalFoprolos,
            run.TotalWorkAccident,
            run.TotalOtherDeductions,
            $"Paie {run.Month:D2}/{run.Year}",
            run.TotalIrppRegularization,
            run.TotalCssRegularization,
            run.TotalCssEmployer).Value;

        return JournalEntry.Create(
            42,
            "JOD",
            new DateTime(2026, 3, 31),
            $"Paie {run.Month:D2}/{run.Year}",
            Guid.NewGuid(),
            true,
            "PayrollRun",
            run.Id,
            lines).Value;
    }

    [Fact]
    public async Task NoRunForPeriod_ReturnsNotFound()
    {
        var handler = CreateHandler(run: null);

        var result = await handler.Handle(new GeneratePayrollJournalQuery(2026, 3), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("NotFound", result.Error.Code);
    }

    [Fact]
    public async Task DraftRun_ReturnsConflict()
    {
        var handler = CreateHandler(BuildRun(PayrollRunStatus.Draft));

        var result = await handler.Handle(new GeneratePayrollJournalQuery(2026, 3), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);
    }

    [Fact]
    public async Task CalculatedRun_IsRefusedUnlessExplicitlyIncluded()
    {
        var run = BuildRun(PayrollRunStatus.Calculated, (EmpAId, "Ahmed Ben Ali", "EMP001", 2000m));
        var handler = CreateHandler(run);

        var refused = await handler.Handle(new GeneratePayrollJournalQuery(2026, 3), CancellationToken.None);
        Assert.True(refused.IsFailure);
        Assert.Equal("Conflict", refused.Error.Code);

        var accepted = await handler.Handle(new GeneratePayrollJournalQuery(2026, 3, IncludeCalculated: true), CancellationToken.None);
        Assert.True(accepted.IsSuccess);
        Assert.True(accepted.Value.IsProvisional);
    }

    [Fact]
    public async Task ValidatedRun_ProjectsEmployeeLinesFromFrozenPayslips()
    {
        var run = BuildRun(PayrollRunStatus.Validated,
            (EmpBId, "Sonia Trabelsi", "EMP002", 1500m),
            (EmpAId, "Ahmed Ben Ali", "EMP001", 2000m));
        var handler = CreateHandler(run);

        var result = await handler.Handle(new GeneratePayrollJournalQuery(2026, 3), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = result.Value;

        Assert.Equal(2, dto.EmployeeCount);
        Assert.Equal("Ahmed Ben Ali", dto.Lines[0].EmployeeName);
        Assert.Equal("Sonia Trabelsi", dto.Lines[1].EmployeeName);
        Assert.False(dto.IsProvisional);

        // Les totaux de l'état sont ceux figés sur le cycle — pas de recalcul.
        Assert.Equal(run.TotalGross, dto.TotalGross);
        Assert.Equal(run.TotalNet, dto.TotalNetSalary);
        Assert.Equal(run.TotalIrpp, dto.TotalIrpp);
        Assert.Equal(dto.Lines.Sum(l => l.GrossSalary), dto.TotalGross);
        Assert.Equal(dto.Lines.Sum(l => l.NetSalary), dto.TotalNetSalary);
    }

    [Fact]
    public async Task EmployeeLine_ExposesEmployerChargesAndCost()
    {
        var run = BuildRun(PayrollRunStatus.Validated, (EmpAId, "Ahmed Ben Ali", "EMP001", 2000m));
        var handler = CreateHandler(run);

        var result = await handler.Handle(new GeneratePayrollJournalQuery(2026, 3), CancellationToken.None);

        var line = Assert.Single(result.Value.Lines);
        var payslip = Assert.Single(run.Payslips);

        Assert.Equal(
            payslip.CnssEmployer + payslip.WorkAccidentContribution + payslip.Tfp + payslip.Foprolos + payslip.CssEmployer,
            line.TotalEmployerCharges);
        Assert.Equal(payslip.GrossSalary + line.TotalEmployerCharges, line.TotalCost);
    }

    [Fact]
    public async Task NotPostedRun_SimulatesBalancedAccountingSplit()
    {
        var run = BuildRun(PayrollRunStatus.Validated, (EmpAId, "Ahmed Ben Ali", "EMP001", 2000m));
        var handler = CreateHandler(run); // aucune écriture comptable

        var result = await handler.Handle(new GeneratePayrollJournalQuery(2026, 3), CancellationToken.None);
        var dto = result.Value;

        Assert.False(dto.AccountingLinesArePosted);
        Assert.Null(dto.AccountingEntryNumber);
        Assert.NotEmpty(dto.AccountingLines);
        Assert.True(dto.IsBalanced);
        Assert.Equal(dto.TotalDebit, dto.TotalCredit);

        // Comptes attendus de l'OD de paie.
        Assert.Contains(dto.AccountingLines, l => l.AccountNumber == PayrollJournalEntryBuilder.SalaryAccount && l.Debit == run.TotalGross);
        Assert.Contains(dto.AccountingLines, l => l.AccountNumber == PayrollJournalEntryBuilder.PersonnelPayableAccount && l.Credit == run.TotalNet);
        Assert.Contains(dto.AccountingLines, l => l.AccountNumber == PayrollJournalEntryBuilder.EmployerChargesAccount);
        Assert.Contains(dto.AccountingLines, l => l.AccountNumber == PayrollJournalEntryBuilder.SocialOrgAccount);

        // Bucket nul (aucune avance) : la ligne 421 est omise.
        Assert.DoesNotContain(dto.AccountingLines, l => l.AccountNumber == PayrollJournalEntryBuilder.AdvancesAccount);
    }

    [Fact]
    public async Task AccountingLines_CarryReadableAccountLabels()
    {
        var run = BuildRun(PayrollRunStatus.Validated, (EmpAId, "Ahmed Ben Ali", "EMP001", 2000m));
        var handler = CreateHandler(run);

        var result = await handler.Handle(new GeneratePayrollJournalQuery(2026, 3), CancellationToken.None);

        var salary = Assert.Single(result.Value.AccountingLines, l => l.AccountNumber == PayrollJournalEntryBuilder.SalaryAccount);
        Assert.Equal("Charges de personnel", salary.AccountLabel);
    }

    [Fact]
    public async Task PostedRun_ReflectsTheRealJournalEntry()
    {
        var run = BuildRun(PayrollRunStatus.Closed, (EmpAId, "Ahmed Ben Ali", "EMP001", 2000m));
        var handler = CreateHandler(run, BuildPostedEntry(run));

        var result = await handler.Handle(new GeneratePayrollJournalQuery(2026, 3), CancellationToken.None);
        var dto = result.Value;

        Assert.True(dto.AccountingLinesArePosted);
        Assert.Equal(42, dto.AccountingEntryNumber);
        Assert.Equal("JOD", dto.AccountingJournalCode);
        Assert.Equal(new DateTime(2026, 3, 31), dto.AccountingEntryDate);
        Assert.True(dto.IsBalanced);
        Assert.Contains(dto.AccountingLines, l => l.AccountNumber == PayrollJournalEntryBuilder.SalaryAccount && l.Debit == run.TotalGross);
    }

    [Theory]
    [InlineData(1999, 3)]
    [InlineData(2026, 0)]
    [InlineData(2026, 13)]
    public async Task InvalidPeriod_Fails(int year, int month)
    {
        var handler = CreateHandler(run: null);

        var result = await handler.Handle(new GeneratePayrollJournalQuery(year, month), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.StartsWith("Validation.", result.Error.Code);
    }
}
