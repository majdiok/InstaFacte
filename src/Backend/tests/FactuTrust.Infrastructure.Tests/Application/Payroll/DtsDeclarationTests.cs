using System.Text;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Payroll.Declarations;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using MediatR;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application.Payroll;

public sealed class DtsDeclarationTests
{
    private static readonly Guid EmployeeId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private static PayrollYearParameters Params() => PayrollParameterDefaults.CreateDefaults(2026).Value;

    private static PayrollRun BuildRun(int year, int month, Guid employeeId, string name, string? cnss, decimal baseSalary)
    {
        var input = new PayrollComputationInput
        {
            BaseSalary = baseSalary,
            Regime = SocialRegime.Rsna,
            WorkAccidentRate = 0.4m
        };
        var parameters = Params();
        var computation = PayrollCalculator.Compute(input, parameters);
        var run = PayrollRun.Create(year, month, year).Value;
        var (empRate, empoyerRate) = PayrollCalculator.ResolveCnssRates(input.Regime, parameters);
        var payslip = Payslip.FromComputation(
            run.Id, employeeId, name, "EMP-001", cnss, year, month, computation,
            empRate, empoyerRate);
        run.SetPayslips([payslip]);
        return run;
    }

    private static GenerateDtsDeclarationQueryHandler CreateGenerateHandler(IReadOnlyList<PayrollRun> runs)
    {
        var repo = new Mock<IPayrollRunRepository>();
        repo.Setup(r => r.ListByQuarterWithPayslipsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(runs);
        return new GenerateDtsDeclarationQueryHandler(repo.Object);
    }

    [Fact]
    public async Task Generate_InvalidQuarter_Fails()
    {
        var handler = CreateGenerateHandler(Array.Empty<PayrollRun>());
        var result = await handler.Handle(new GenerateDtsDeclarationQuery(2026, 5), CancellationToken.None);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Generate_NoRuns_EmptyLinesAndAllMonthsMissing()
    {
        var handler = CreateGenerateHandler(Array.Empty<PayrollRun>());
        var result = await handler.Handle(new GenerateDtsDeclarationQuery(2026, 2), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = result.Value;
        Assert.Empty(dto.Lines);
        Assert.Equal(0, dto.EmployeeCount);
        Assert.Empty(dto.IncludedMonths);
        Assert.Equal(new[] { 4, 5, 6 }, dto.MissingMonths);
        Assert.False(dto.IsComplete);
        Assert.Equal(0m, dto.TotalGross);
        Assert.Equal(0m, dto.TotalCnssableGross);
    }

    [Fact]
    public async Task Generate_SingleMonthApril_PartialCompleteness()
    {
        var run = BuildRun(2026, 4, EmployeeId, "Alice Dupont", "1234567890", 2000m);
        var handler = CreateGenerateHandler([run]);

        var result = await handler.Handle(new GenerateDtsDeclarationQuery(2026, 2), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = result.Value;
        Assert.Equal(new[] { 4 }, dto.IncludedMonths);
        Assert.Equal(new[] { 5, 6 }, dto.MissingMonths);
        Assert.False(dto.IsComplete);
        Assert.Single(dto.Lines);
        Assert.Equal("Alice Dupont", dto.Lines[0].EmployeeName);
        Assert.Equal(1, dto.Lines[0].MonthsCount);
        Assert.Equal(run.Payslips.First().GrossSalary, dto.TotalGross);
        Assert.Equal(run.Payslips.First().CnssableGross, dto.TotalCnssableGross);
        Assert.Equal(run.Payslips.First().CnssEmployee, dto.TotalCnssEmployee);
        Assert.Equal(run.Payslips.First().CnssEmployer, dto.TotalCnssEmployer);
        Assert.Equal(dto.TotalCnssEmployee + dto.TotalCnssEmployer, dto.TotalContributions);
    }

    [Fact]
    public async Task Generate_ThreeMonths_IsComplete()
    {
        var runs = new[]
        {
            BuildRun(2026, 4, EmployeeId, "Alice Dupont", "1234567890", 2000m),
            BuildRun(2026, 5, EmployeeId, "Alice Dupont", "1234567890", 2000m),
            BuildRun(2026, 6, EmployeeId, "Alice Dupont", "1234567890", 2000m)
        };
        var handler = CreateGenerateHandler(runs);

        var result = await handler.Handle(new GenerateDtsDeclarationQuery(2026, 2), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = result.Value;
        Assert.True(dto.IsComplete);
        Assert.Equal(new[] { 4, 5, 6 }, dto.IncludedMonths);
        Assert.Empty(dto.MissingMonths);
        Assert.Single(dto.Lines);
        Assert.Equal(3, dto.Lines[0].MonthsCount);
        Assert.Equal(1, dto.EmployeeCount);

        var expectedGross = Math.Round(runs.Sum(r => r.Payslips.First().GrossSalary), 3, MidpointRounding.AwayFromZero);
        Assert.Equal(expectedGross, dto.TotalGross);
    }

    [Fact]
    public async Task Generate_SameEmployeeTwoMonths_AggregatesOneLine()
    {
        var runs = new[]
        {
            BuildRun(2026, 4, EmployeeId, "Alice Dupont", "1234567890", 1000m),
            BuildRun(2026, 5, EmployeeId, "Alice Dupont", "1234567890", 1500m)
        };
        var handler = CreateGenerateHandler(runs);

        var result = await handler.Handle(new GenerateDtsDeclarationQuery(2026, 2), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = result.Value;
        Assert.Single(dto.Lines);
        Assert.Equal(2, dto.Lines[0].MonthsCount);

        var expectedCnssable = Math.Round(
            runs.Sum(r => r.Payslips.First().CnssableGross), 3, MidpointRounding.AwayFromZero);
        Assert.Equal(expectedCnssable, dto.Lines[0].TotalCnssableGross);
        Assert.Equal(expectedCnssable, dto.TotalCnssableGross);
    }

    [Fact]
    public async Task Export_TotalRow_HasSevenAlignedColumns()
    {
        var dto = new DtsDeclarationDto
        {
            Year = 2026,
            Quarter = 2,
            TotalGross = 3000m,
            TotalCnssableGross = 2800m,
            TotalCnssEmployee = 257.040m,
            TotalCnssEmployer = 463.960m,
            TotalContributions = 721.000m,
            EmployeeCount = 1,
            IncludedMonths = new[] { 4, 5, 6 },
            MissingMonths = Array.Empty<int>(),
            IsComplete = true,
            Lines =
            [
                new DtsLineDto
                {
                    EmployeeId = EmployeeId,
                    EmployeeName = "Alice Dupont",
                    CnssNumber = "1234567890",
                    TotalGross = 3000m,
                    TotalCnssableGross = 2800m,
                    CnssEmployee = 257.040m,
                    CnssEmployer = 463.960m,
                    MonthsCount = 3
                }
            ]
        };

        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GenerateDtsDeclarationQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(dto));

        var handler = new ExportDtsDeclarationQueryHandler(mediator.Object);
        var result = await handler.Handle(new ExportDtsDeclarationQuery(2026, 2), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var bytes = result.Value;
        Assert.True(bytes.Length >= 3);
        Assert.Equal(0xEF, bytes[0]);
        Assert.Equal(0xBB, bytes[1]);
        Assert.Equal(0xBF, bytes[2]);

        var text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        Assert.Equal("Nom;CNSS;Brut total;Brut CNSS;CNSS salarié;CNSS patronale;Mois", lines[0]);

        var data = lines[1].Split(';');
        Assert.Equal(7, data.Length);
        Assert.Equal("Alice Dupont", data[0]);
        Assert.Equal("1234567890", data[1]);
        Assert.Equal("3000.000", data[2]);
        Assert.Equal("2800.000", data[3]);
        Assert.Equal("257.040", data[4]);
        Assert.Equal("463.960", data[5]);
        Assert.Equal("3", data[6]);

        var totalLine = lines.Last(l => l.StartsWith("TOTAL;", StringComparison.Ordinal));
        var total = totalLine.Split(';');
        Assert.Equal(7, total.Length);
        Assert.Equal("TOTAL", total[0]);
        Assert.Equal(string.Empty, total[1]);
        Assert.Equal("3000.000", total[2]);
        Assert.Equal("2800.000", total[3]);
        Assert.Equal("257.040", total[4]);
        Assert.Equal("463.960", total[5]);
        Assert.Equal("1", total[6]);
    }

    [Fact]
    public async Task Export_PropagatesGenerateFailure()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GenerateDtsDeclarationQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<DtsDeclarationDto>(Error.Validation("Quarter", "bad")));

        var handler = new ExportDtsDeclarationQueryHandler(mediator.Object);
        var result = await handler.Handle(new ExportDtsDeclarationQuery(2026, 9), CancellationToken.None);

        Assert.True(result.IsFailure);
    }
}
