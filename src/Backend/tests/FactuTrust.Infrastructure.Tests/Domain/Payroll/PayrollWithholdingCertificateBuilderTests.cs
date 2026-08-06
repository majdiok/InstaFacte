using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain.Payroll;

public sealed class PayrollWithholdingCertificateBuilderTests
{
    private static readonly Guid EmployeeA = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid EmployeeB = Guid.Parse("22222222-3333-4444-5555-666666666666");

    private static readonly EmployerSnapshot Employer = new("Société Test", "1234567A", "Tunis");

    private static PayrollYearParameters Params() => PayrollParameterDefaults.CreateDefaults(2026).Value;

    private static Payslip BuildPayslip(
        Guid employeeId,
        string name,
        string number,
        int year,
        int month,
        decimal baseSalary,
        decimal? irppRegularization = null,
        decimal? cssRegularization = null)
    {
        var input = new PayrollComputationInput
        {
            BaseSalary = baseSalary,
            Regime = SocialRegime.Rsna,
            WorkAccidentRate = 0.4m,
            IrppRegularization = irppRegularization ?? 0m,
            CssRegularization = cssRegularization ?? 0m
        };
        var computation = PayrollCalculator.Compute(input, Params());
        return Payslip.FromComputation(
            Guid.NewGuid(),
            employeeId,
            name,
            number,
            "1234567890",
            year,
            month,
            computation,
            9.18m,
            16.57m);
    }

    [Fact]
    public void Build_NoPayslips_EmptyBatchWithAllMonthsMissing()
    {
        var batch = PayrollWithholdingCertificateBuilder.Build(
            2026,
            Array.Empty<Payslip>(),
            new Dictionary<Guid, EmployeeIdentitySnapshot>(),
            Employer,
            Array.Empty<int>());

        Assert.Equal(0, batch.EmployeeCount);
        Assert.Empty(batch.Lines);
        Assert.False(batch.IsComplete);
        Assert.Equal(Enumerable.Range(1, 12), batch.MissingMonths);
        Assert.Empty(batch.IncludedMonths);
    }

    [Fact]
    public void Build_SingleEmployeeThreeMonths_AggregatesTotals()
    {
        var payslips = new[]
        {
            BuildPayslip(EmployeeA, "Alice Dupont", "EMP-001", 2026, 1, 2000m),
            BuildPayslip(EmployeeA, "Alice Dupont", "EMP-001", 2026, 2, 2000m),
            BuildPayslip(EmployeeA, "Alice Dupont", "EMP-001", 2026, 3, 2000m)
        };

        var employees = new Dictionary<Guid, EmployeeIdentitySnapshot>
        {
            [EmployeeA] = new(EmployeeA, "EMP-001", "Alice Dupont", "12345678", "1234567890", "Tunis", false)
        };

        var batch = PayrollWithholdingCertificateBuilder.Build(
            2026,
            payslips,
            employees,
            Employer,
            new[] { 1, 2, 3 });

        Assert.Single(batch.Lines);
        var line = batch.Lines[0];
        Assert.Equal(3, line.MonthsCount);
        Assert.True(line.IsPartialYear);
        Assert.Equal(R(payslips.Sum(p => p.GrossSalary)), line.TotalGross);
        Assert.Equal(R(payslips.Sum(p => p.MonthlyNetTaxable)), line.AnnualNetTaxable);
        Assert.Equal(R(payslips.Sum(p => p.Irpp + p.IrppRegularization)), line.TotalIrppWithheld);
        Assert.Equal(R(payslips.Sum(p => p.Css + p.CssRegularization)), line.TotalCssWithheld);
        Assert.Equal(R(line.TotalIrppWithheld + line.TotalCssWithheld), line.TotalWithholding);
        Assert.Contains("Année incomplète", line.Warnings);
    }

