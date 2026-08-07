using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain.Payroll;

public sealed class CnssContributionRemittanceBuilderTests
{
    private static readonly Guid EmployeeA = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid EmployeeB = Guid.Parse("22222222-3333-4444-5555-666666666666");

    private static readonly EmployerSnapshot Employer = new(
        "Société Test", "1234567A", "Tunis", "9988776655");

    private static PayrollYearParameters Params() => PayrollParameterDefaults.CreateDefaults(2026).Value;

    private static PayrollRun BuildValidatedRun(int year, int month, params Payslip[] payslips)
    {
        var run = PayrollRun.Create(year, month, year).Value;
        run.SetPayslips(payslips);
        run.Validate("test");
        return run;
    }

    private static Payslip BuildPayslip(
        Guid employeeId,
        string name,
        string? cnss,
        decimal baseSalary,
        SocialRegime regime = SocialRegime.Rsna)
    {
        var input = new PayrollComputationInput
        {
            BaseSalary = baseSalary,
            Regime = regime,
            WorkAccidentRate = 0.4m
        };
        var parameters = Params();
        var computation = PayrollCalculator.Compute(input, parameters);
        var (empRate, employerRate) = PayrollCalculator.ResolveCnssRates(regime, parameters);
        return Payslip.FromComputation(
            Guid.NewGuid(),
            employeeId,
            name,
            "EMP-001",
            cnss,
            2026,
            3,
            computation,
            empRate,
            employerRate);
    }

    [Fact]
    public void Build_NoRun_NotEligible()
    {
        var batch = CnssContributionRemittanceBuilder.Build(
            2026, 3, null, Employer, false, null);

        Assert.False(batch.IsEligible);
        Assert.Empty(batch.Lines);
        Assert.Contains(batch.Warnings, w => w.Contains("Aucun cycle"));
    }

    [Fact]
    public void Build_DraftRun_NotEligible()
    {
        var payslip = BuildPayslip(EmployeeA, "Alice", "1234567890", 2000m);
        var run = PayrollRun.Create(2026, 3, 2026).Value;
        run.SetPayslips([payslip]);

        var batch = CnssContributionRemittanceBuilder.Build(2026, 3, run, Employer, false, null);

        Assert.False(batch.IsEligible);
        Assert.Contains(batch.Warnings, w => w.Contains("pas validé"));
    }

    [Fact]
    public void Build_ValidatedRun_AggregatesTotalsMatchingAccount453()
    {
        var payslipA = BuildPayslip(EmployeeA, "Alice Dupont", "1111111111", 2000m);
        var payslipB = BuildPayslip(EmployeeB, "Bob Martin", "2222222222", 3000m);
        var run = BuildValidatedRun(2026, 3, payslipA, payslipB);

        var batch = CnssContributionRemittanceBuilder.Build(
            2026, 3, run, Employer, false, CnssRemittancePaymentStatus.Pending);

        Assert.True(batch.IsEligible);
        Assert.Equal(2, batch.EmployeeCount);
        Assert.Equal(run.Id, batch.PayrollRunId);

        var expectedCnssEmployee = payslipA.CnssEmployee + payslipB.CnssEmployee;
        var expectedCnssEmployer = payslipA.CnssEmployer + payslipB.CnssEmployer;
        var expectedWorkAccident = payslipA.WorkAccidentContribution + payslipB.WorkAccidentContribution;

        Assert.Equal(Math.Round(expectedCnssEmployee, 3), batch.TotalCnssEmployee);
        Assert.Equal(Math.Round(expectedCnssEmployer, 3), batch.TotalCnssEmployer);
        Assert.Equal(Math.Round(expectedWorkAccident, 3), batch.TotalWorkAccident);
        Assert.Equal(
            Math.Round(expectedCnssEmployee + expectedCnssEmployer + expectedWorkAccident, 3),
            batch.TotalDue);
    }

    [Fact]
    public void Build_MissingEmployerCnss_AddsWarning()
    {
        var employer = new EmployerSnapshot("Test", "1234567A", null, null);
        var payslip = BuildPayslip(EmployeeA, "Alice", null, 2000m);
        var run = BuildValidatedRun(2026, 3, payslip);

        var batch = CnssContributionRemittanceBuilder.Build(2026, 3, run, employer, false, null);

        Assert.Contains(batch.Warnings, w => w.Contains("Matricule employeur CNSS"));
        Assert.Contains(batch.Lines[0].Warnings, w => w.Contains("CNSS manquant"));
    }

    [Fact]
    public void Build_SivpExonere_IncludedWithZeroContributions()
    {
        var payslip = BuildPayslip(EmployeeA, "Stagiaire", "3333333333", 500m, SocialRegime.SivpExonere);
        var run = BuildValidatedRun(2026, 3, payslip);

        var batch = CnssContributionRemittanceBuilder.Build(2026, 3, run, Employer, false, null);

        Assert.Single(batch.Lines);
        Assert.Equal(0m, batch.Lines[0].LineTotal);
        Assert.Equal(0m, batch.TotalDue);
        Assert.Contains(batch.Lines[0].Warnings, w => w.Contains("Régime exonéré CNSS"));
        Assert.Contains(batch.Warnings, w => w.Contains("régime exonéré CNSS"));
    }

    [Fact]
    public void Build_Rsna_WithContributions_HasNoExoneratedRegimeWarning()
    {
        var payslip = BuildPayslip(EmployeeA, "Alice Dupont", "1111111111", 2000m);
        var run = BuildValidatedRun(2026, 3, payslip);

        var batch = CnssContributionRemittanceBuilder.Build(2026, 3, run, Employer, false, null);

        Assert.DoesNotContain(batch.Lines[0].Warnings, w => w.Contains("Régime exonéré CNSS"));
        Assert.DoesNotContain(batch.Warnings, w => w.Contains("régime exonéré CNSS"));
    }
}
