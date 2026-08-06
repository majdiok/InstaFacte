using System.Text;
using FactuTrust.Application.Common.Interfaces;
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

public sealed class PayrollWithholdingCertificateTests
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
        var computation = PayrollCalculator.Compute(input, Params());
        var run = PayrollRun.Create(year, month, year).Value;
        var (empRate, employerRate) = PayrollCalculator.ResolveCnssRates(input.Regime, Params());
        var payslip = Payslip.FromComputation(
            run.Id, employeeId, name, "EMP-001", cnss, year, month, computation,
            empRate, employerRate);
        run.SetPayslips([payslip]);
        return run;
    }

    private static TenantCompanySummaryDto CompanySummary() => new()
    {
        CompanyName = "Société Test",
        Nif = "1234567A",
        TaxRegimeDisplay = "Réel",
        AddressLine = "Tunis"
    };

    private static GeneratePayrollWithholdingCertificatesQueryHandler CreateGenerateHandler(
        IReadOnlyList<PayrollRun> runs,
        TenantCompanySummaryDto? company = null)
    {
        var runRepo = new Mock<IPayrollRunRepository>();
        runRepo.Setup(r => r.ListByMonthRangeWithPayslipsAsync(
                It.IsAny<int>(), 1, 12, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(runs);

        var employeeRepo = new Mock<IEmployeeRepository>();
        employeeRepo.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, Employee>());

        var companyProvider = new Mock<ITenantCompanySummaryProvider>();
        companyProvider.Setup(c => c.GetCurrentTenantSummaryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(company ?? CompanySummary());

        return new GeneratePayrollWithholdingCertificatesQueryHandler(
            runRepo.Object,
            employeeRepo.Object,
            companyProvider.Object);
    }

    [Fact]
    public async Task Generate_InvalidYear_Fails()
    {
        var handler = CreateGenerateHandler(Array.Empty<PayrollRun>());
        var result = await handler.Handle(new GeneratePayrollWithholdingCertificatesQuery(1999), CancellationToken.None);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Generate_MissingNif_Fails()
    {
        var handler = CreateGenerateHandler(
            Array.Empty<PayrollRun>(),
            new TenantCompanySummaryDto { CompanyName = "X", Nif = "", TaxRegimeDisplay = "Réel" });

        var result = await handler.Handle(new GeneratePayrollWithholdingCertificatesQuery(2026), CancellationToken.None);
        Assert.True(result.IsFailure);
        Assert.Contains("NIF", result.Error.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Generate_NoRuns_EmptyLinesAndAllMonthsMissing()
    {
        var handler = CreateGenerateHandler(Array.Empty<PayrollRun>());
        var result = await handler.Handle(new GeneratePayrollWithholdingCertificatesQuery(2026), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = result.Value;
        Assert.Empty(dto.Lines);
        Assert.Equal(0, dto.EmployeeCount);
        Assert.False(dto.IsComplete);
        Assert.Equal(Enumerable.Range(1, 12), dto.MissingMonths);
    }

    [Fact]
    public async Task Generate_ThreeMonths_AggregatesEmployeeLine()
    {
        var runs = new[]
        {
            BuildRun(2026, 1, EmployeeId, "Alice Dupont", "1234567890", 2000m),
            BuildRun(2026, 2, EmployeeId, "Alice Dupont", "1234567890", 2000m),
            BuildRun(2026, 3, EmployeeId, "Alice Dupont", "1234567890", 2000m)
        };
        var handler = CreateGenerateHandler(runs);

        var result = await handler.Handle(new GeneratePayrollWithholdingCertificatesQuery(2026), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = result.Value;
        Assert.Single(dto.Lines);
        Assert.Equal(3, dto.Lines[0].MonthsCount);
        Assert.Equal(new[] { 1, 2, 3 }, dto.IncludedMonths);
        Assert.False(dto.IsComplete);
        Assert.Equal(runs.Sum(r => r.Payslips.First().GrossSalary), dto.TotalGross);
    }

    [Fact]
    public async Task ExportCsv_TotalRow_HasAlignedColumns()
    {
        var dto = new PayrollWithholdingCertificateBatchDto
        {
            Year = 2026,
            EmployerCompanyName = "Société Test",
            EmployerNif = "1234567A",
            EmployeeCount = 1,
            TotalGross = 6000m,
            TotalAnnualNetTaxable = 4949.199m,
            TotalIrppWithheld = 799.800m,
            TotalCssWithheld = 24.746m,
            TotalWithholding = 824.546m,
            Lines =
            [
                new PayrollWithholdingCertificateLineDto
                {
                    EmployeeId = EmployeeId,
                    EmployeeNumber = "EMP-001",
                    EmployeeName = "Alice Dupont",
                    Cin = "12345678",
                    CnssNumber = "1234567890",
                    MonthsCount = 3,
                    TotalGross = 6000m,
                    AnnualNetTaxable = 4949.199m,
                    TotalIrppWithheld = 799.800m,
                    TotalCssWithheld = 24.746m,
                    TotalWithholding = 824.546m,
                    DocumentReference = "CRS-EMP-001"
                }
            ]
        };

        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GeneratePayrollWithholdingCertificatesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(dto));

        var handler = new ExportPayrollWithholdingCertificatesCsvQueryHandler(mediator.Object);
        var result = await handler.Handle(new ExportPayrollWithholdingCertificatesCsvQuery(2026), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var text = Encoding.UTF8.GetString(result.Value, 3, result.Value.Length - 3);
        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("Matricule;Nom;CIN", lines[0]);

        var data = lines[1].Split(';');
        Assert.Equal(14, data.Length);
        Assert.Equal("Alice Dupont", data[1]);

        var totalLine = lines.Last(l => l.StartsWith("TOTAL;", StringComparison.Ordinal));
        var total = totalLine.Split(';');
        Assert.Equal(14, total.Length);
        Assert.Equal("824.546", total[13]);
    }

    [Fact]
    public async Task ExportCsv_PropagatesGenerateFailure()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GeneratePayrollWithholdingCertificatesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<PayrollWithholdingCertificateBatchDto>(Error.Validation("Year", "bad")));

        var handler = new ExportPayrollWithholdingCertificatesCsvQueryHandler(mediator.Object);
        var result = await handler.Handle(new ExportPayrollWithholdingCertificatesCsvQuery(2026), CancellationToken.None);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void BuildPdfFileName_SanitizesInvalidCharacters()
    {
        var line = new PayrollWithholdingCertificateLineDto
        {
            EmployeeNumber = "EMP/001",
            EmployeeName = "Dupont Jean",
            DocumentReference = "CRS-EMP/001"
        };

        var name = ExportPayrollWithholdingCertificatesZipQueryHandler.BuildPdfFileName(line, 2026);
        Assert.DoesNotContain("/", name);
        Assert.Contains("CRS_2026", name);
    }
}