    [Fact]
    public void Build_TwoEmployees_ProducesTwoLinesSortedByName()
    {
        var payslips = new[]
        {
            BuildPayslip(EmployeeB, "Zorro Last", "EMP-002", 2026, 4, 1500m),
            BuildPayslip(EmployeeA, "Alice Dupont", "EMP-001", 2026, 4, 2000m)
        };

        var employees = new Dictionary<Guid, EmployeeIdentitySnapshot>
        {
            [EmployeeA] = new(EmployeeA, "EMP-001", "Alice Dupont", "11111111", "1111111111", null, false),
            [EmployeeB] = new(EmployeeB, "EMP-002", "Zorro Last", "22222222", "2222222222", null, true)
        };

        var batch = PayrollWithholdingCertificateBuilder.Build(
            2026,
            payslips,
            employees,
            Employer,
            new[] { 4 });

        Assert.Equal(2, batch.EmployeeCount);
        Assert.Equal("Alice Dupont", batch.Lines[0].EmployeeName);
        Assert.Equal("Zorro Last", batch.Lines[1].EmployeeName);
        Assert.True(batch.Lines[1].IsHeadOfFamily);
    }

    [Fact]
    public void Build_DecemberRegularization_IncludesIrppAndCssRegularizationInTotals()
    {
        var payslips = new[]
        {
            BuildPayslip(EmployeeA, "Alice Dupont", "EMP-001", 2026, 11, 2000m),
            BuildPayslip(EmployeeA, "Alice Dupont", "EMP-001", 2026, 12, 2000m, irppRegularization: -50m, cssRegularization: -2m)
        };

        var employees = new Dictionary<Guid, EmployeeIdentitySnapshot>
        {
            [EmployeeA] = new(EmployeeA, "EMP-001", "Alice Dupont", "12345678", "1234567890", null, false)
        };

        var batch = PayrollWithholdingCertificateBuilder.Build(
            2026,
            payslips,
            employees,
            Employer,
            new[] { 11, 12 });

        var line = batch.Lines[0];
        var dec = payslips[1];
        var expectedIrpp = R(payslips.Sum(p => p.Irpp + p.IrppRegularization));
        var expectedCss = R(payslips.Sum(p => p.Css + p.CssRegularization));

        Assert.Equal(-50m, dec.IrppRegularization);
        Assert.Equal(expectedIrpp, line.TotalIrppWithheld);
        Assert.Equal(expectedCss, line.TotalCssWithheld);

        var decMonth = line.Months.Single(m => m.Month == 12);
        Assert.True(decMonth.HasPayslip);
        Assert.Equal(dec.IrppRegularization, decMonth.IrppRegularization);
    }

    [Fact]
    public void Build_TwelveMonths_IsCompleteAndNotPartialYear()
    {
        var payslips = Enumerable.Range(1, 12)
            .Select(m => BuildPayslip(EmployeeA, "Alice Dupont", "EMP-001", 2026, m, 2000m))
            .ToList();

        var employees = new Dictionary<Guid, EmployeeIdentitySnapshot>
        {
            [EmployeeA] = new(EmployeeA, "EMP-001", "Alice Dupont", "12345678", "1234567890", "Adresse", false)
        };

        var batch = PayrollWithholdingCertificateBuilder.Build(
            2026,
            payslips,
            employees,
            Employer,
            Enumerable.Range(1, 12).ToList());

        Assert.True(batch.IsComplete);
        Assert.Empty(batch.MissingMonths);
        var line = batch.Lines[0];
        Assert.False(line.IsPartialYear);
        Assert.Equal(12, line.MonthsCount);
        Assert.DoesNotContain(line.Warnings, w => w == "Année incomplète");
        Assert.DoesNotContain(line.Warnings, w => w == "CIN manquant");
    }

    [Fact]
    public void Build_MissingCin_AddsWarning()
    {
        var payslips = new[] { BuildPayslip(EmployeeA, "Alice Dupont", "EMP-001", 2026, 1, 2000m) };
        var employees = new Dictionary<Guid, EmployeeIdentitySnapshot>
        {
            [EmployeeA] = new(EmployeeA, "EMP-001", "Alice Dupont", null, "1234567890", null, false)
        };

        var batch = PayrollWithholdingCertificateBuilder.Build(
            2026,
            payslips,
            employees,
            Employer,
            new[] { 1 });

        Assert.Contains("CIN manquant", batch.Lines[0].Warnings);
    }

    private static decimal R(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}
